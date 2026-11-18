'use strict';
const test = require('node:test'), assert = require('node:assert/strict');
const { start, lesson } = require('./helpers');

test('accounts, invites and roles', async t => {
  const s = await start(); t.after(s.stop);
  const a = await s.owner();
  assert.equal((await s.api.get('/v1/me', a.token)).body.memberships[0].role, 'owner');
  assert.equal((await s.api.get('/v1/me')).status, 401);
  assert.equal((await s.api.post('/v1/auth/register', { username: 'short', password: 'tooshort', orgName: 'Org X' })).status, 400);
  assert.equal((await s.api.post('/v1/auth/register', { username: 'nobody1', password: 'isolated-test-password' })).status, 400, 'needs invite or org');

  const teacher = await s.member(a, 'teacher'), student = await s.member(a, 'student');
  // Teachers may invite students only; students may invite nobody; nobody can mint an owner.
  assert.equal((await s.api.post('/v1/orgs/' + a.org + '/invites', { role: 'teacher' }, teacher.token)).status, 403);
  assert.equal((await s.api.post('/v1/orgs/' + a.org + '/invites', { role: 'student' }, teacher.token)).status, 200);
  assert.equal((await s.api.post('/v1/orgs/' + a.org + '/invites', { role: 'student' }, student.token)).status, 403);
  assert.equal((await s.api.post('/v1/orgs/' + a.org + '/invites', { role: 'owner' }, a.token)).status, 400);

  // An invite works once.
  const inv = (await s.api.post('/v1/orgs/' + a.org + '/invites', { role: 'student' }, a.token)).body.code;
  assert.equal((await s.api.post('/v1/auth/register', { username: 'first.use', password: 'isolated-test-password', invite: inv })).status, 200);
  assert.equal((await s.api.post('/v1/auth/register', { username: 'second.use', password: 'isolated-test-password', invite: inv })).status, 400);
  assert.equal((await s.api.post('/v1/auth/login', { username: 'second.use', password: 'isolated-test-password' })).status, 401, 'failed registration left no account');

  // Wrong password, then logout revokes the token.
  assert.equal((await s.api.post('/v1/auth/login', { username: 'first.use', password: 'wrong-password-here' })).status, 401);
  const login = await s.api.post('/v1/auth/login', { username: 'FIRST.USE', password: 'isolated-test-password' });
  assert.equal(login.status, 200);
  await s.api.post('/v1/auth/logout', {}, login.body.token);
  assert.equal((await s.api.get('/v1/me', login.body.token)).status, 401);
  // Tokens are stored hashed, never in clear.
  assert.equal(s.app.db.prepare('SELECT COUNT(*) AS n FROM auth_sessions WHERE token_hash = ?').get(a.token).n, 0);
});

test('expired invites and login rate limit', async t => {
  let clock = Date.now();
  const s = await start({ now: () => clock }); t.after(s.stop);
  const a = await s.owner();
  const inv = (await s.api.post('/v1/orgs/' + a.org + '/invites', { role: 'student', hours: 1 }, a.token)).body.code;
  clock += 2 * 3600000;
  assert.equal((await s.api.post('/v1/auth/register', { username: 'late.user', password: 'isolated-test-password', invite: inv })).status, 400);
  let last;
  for (let i = 0; i < 9; i++) last = await s.api.post('/v1/auth/login', { username: 'someone', password: 'wrong-password-here' });
  assert.equal(last.status, 429);
  // Session tokens expire.
  clock += 9 * 3600000;
  assert.equal((await s.api.get('/v1/me', a.token)).status, 401);
});

