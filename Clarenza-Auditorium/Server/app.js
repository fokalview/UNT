'use strict';
// Clarenza hosted service: organizations (teams), org-scoped lessons, shared class sessions,
// recordings for self-paced replay, shared questions, teacher insights, build log and jobs.
// Node 22 built-ins only. Every permission rule is enforced here, never in a client.
const http = require('node:http');
const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const { open } = require('./db');
const { validateLesson, contentOf } = require('./lesson');
const { offlineGenerator } = require('./generator');

class HttpError extends Error { constructor(status, message) { super(message); this.status = status; } }
const fail = (status, message) => { throw new HttpError(status, message); };
const sha = s => crypto.createHash('sha256').update(String(s)).digest('hex');
const newId = () => crypto.randomUUID();
const SESSION_MS = 8 * 3600000;
const LEVELS = ['Beginner', 'Intermediate', 'Advanced'];

function createApp(options = {}) {
  const db = open(options.dbFile || ':memory:');
  const generator = options.generator || offlineGenerator;
  const heartbeatMs = options.heartbeatMs || 20000;
  const dailyJobLimit = options.dailyJobLimit || 20;
  const now = options.now || Date.now;
  const listeners = new Map();   // session id -> Set of open SSE responses
  const limits = new Map();      // rate-limit buckets (in memory; resets on restart)
  let closed = false;

  const one = (sql, ...p) => db.prepare(sql).get(...p);
  const all = (sql, ...p) => db.prepare(sql).all(...p);
  const run = (sql, ...p) => db.prepare(sql).run(...p);
  function tx(fn) {
    db.exec('BEGIN IMMEDIATE');
    try { const result = fn(); db.exec('COMMIT'); return result; }
    catch (e) { db.exec('ROLLBACK'); throw e; }
  }
  function limit(key, max, windowMs) {
    const t = now(); let b = limits.get(key);
    if (!b || b.until < t) b = { count: 0, until: t + windowMs };
    b.count++; limits.set(key, b);
    if (b.count > max) fail(429, 'Too many attempts. Wait and try again.');
  }
  const text = (v, min, max, label) => {
    if (typeof v !== 'string' || v.trim().length < min || v.length > max) fail(400, label + ' must be ' + min + '–' + max + ' characters.');
    return v.trim();
  };

  // ---------- auth ----------
  function authenticate(req) {
    const header = req.headers.authorization || '';
    if (!header.startsWith('Bearer ')) fail(401, 'Please sign in.');
    const row = one('SELECT u.id, u.username, s.expires_at FROM auth_sessions s JOIN users u ON u.id = s.user_id WHERE s.token_hash = ?', sha(header.slice(7)));
    if (!row || row.expires_at < now()) fail(401, 'Please sign in again.');
    return { id: row.id, username: row.username };
  }
  function issueToken(userId) {
    const token = crypto.randomBytes(32).toString('hex');
    run('INSERT INTO auth_sessions (token_hash, user_id, expires_at) VALUES (?,?,?)', sha(token), userId, now() + SESSION_MS);
    return token;
  }
  function hashPassword(password, salt) { return crypto.scryptSync(password, salt, 64).toString('hex'); }
  function memberships(userId) {
    return all('SELECT o.id AS orgId, o.name, m.role FROM memberships m JOIN orgs o ON o.id = m.org_id WHERE m.user_id = ? ORDER BY o.name', userId);
  }
  // Non-members get 404 so the existence of an organization or its contents never leaks.
  function roleIn(user, orgId) {
    const m = one('SELECT role FROM memberships WHERE org_id = ? AND user_id = ?', orgId, user.id);
    if (!m) fail(404, 'Not found.');
    return m.role;
  }
  const canTeach = role => role === 'owner' || role === 'teacher';

  function register(body, req) {
    limit('register:' + (req.socket.remoteAddress || ''), 30, 3600000);
    if (typeof body.username !== 'string' || !/^[a-zA-Z0-9_.-]{3,40}$/.test(body.username)) fail(400, 'Username: 3–40 letters, digits, dots, dashes or underscores.');
    if (typeof body.password !== 'string' || body.password.length < 12 || body.password.length > 128) fail(400, 'Use a password of 12–128 characters.');
    const hasInvite = typeof body.invite === 'string' && body.invite.length > 0;
    const hasOrg = typeof body.orgName === 'string' && body.orgName.length > 0;
    if (hasInvite === hasOrg) fail(400, 'Provide either an invite code or a name for a new organization.');
    const orgName = hasOrg ? text(body.orgName, 3, 80, 'Organization name') : null;
    return tx(() => {
      if (one('SELECT 1 AS x FROM users WHERE username_key = ?', body.username.toLowerCase())) fail(409, 'Username already exists.');
      const userId = newId(), salt = crypto.randomBytes(16).toString('hex');
      run('INSERT INTO users (id, username, username_key, salt, hash, created_at) VALUES (?,?,?,?,?,?)', userId, body.username, body.username.toLowerCase(), salt, hashPassword(body.password, salt), now());
      if (hasInvite) redeemInvite(body.invite, userId);
      else { const orgId = newId(); run('INSERT INTO orgs (id, name, created_by, created_at) VALUES (?,?,?,?)', orgId, orgName, userId, now()); run('INSERT INTO memberships (org_id, user_id, role) VALUES (?,?,?)', orgId, userId, 'owner'); }
      return { token: issueToken(userId), id: userId, username: body.username, memberships: memberships(userId) };
    });
  }
  function login(body) {
    const key = String(body.username || '').toLowerCase();
    limit('login:' + key, 8, 60000);
    const user = one('SELECT * FROM users WHERE username_key = ?', key);
    // Always run scrypt so response time does not reveal whether the username exists.
    const supplied = Buffer.from(hashPassword(typeof body.password === 'string' ? body.password.slice(0, 128) : '', user ? user.salt : 'invalid-user'), 'hex');
    if (!user || !crypto.timingSafeEqual(supplied, Buffer.from(user.hash, 'hex'))) fail(401, 'Incorrect username or password.');
    limits.delete('login:' + key);
    return { token: issueToken(user.id), id: user.id, username: user.username, memberships: memberships(user.id) };
  }

  // ---------- organizations and invites ----------
  function redeemInvite(code, userId) {
    const invite = one('SELECT * FROM invites WHERE code_hash = ?', sha(code));
    if (!invite || invite.used_by || invite.expires_at < now()) fail(400, 'This invite is invalid, used or expired.');
    if (one('SELECT 1 AS x FROM memberships WHERE org_id = ? AND user_id = ?', invite.org_id, userId)) fail(409, 'You already belong to this organization.');
    run('UPDATE invites SET used_by = ? WHERE code_hash = ?', userId, invite.code_hash);
    run('INSERT INTO memberships (org_id, user_id, role) VALUES (?,?,?)', invite.org_id, userId, invite.role);
    return { orgId: invite.org_id, role: invite.role };
  }
  function createInvite(user, orgId, body) {
    const role = roleIn(user, orgId);
    if (!['teacher', 'student'].includes(body.role)) fail(400, 'Invite role must be teacher or student.');
    if (role === 'student' || (role === 'teacher' && body.role !== 'student')) fail(403, 'You cannot create this invite.');
    const hours = Number.isInteger(body.hours) ? body.hours : 72;
    if (hours < 1 || hours > 720) fail(400, 'Invite lifetime must be 1–720 hours.');
    const code = crypto.randomBytes(9).toString('base64url');
    run('INSERT INTO invites (code_hash, org_id, role, created_by, expires_at) VALUES (?,?,?,?,?)', sha(code), orgId, body.role, user.id, now() + hours * 3600000);
    return { code, role: body.role, expiresAt: now() + hours * 3600000 }; // the code is shown once and never stored in clear
  }

  // ---------- lessons ----------
  const visible = (row, user) => row.scope === 'published' || row.owner_id === user.id;
  function lessonOut(row, full) {
    const meta = { id: row.id, orgId: row.org_id, familyId: row.family_id, parentId: row.parent_id || '', version: row.version, ownerId: row.owner_id, scope: row.scope, approval: row.approval, approvedBy: row.approved_by || '', approvedAt: row.approved_at || '', classType: row.class_type, title: row.title };
    return full ? { ...JSON.parse(row.document), ...meta } : meta;
  }
  function readableLesson(user, lessonId) {
    const row = one('SELECT * FROM lessons WHERE id = ?', String(lessonId));
    if (!row || !one('SELECT 1 AS x FROM memberships WHERE org_id = ? AND user_id = ?', row.org_id, user.id) || !visible(row, user)) fail(404, 'Not found.');
    return row;
  }
  function listLessons(user, orgId, query) {
    roleIn(user, orgId);
    const rows = all('SELECT * FROM lessons WHERE org_id = ? AND (scope = ? OR owner_id = ?) ORDER BY family_id, version DESC', orgId, 'published', user.id);
    if (query.get('all') === '1') return { items: rows.map(r => lessonOut(r)) };
    // Default: per family, the newest published version, plus the caller's own newer unpublished work.
    const items = [], byFamily = new Map();
    for (const r of rows) { if (!byFamily.has(r.family_id)) byFamily.set(r.family_id, []); byFamily.get(r.family_id).push(r); }
    for (const versions of byFamily.values()) {
      const published = versions.find(r => r.scope === 'published');
      const mine = versions.find(r => r.scope !== 'published');
      if (published) items.push(lessonOut(published));
      if (mine && (!published || mine.version > published.version)) items.push(lessonOut(mine));
    }
    return { items };
  }
  function saveLesson(user, orgId, body) {
    const role = roleIn(user, orgId);
    if (!['study', 'draft', 'published'].includes(body.scope)) fail(400, 'Choose study, draft or published.');
    if (body.scope !== 'study' && !canTeach(role)) fail(403, 'Students can save only private study lessons.');
    if (body.scope === 'published' && body.reviewed !== true) fail(400, 'Review the lesson and explicitly approve publication.');
    const content = contentOf(validateLesson(body.document));
    return tx(() => {
      let parent = null;
      if (body.parentId) {
        parent = one('SELECT * FROM lessons WHERE id = ? AND org_id = ?', String(body.parentId), orgId);
        if (!parent || !visible(parent, user)) fail(404, 'Not found.');
        if (parent.owner_id !== user.id) fail(403, 'Only the owner can revise this lesson.');
      }
      const familyId = parent ? parent.family_id : newId();
      const latest = one('SELECT MAX(version) AS v FROM lessons WHERE family_id = ?', familyId).v || 0;
      if (parent && latest !== parent.version) fail(409, 'A newer version exists. Open the latest version before editing.');
      const id = newId(), version = latest + 1, approved = body.scope === 'published';
      const classType = approved ? 'teacher' : body.document.classType === 'ai' ? 'ai' : 'source';
      run('INSERT INTO lessons (id, org_id, family_id, parent_id, version, owner_id, scope, approval, approved_by, approved_at, class_type, title, document, created_at) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)',
        id, orgId, familyId, parent ? parent.id : null, version, user.id, body.scope,
        approved ? 'teacher-approved' : body.scope === 'draft' ? 'draft' : 'unreviewed',
        approved ? user.username : null, approved ? new Date(now()).toISOString() : null, classType, content.title, JSON.stringify(content), now());
      const seq = (one('SELECT MAX(seq) AS s FROM build_events WHERE family_id = ?', familyId).s || 0) + 1;
      const type = approved ? (parent ? 'revision_published' : 'published') : body.scope === 'draft' ? 'draft_saved' : 'study_saved';
      run('INSERT INTO build_events (org_id, family_id, seq, at, actor_id, type, lesson_id, summary) VALUES (?,?,?,?,?,?,?,?)', orgId, familyId, seq, now(), user.id, type, id, content.title + ' v' + version);
      return { id, ok: true, document: lessonOut(one('SELECT * FROM lessons WHERE id = ?', id), true) };
    });
  }
  function buildLog(user, orgId, familyId) {
    roleIn(user, orgId);
    const rows = all('SELECT b.*, l.scope, l.owner_id, u.username FROM build_events b JOIN lessons l ON l.id = b.lesson_id JOIN users u ON u.id = b.actor_id WHERE b.org_id = ? AND b.family_id = ? ORDER BY b.seq', orgId, String(familyId)).filter(r => visible(r, user));
    if (!rows.length) fail(404, 'Not found.');
    return { items: rows.map(r => ({ seq: r.seq, at: r.at, actor: r.username, type: r.type, lessonId: r.lesson_id, summary: r.summary })) };
  }

  // ---------- progress and quiz attempts ----------
  const int = (v, min, max, label) => { if (!Number.isInteger(v) || v < min || v > max) fail(400, 'Invalid ' + label + '.'); return v; };
  function putProgress(user, lessonId, body) {
    const row = readableLesson(user, lessonId), doc = JSON.parse(row.document);
    const section = int(body.section, 0, doc.sections.length - 1, 'section');
    const quizIndex = int(body.quizIndex === undefined ? 0 : body.quizIndex, 0, doc.quiz.length, 'quiz index');
    const score = int(body.score === undefined ? 0 : body.score, 0, doc.quiz.length, 'score');
    const clock = typeof body.clock === 'number' && body.clock >= 0 && body.clock < 1e7 ? body.clock : 0;
    run('INSERT INTO progress (user_id, lesson_id, section, quiz_index, score, clock, completed, updated_at) VALUES (?,?,?,?,?,?,?,?) ON CONFLICT (user_id, lesson_id) DO UPDATE SET section = excluded.section, quiz_index = excluded.quiz_index, score = excluded.score, clock = excluded.clock, completed = MAX(completed, excluded.completed), updated_at = excluded.updated_at',
      user.id, row.id, section, quizIndex, score, clock, body.completed === true ? 1 : 0, now());
    return { ok: true };
  }
  function getProgress(user, lessonId) {
    const row = readableLesson(user, lessonId);
    const p = one('SELECT * FROM progress WHERE user_id = ? AND lesson_id = ?', user.id, row.id);
    return p ? { section: p.section, quizIndex: p.quiz_index, score: p.score, clock: p.clock, completed: !!p.completed, updatedAt: p.updated_at } : { section: 0, quizIndex: 0, score: 0, clock: 0, completed: false, updatedAt: 0 };
  }
  // The server marks the answer; only a learner's first attempt per question counts for insights.
  function attempt(user, lessonId, body) {
    const row = readableLesson(user, lessonId), doc = JSON.parse(row.document);
    const question = int(body.question, 0, doc.quiz.length - 1, 'question'), selected = int(body.selected, 0, 2, 'answer');
    const correct = doc.quiz[question].correct === selected;
    run('INSERT OR IGNORE INTO quiz_attempts (user_id, lesson_id, question, selected, correct, at) VALUES (?,?,?,?,?,?)', user.id, row.id, question, selected, correct ? 1 : 0, now());
    return { correct, explanation: doc.quiz[question].explanation };
  }

  // ---------- sessions ----------
  function sessionFor(user, sessionId) {
    const s = one('SELECT * FROM sessions WHERE id = ?', String(sessionId));
    if (!s || !one('SELECT 1 AS x FROM memberships WHERE org_id = ? AND user_id = ?', s.org_id, user.id)) fail(404, 'Not found.');
    const isHost = s.host_id === user.id;
    const joined = !!one('SELECT 1 AS x FROM session_members WHERE session_id = ? AND user_id = ?', s.id, user.id);
    if (!isHost && !joined && s.visibility !== 'org') fail(404, 'Not found.');
    return { s, isHost, joined };
  }
  function sessionOut(s) {
    return { id: s.id, orgId: s.org_id, lessonId: s.lesson_id, hostId: s.host_id, title: s.title, state: s.state, visibility: s.visibility, section: s.section, playing: !!s.playing, startedAt: s.started_at, endedAt: s.ended_at || 0 };
  }
  // Append one event. Callers hold a transaction, so seq is gap-free and ordered per session.
  function appendEvent(s, actorId, type, payload) {
    const seq = (one('SELECT MAX(seq) AS q FROM session_events WHERE session_id = ?', s.id).q || 0) + 1;
    const event = { seq, at: now() - s.started_at, actor: actorId, type, payload };
    run('INSERT INTO session_events (session_id, seq, at_ms, actor_id, type, payload) VALUES (?,?,?,?,?,?)', s.id, seq, event.at, actorId, type, JSON.stringify(payload));
    return event;
  }
  function publish(sessionId, events, end) {
    const set = listeners.get(sessionId); if (!set) return;
    for (const res of set) { for (const e of events) writeEvent(res, e); if (end) res.end(); }
    if (end) listeners.delete(sessionId);
  }
  const writeEvent = (res, e) => res.write('id: ' + e.seq + '\nevent: ' + e.type + '\ndata: ' + JSON.stringify(e) + '\n\n');
  const eventRow = r => ({ seq: r.seq, at: r.at_ms, actor: r.actor_id, type: r.type, payload: JSON.parse(r.payload) });

  function createSession(user, orgId, body) {
    const role = roleIn(user, orgId);
    const lesson = readableLesson(user, body.lessonId);
    if (lesson.org_id !== orgId) fail(404, 'Not found.');
    const visibility = body.visibility === 'invite' ? 'invite' : 'org';
    if (visibility === 'org') {
      if (!canTeach(role)) fail(403, 'Only teachers can open a class to the organization. Start an invite-only study party instead.');
      if (lesson.scope !== 'published') fail(400, 'Only a published lesson can be opened to the organization.');
    }
    const title = body.title === undefined ? lesson.title : text(body.title, 1, 120, 'Title');
    const code = visibility === 'invite' ? crypto.randomBytes(6).toString('base64url') : null;
    const id = newId();
    tx(() => {
      run('INSERT INTO sessions (id, org_id, lesson_id, host_id, title, state, visibility, join_code_hash, started_at) VALUES (?,?,?,?,?,?,?,?,?)', id, orgId, lesson.id, user.id, title, 'live', visibility, code ? sha(code) : null, now());
      run('INSERT INTO session_members (session_id, user_id, joined_at) VALUES (?,?,?)', id, user.id, now());
      appendEvent(one('SELECT * FROM sessions WHERE id = ?', id), user.id, 'started', { lessonId: lesson.id, section: 0 });
    });
    return { ...sessionOut(one('SELECT * FROM sessions WHERE id = ?', id)), joinCode: code || undefined };
  }
  function listSessions(user, orgId, query) {
    roleIn(user, orgId);
    const state = query.get('state') === 'ended' ? 'ended' : 'live';
    const rows = all('SELECT s.* FROM sessions s WHERE s.org_id = ? AND s.state = ? AND (s.visibility = ? OR s.host_id = ? OR EXISTS (SELECT 1 FROM session_members m WHERE m.session_id = s.id AND m.user_id = ?)) ORDER BY s.started_at DESC LIMIT 200', orgId, state, 'org', user.id, user.id);
    return { items: rows.map(sessionOut) };
  }
  function joinSession(user, sessionId, body) {
    const s = one('SELECT * FROM sessions WHERE id = ?', String(sessionId));
    if (!s || !one('SELECT 1 AS x FROM memberships WHERE org_id = ? AND user_id = ?', s.org_id, user.id)) fail(404, 'Not found.');
    const existing = one('SELECT * FROM session_members WHERE session_id = ? AND user_id = ?', s.id, user.id);
    if (s.visibility === 'invite' && !existing && s.host_id !== user.id) {
      limit('join:' + user.id, 20, 60000);
      if (typeof body.code !== 'string' || sha(body.code) !== s.join_code_hash) fail(404, 'Not found.');
    }
    if (s.state !== 'live') fail(409, 'This class has ended. Open its recording instead.');
    let event = null;
    tx(() => {
      if (!existing) { run('INSERT INTO session_members (session_id, user_id, joined_at) VALUES (?,?,?)', s.id, user.id, now()); event = appendEvent(s, user.id, 'joined', { username: user.username }); }
      else if (existing.left_at) { run('UPDATE session_members SET left_at = NULL WHERE session_id = ? AND user_id = ?', s.id, user.id); event = appendEvent(s, user.id, 'joined', { username: user.username }); }
    });
    if (event) publish(s.id, [event]);
    return sessionOut(one('SELECT * FROM sessions WHERE id = ?', s.id));
  }
  function leaveSession(user, sessionId) {
    const { s, joined } = sessionFor(user, sessionId);
    if (!joined || s.state !== 'live') return { ok: true };
    const event = tx(() => { run('UPDATE session_members SET left_at = ? WHERE session_id = ? AND user_id = ?', now(), s.id, user.id); return appendEvent(s, user.id, 'left', { username: user.username }); });
    publish(s.id, [event]);
    return { ok: true };
  }
  function control(user, sessionId, body) {
    const { s, isHost } = sessionFor(user, sessionId);
    if (!isHost) fail(403, 'Only the host controls the class.');
    if (s.state !== 'live') fail(409, 'This class has ended.');
    const sections = JSON.parse(one('SELECT document FROM lessons WHERE id = ?', s.lesson_id).document).sections.length;
    let event = null, end = false;
    tx(() => {
      if (body.action === 'play') { if (!s.playing) { run('UPDATE sessions SET playing = 1 WHERE id = ?', s.id); event = appendEvent(s, user.id, 'play', { section: s.section }); } }
      else if (body.action === 'pause') { if (s.playing) { run('UPDATE sessions SET playing = 0 WHERE id = ?', s.id); event = appendEvent(s, user.id, 'pause', { section: s.section }); } }
      else if (body.action === 'goto') { const section = int(body.section, 0, sections - 1, 'section'); if (section !== s.section) { run('UPDATE sessions SET section = ? WHERE id = ?', section, s.id); event = appendEvent(s, user.id, 'goto', { section }); } }
      else if (body.action === 'end') { run('UPDATE sessions SET state = ?, playing = 0, ended_at = ? WHERE id = ?', 'ended', now(), s.id); event = appendEvent(s, user.id, 'ended', {}); end = true; }
      else fail(400, 'Action must be play, pause, goto or end.');
    });
    if (event) publish(s.id, [event], end);
    return sessionOut(one('SELECT * FROM sessions WHERE id = ?', s.id));
  }
  function recording(user, sessionId) {
    const { s } = sessionFor(user, sessionId);
    const lesson = one('SELECT * FROM lessons WHERE id = ?', s.lesson_id);
    return { session: sessionOut(s), lesson: lessonOut(lesson, true), events: all('SELECT * FROM session_events WHERE session_id = ? ORDER BY seq', s.id).map(eventRow) };
  }
  // Server-Sent Events: a snapshot, then every event after Last-Event-ID, then live events.
  function stream(user, sessionId, req, res) {
    const { s } = sessionFor(user, sessionId);
    const after = Number.parseInt(req.headers['last-event-id'] || '0', 10) || 0;
    res.writeHead(200, { 'Content-Type': 'text/event-stream', 'Cache-Control': 'no-store', Connection: 'keep-alive', 'X-Accel-Buffering': 'no' });
    res.write('event: snapshot\ndata: ' + JSON.stringify(sessionOut(s)) + '\n\n');
    for (const r of all('SELECT * FROM session_events WHERE session_id = ? AND seq > ? ORDER BY seq', s.id, after)) writeEvent(res, eventRow(r));
    if (s.state !== 'live') return res.end();
    if (!listeners.has(s.id)) listeners.set(s.id, new Set());
    listeners.get(s.id).add(res);
    const beat = setInterval(() => res.write(': keep-alive\n\n'), heartbeatMs);
    req.on('close', () => { clearInterval(beat); const set = listeners.get(s.id); if (set) { set.delete(res); if (!set.size) listeners.delete(s.id); } });
  }

  // ---------- questions ----------
  function ask(user, sessionId, body) {
    const { s, isHost, joined } = sessionFor(user, sessionId);
    if (!isHost && !joined) fail(403, 'Join the class before asking.');
    const q = { id: newId(), text: text(body.text, 1, 2000, 'Question'), section: Number.isInteger(body.section) ? body.section : s.section, shared: body.shared === true };
    const event = tx(() => {
      run('INSERT INTO questions (id, org_id, session_id, lesson_id, user_id, section, text, shared, created_at) VALUES (?,?,?,?,?,?,?,?,?)', q.id, s.org_id, s.id, s.lesson_id, user.id, q.section, q.text, q.shared ? 1 : 0, now());
      // A private question is stored for its author only. It is never broadcast or shown to the host.
      return q.shared && s.state === 'live' ? appendEvent(s, user.id, 'question', { id: q.id, section: q.section, text: q.text, username: user.username }) : null;
    });
    if (event) publish(s.id, [event]);
    return q;
  }
  function answer(user, questionId, body) {
    const q = one('SELECT * FROM questions WHERE id = ?', String(questionId));
    if (!q || !q.shared) fail(404, 'Not found.');
    const s = one('SELECT * FROM sessions WHERE id = ?', q.session_id);
    const role = roleIn(user, q.org_id);
    if (s.host_id !== user.id && !canTeach(role)) fail(403, 'Only the host or a teacher can answer.');
    const reply = text(body.answer, 1, 4000, 'Answer');
    const event = tx(() => { run('UPDATE questions SET answer = ?, answered_by = ? WHERE id = ?', reply, user.id, q.id); return s.state === 'live' ? appendEvent(s, user.id, 'answer', { id: q.id, answer: reply, username: user.username }) : null; });
    if (event) publish(s.id, [event]);
    return { ok: true };
  }
  function myQuestions(user, sessionId) {
    const { s } = sessionFor(user, sessionId);
    return { items: all('SELECT id, section, text, shared, answer FROM questions WHERE session_id = ? AND (shared = 1 OR user_id = ?) ORDER BY created_at', s.id, user.id).map(q => ({ ...q, shared: !!q.shared })) };
  }

  // ---------- teacher insights: aggregates and shared questions only ----------
  function insights(user, orgId, query) {
    if (!canTeach(roleIn(user, orgId))) fail(403, 'Insights are for teachers.');
    const lesson = readableLesson(user, query.get('lesson'));
    if (lesson.org_id !== orgId) fail(404, 'Not found.');
    const doc = JSON.parse(lesson.document);
    const p = one('SELECT COUNT(*) AS started, COALESCE(SUM(completed), 0) AS completed FROM progress WHERE lesson_id = ?', lesson.id);
    const attempts = all('SELECT question, COUNT(*) AS attempts, SUM(1 - correct) AS misses FROM quiz_attempts WHERE lesson_id = ? GROUP BY question', lesson.id);
    return {
      lessonId: lesson.id, title: lesson.title, version: lesson.version, started: p.started, completed: p.completed,
      questions: doc.quiz.map((q, i) => { const a = attempts.find(x => x.question === i) || { attempts: 0, misses: 0 }; return { index: i, question: q.question, attempts: a.attempts, misses: a.misses, missRate: a.attempts ? a.misses / a.attempts : 0 }; }),
      sharedQuestions: all('SELECT q.section, q.text, q.answer, u.username FROM questions q JOIN users u ON u.id = q.user_id WHERE q.lesson_id = ? AND q.shared = 1 ORDER BY q.section, q.created_at', lesson.id),
    };
  }

  // ---------- jobs ----------
  function createJob(user, orgId, body) {
    roleIn(user, orgId);
    if (body.kind !== 'lesson') fail(400, 'Unknown job kind.');
    const input = { topic: text(body.topic, 3, 200, 'Topic'), level: body.level, minutes: body.minutes, language: text(body.language === undefined ? 'English' : body.language, 1, 60, 'Language'), sources: body.sources };
    if (!LEVELS.includes(input.level) || !Number.isInteger(input.minutes) || input.minutes < 5 || input.minutes > 30) fail(400, 'Choose a level and a duration of 5–30 minutes.');
    if (!Array.isArray(input.sources) || input.sources.length < 1 || input.sources.length > 100 || input.sources.some(s => typeof s !== 'string' || !s.trim()) || input.sources.join('').length > 50000) fail(400, 'Provide 1–100 source passages, up to 50,000 characters.');
    const used = one('SELECT COUNT(*) AS n FROM jobs WHERE org_id = ? AND created_at > ?', orgId, now() - 86400000).n;
    if (used >= dailyJobLimit) fail(429, 'This organization has reached its daily generation limit.');
    const id = newId();
    run('INSERT INTO jobs (id, org_id, user_id, kind, state, input, created_at) VALUES (?,?,?,?,?,?,?)', id, orgId, user.id, 'lesson', 'queued', JSON.stringify(input), now());
    setImmediate(() => runJob(id));
    return { id, state: 'queued' };
  }
  async function runJob(id) {
    if (closed) return;
    const job = one('SELECT * FROM jobs WHERE id = ? AND state = ?', id, 'queued'); if (!job) return;
    run('UPDATE jobs SET state = ? WHERE id = ?', 'running', id);
    try {
      const result = await generator(JSON.parse(job.input));
      const document = validateLesson(result.document);
      if (document.sections.some(s => !s.sourceIds.length || !s.title || !s.narration || !s.board)) throw new Error('Generated section is incomplete or uncited.');
      if (closed) return;
      run('UPDATE jobs SET state = ?, output = ?, usage = ?, finished_at = ? WHERE id = ?', 'ready', JSON.stringify({ ...contentOf(document), classType: result.usage && result.usage.provider !== 'offline' ? 'ai' : 'source' }), JSON.stringify(result.usage || {}), now(), id);
    } catch (e) {
      if (!closed) run('UPDATE jobs SET state = ?, error = ?, finished_at = ? WHERE id = ?', 'failed', String(e.message).slice(0, 500), now(), id);
    }
  }
  function getJob(user, jobId) {
    const job = one('SELECT * FROM jobs WHERE id = ?', String(jobId));
    if (!job || job.user_id !== user.id) fail(404, 'Not found.');
    return { id: job.id, state: job.state, error: job.error || '', document: job.output ? JSON.parse(job.output) : null, usage: job.usage ? JSON.parse(job.usage) : null };
  }
  // A job interrupted by a restart goes back to the queue.
  run("UPDATE jobs SET state = 'queued' WHERE state = 'running'");
  for (const j of all("SELECT id FROM jobs WHERE state = 'queued'")) setImmediate(() => runJob(j.id));

  // ---------- routing ----------
  const routes = [];
  const route = (method, pattern, handler, opts = {}) => routes.push({ method, regex: new RegExp('^' + pattern.replace(/:(\w+)/g, '(?<$1>[^/]+)') + '$'), handler, ...opts });
  route('POST', '/v1/auth/register', c => register(c.body, c.req), { open: true });
  route('POST', '/v1/auth/login', c => login(c.body), { open: true });
  route('POST', '/v1/auth/logout', c => { run('DELETE FROM auth_sessions WHERE token_hash = ?', sha(c.req.headers.authorization.slice(7))); return { ok: true }; });
  route('GET', '/v1/me', c => ({ id: c.user.id, username: c.user.username, memberships: memberships(c.user.id) }));
  route('POST', '/v1/orgs', c => { const id = newId(), name = text(c.body.name, 3, 80, 'Organization name'); tx(() => { run('INSERT INTO orgs (id, name, created_by, created_at) VALUES (?,?,?,?)', id, name, c.user.id, now()); run('INSERT INTO memberships (org_id, user_id, role) VALUES (?,?,?)', id, c.user.id, 'owner'); }); return { orgId: id, name, role: 'owner' }; });
  route('GET', '/v1/orgs/:org/members', c => { roleIn(c.user, c.p.org); return { items: all('SELECT u.id, u.username, m.role FROM memberships m JOIN users u ON u.id = m.user_id WHERE m.org_id = ? ORDER BY u.username', c.p.org) }; });
  route('POST', '/v1/orgs/:org/invites', c => createInvite(c.user, c.p.org, c.body));
  route('POST', '/v1/invites/accept', c => { limit('invite:' + c.user.id, 10, 60000); return tx(() => redeemInvite(String(c.body.code || ''), c.user.id)); });
  route('GET', '/v1/orgs/:org/lessons', c => listLessons(c.user, c.p.org, c.query));
  route('POST', '/v1/orgs/:org/lessons', c => saveLesson(c.user, c.p.org, c.body));
  route('GET', '/v1/lessons/:id', c => lessonOut(readableLesson(c.user, c.p.id), true));
  route('GET', '/v1/lessons/:id/history', c => { const row = readableLesson(c.user, c.p.id); return { items: all('SELECT * FROM lessons WHERE family_id = ? ORDER BY version', row.family_id).filter(r => visible(r, c.user)).map(r => lessonOut(r)) }; });
  route('GET', '/v1/orgs/:org/families/:family/build', c => buildLog(c.user, c.p.org, c.p.family));
  route('PUT', '/v1/lessons/:id/progress', c => putProgress(c.user, c.p.id, c.body));
  route('GET', '/v1/lessons/:id/progress', c => getProgress(c.user, c.p.id));
  route('POST', '/v1/lessons/:id/attempts', c => attempt(c.user, c.p.id, c.body));
  route('POST', '/v1/orgs/:org/sessions', c => createSession(c.user, c.p.org, c.body));
  route('GET', '/v1/orgs/:org/sessions', c => listSessions(c.user, c.p.org, c.query));
  route('POST', '/v1/sessions/:id/join', c => joinSession(c.user, c.p.id, c.body));
  route('POST', '/v1/sessions/:id/leave', c => leaveSession(c.user, c.p.id));
  route('POST', '/v1/sessions/:id/control', c => control(c.user, c.p.id, c.body));
  route('GET', '/v1/sessions/:id/recording', c => recording(c.user, c.p.id));
  route('GET', '/v1/sessions/:id/events', c => stream(c.user, c.p.id, c.req, c.res), { raw: true });
  route('POST', '/v1/sessions/:id/questions', c => ask(c.user, c.p.id, c.body));
  route('GET', '/v1/sessions/:id/questions', c => myQuestions(c.user, c.p.id));
  route('POST', '/v1/questions/:id/answer', c => answer(c.user, c.p.id, c.body));
  route('GET', '/v1/orgs/:org/insights', c => insights(c.user, c.p.org, c.query));
  route('POST', '/v1/orgs/:org/jobs', c => createJob(c.user, c.p.org, c.body));
  route('GET', '/v1/jobs/:id', c => getJob(c.user, c.p.id));

  // Student view (/student, also / and /watch) and teacher view (/teacher).
  const pages = { '/': 'watch.html', '/watch': 'watch.html', '/student': 'watch.html', '/teacher': 'teacher.html' };
  async function readBody(req) {
    if (!/^application\/json(?:;|$)/i.test(req.headers['content-type'] || '')) fail(415, 'Send application/json.');
    const chunks = []; let size = 0;
    for await (const chunk of req) { size += chunk.length; if (size > 300000) fail(413, 'Request too large.'); chunks.push(chunk); }
    let body; try { body = JSON.parse(Buffer.concat(chunks).toString('utf8') || '{}'); } catch { fail(400, 'Invalid JSON.'); }
    if (!body || typeof body !== 'object' || Array.isArray(body)) fail(400, 'Invalid JSON.');
    return body;
  }
  const server = http.createServer(async (req, res) => {
    const url = new URL(req.url, 'http://local');
    try {
      if (req.method === 'GET' && url.pathname === '/health') return send(res, 200, { ready: true, service: 'clarenza-hosted', version: 1 });
      if (req.method === 'GET' && pages[url.pathname]) {
        const page = fs.readFileSync(path.join(__dirname, 'public', pages[url.pathname]));
        res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store', 'Content-Security-Policy': "default-src 'self'; script-src 'unsafe-inline'; style-src 'unsafe-inline'", 'X-Content-Type-Options': 'nosniff', 'X-Frame-Options': 'DENY' });
        return res.end(page);
      }
      for (const r of routes) {
        const m = r.method === req.method && r.regex.exec(url.pathname); if (!m) continue;
        // Browsers on other sites cannot drive this API: no CORS headers are sent, and
        // state-changing requests that carry a foreign Origin are refused.
        const origin = req.headers.origin;
        if (origin && req.method !== 'GET' && origin !== 'http://' + req.headers.host && origin !== 'https://' + req.headers.host) fail(403, 'Cross-site requests are not allowed.');
        const body = req.method === 'POST' || req.method === 'PUT' ? await readBody(req) : {};
        const user = r.open ? null : authenticate(req);
        const result = r.handler({ req, res, body, user, p: m.groups || {}, query: url.searchParams });
        if (!r.raw) send(res, 200, result);
        return;
      }
      fail(404, 'Not found.');
    } catch (e) {
      if (res.headersSent) return res.end();
      if (!(e instanceof HttpError) && !e.status) console.error('Unhandled error on', req.method, url.pathname, '-', e.message);
      send(res, e.status || 500, { error: e.status ? e.message : 'Server error.' });
    }
  });
  function send(res, status, payload) { res.writeHead(status, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' }); res.end(JSON.stringify(payload)); }
  function close() {
    closed = true;
    for (const set of listeners.values()) for (const res of set) res.end();
    listeners.clear();
    return new Promise(resolve => { server.closeAllConnections(); server.close(() => { db.close(); resolve(); }); });
  }
  return { server, db, close };
}

module.exports = { createApp };