test('lessons: roles, privacy, versions and cross-organization isolation', async t => {
  const s = await start(); t.after(s.stop);
  const a = await s.owner(), teacher = await s.member(a, 'teacher'), student = await s.member(a, 'student'), other = await s.member(a, 'student');
  const b = await s.owner(); // a different organization
  const L = '/v1/orgs/' + a.org + '/lessons';

  // Students cannot publish or draft, even with forged fields.
  assert.equal((await s.api.post(L, { scope: 'published', reviewed: true, document: lesson(), role: 'teacher' }, student.token)).status, 403);
  assert.equal((await s.api.post(L, { scope: 'draft', document: lesson() }, student.token)).status, 403);
  // Publishing needs explicit review.
  assert.equal((await s.api.post(L, { scope: 'published', document: lesson() }, teacher.token)).status, 400);
  assert.equal((await s.api.post(L, { scope: 'published', reviewed: true, document: { title: 'x' } }, teacher.token)).status, 400);

  // Identity and approval are assigned by the server, not taken from the document.
  const forged = { ...lesson(), approval: 'teacher-approved', approvedBy: 'someone', ownerId: 'x', version: 99, classType: 'teacher' };
  const study = (await s.api.post(L, { scope: 'study', document: forged }, student.token)).body.document;
  assert.equal(study.approval, 'unreviewed'); assert.equal(study.version, 1); assert.equal(study.ownerId, student.id); assert.equal(study.approvedBy, ''); assert.equal(study.classType, 'source');

  // A private study lesson is invisible to everyone else, including the organization owner.
  for (const who of [other, teacher, a]) {
    assert.equal((await s.api.get('/v1/lessons/' + study.id, who.token)).status, 404);
    assert.ok(!(await s.api.get(L + '?all=1', who.token)).body.items.some(i => i.id === study.id));
    assert.equal((await s.api.get('/v1/lessons/' + study.id + '/history', who.token)).status, 404);
    assert.equal((await s.api.get('/v1/orgs/' + a.org + '/families/' + study.familyId + '/build', who.token)).status, 404);
  }

  // Publish v1, revise to v2; v1 stays unchanged; a stale parent conflicts.
  const v1 = (await s.api.post(L, { scope: 'published', reviewed: true, document: lesson() }, teacher.token)).body.document;
  assert.equal(v1.approval, 'teacher-approved'); assert.equal(v1.approvedBy.startsWith('teacher'), true);
  const draft = (await s.api.post(L, { scope: 'draft', parentId: v1.id, document: lesson('Circuits revised') }, teacher.token)).body.document;
  assert.equal(draft.version, 2); assert.equal(draft.familyId, v1.familyId);
  assert.equal((await s.api.post(L, { scope: 'draft', parentId: v1.id, document: lesson() }, teacher.token)).status, 409);
  assert.equal((await s.api.post(L, { scope: 'study', parentId: v1.id, document: lesson() }, student.token)).status, 403, 'only the owner revises');
  assert.equal((await s.api.get('/v1/lessons/' + v1.id, student.token)).body.title, 'Circuits');
  // The draft is hidden from students; the default list shows one row per family for them.
  assert.equal((await s.api.get('/v1/lessons/' + draft.id, student.token)).status, 404);
  assert.deepEqual((await s.api.get(L, other.token)).body.items.map(i => i.id), [v1.id]);
  assert.deepEqual((await s.api.get(L, teacher.token)).body.items.map(i => i.id).sort(), [v1.id, draft.id].sort());
  const v3 = (await s.api.post(L, { scope: 'published', reviewed: true, parentId: draft.id, document: lesson('Circuits revised') }, teacher.token)).body.document;
  assert.deepEqual((await s.api.get(L, other.token)).body.items.map(i => i.id), [v3.id], 'latest published version only');
  // Build log: students see published steps only; the author sees the draft step too.
  assert.deepEqual((await s.api.get('/v1/orgs/' + a.org + '/families/' + v1.familyId + '/build', student.token)).body.items.map(e => e.type), ['published', 'revision_published']);
  assert.deepEqual((await s.api.get('/v1/orgs/' + a.org + '/families/' + v1.familyId + '/build', teacher.token)).body.items.map(e => e.type), ['published', 'draft_saved', 'revision_published']);

  // Another organization can reach nothing here, and gets 404 rather than 403.
  for (const r of [L, '/v1/orgs/' + a.org + '/members', '/v1/lessons/' + v1.id, '/v1/lessons/' + v1.id + '/history', '/v1/lessons/' + v1.id + '/progress', '/v1/orgs/' + a.org + '/sessions', '/v1/orgs/' + a.org + '/insights?lesson=' + v1.id, '/v1/orgs/' + a.org + '/families/' + v1.familyId + '/build'])
    assert.equal((await s.api.get(r, b.token)).status, 404, r);
  assert.equal((await s.api.post(L, { scope: 'study', document: lesson() }, b.token)).status, 404);
  assert.equal((await s.api.post('/v1/orgs/' + a.org + '/invites', { role: 'student' }, b.token)).status, 404);
  assert.equal((await s.api.post('/v1/orgs/' + a.org + '/sessions', { lessonId: v1.id }, b.token)).status, 404);
  assert.equal((await s.api.post('/v1/orgs/' + a.org + '/jobs', { kind: 'lesson', topic: 'Circuits', level: 'Beginner', minutes: 10, sources: ['x'] }, b.token)).status, 404);
  // A lesson from organization A cannot be used to start a session in organization B.
  const both = await s.api.post('/v1/orgs', { name: 'Second org' }, teacher.token);
  assert.equal((await s.api.post('/v1/orgs/' + both.body.orgId + '/sessions', { lessonId: v1.id }, teacher.token)).status, 404);
});

test('sign-in survives a restart; cross-site and malformed requests are refused', async t => {
  const first = await start();
  const a = await first.owner();
  const saved = (await first.api.post('/v1/orgs/' + a.org + '/lessons', { scope: 'published', reviewed: true, document: lesson() }, a.token)).body.id;
  await first.app.close();
  const second = await start({ dir: first.dir });
  t.after(async () => { await second.app.close(); require('node:fs').rmSync(first.dir, { recursive: true, force: true }); });
  assert.equal((await second.api.get('/v1/me', a.token)).status, 200);
  assert.equal((await second.api.get('/v1/lessons/' + saved, a.token)).body.id, saved);
  assert.equal((await second.api.post('/v1/orgs', { name: 'Evil org' }, a.token, { Origin: 'https://evil.example' })).status, 403);
  const raw = await fetch(second.base + '/v1/orgs', { method: 'POST', headers: { Authorization: 'Bearer ' + a.token, 'Content-Type': 'text/plain' }, body: '{}' });
  assert.equal(raw.status, 415);
  const page = await fetch(second.base + '/watch'); assert.equal(page.status, 200); assert.match(await page.text(), /Clarenza/);
});
