'use strict';
const test = require('node:test'), assert = require('node:assert/strict');
const { start, lesson, readEvents } = require('./helpers');

async function classroom(s) {
  const a = await s.owner(), teacher = await s.member(a, 'teacher'), student = await s.member(a, 'student'), other = await s.member(a, 'student');
  const published = (await s.api.post('/v1/orgs/' + a.org + '/lessons', { scope: 'published', reviewed: true, document: lesson() }, teacher.token)).body.document;
  return { a, teacher, student, other, published };
}
const types = events => events.filter(e => e.type !== 'heartbeat' && e.type !== 'snapshot').map(e => e.type);

test('live session: host-only control, ordered events, resume, recording', async t => {
  const s = await start(); t.after(s.stop);
  const { a, teacher, student, other, published } = await classroom(s);
  const S = '/v1/orgs/' + a.org + '/sessions';

  // A student cannot open a class to the organization.
  assert.equal((await s.api.post(S, { lessonId: published.id }, student.token)).status, 403);
  const session = (await s.api.post(S, { lessonId: published.id }, teacher.token)).body;
  assert.equal(session.state, 'live'); assert.equal(session.joinCode, undefined);
  assert.deepEqual((await s.api.get(S + '?state=live', student.token)).body.items.map(x => x.id), [session.id]);

  // The student follows the stream while the host drives the class.
  const following = readEvents(s.base, '/v1/sessions/' + session.id + '/events', student.token, ev => ev.some(e => e.type === 'ended'));
  await new Promise(r => setTimeout(r, 100));
  assert.equal((await s.api.post('/v1/sessions/' + session.id + '/join', {}, student.token)).status, 200);
  const C = '/v1/sessions/' + session.id + '/control';
  for (const body of [{ action: 'play' }, { action: 'goto', section: 1 }, { action: 'pause' }]) assert.equal((await s.api.post(C, body, student.token)).status, 403, 'students do not control');
  assert.equal((await s.api.post(C, { action: 'goto', section: 9 }, teacher.token)).status, 400);
  assert.equal((await s.api.post(C, { action: 'fly' }, teacher.token)).status, 400);
  await s.api.post(C, { action: 'play' }, teacher.token);
  await s.api.post(C, { action: 'play' }, teacher.token); // repeated play is not a new event
  assert.equal((await s.api.post(C, { action: 'goto', section: 1 }, teacher.token)).body.section, 1);

  // Shared questions reach the class; private ones never do.
  const shared = (await s.api.post('/v1/sessions/' + session.id + '/questions', { text: 'Why does the lamp go out?', shared: true }, student.token)).body;
  await s.api.post('/v1/sessions/' + session.id + '/questions', { text: 'my private confusion', shared: false }, student.token);
  assert.equal((await s.api.post('/v1/sessions/' + session.id + '/questions', { text: 'not joined', shared: true }, other.token)).status, 403);
  assert.equal((await s.api.post('/v1/questions/' + shared.id + '/answer', { answer: 'x' }, other.token)).status, 403);
  await s.api.post('/v1/questions/' + shared.id + '/answer', { answer: 'The path is broken.' }, teacher.token);
  await new Promise(r => setTimeout(r, 450)); // long enough for a heartbeat
  await s.api.post(C, { action: 'pause' }, teacher.token);
  await s.api.post(C, { action: 'end' }, teacher.token);

  const live = await following;
  assert.equal(live.events[0].type, 'snapshot');
  assert.ok(live.events.some(e => e.type === 'heartbeat'), 'heartbeat sent');
  const expected = ['started', 'joined', 'play', 'goto', 'question', 'answer', 'pause', 'ended'];
  assert.deepEqual(types(live.events), expected);
  const seqs = live.events.filter(e => e.data && e.data.seq).map(e => e.data.seq);
  assert.deepEqual(seqs, seqs.map((_, i) => i + 1), 'gap-free and ordered');
  assert.ok(!JSON.stringify(live.events).includes('my private confusion'));

  // Resume: a client that saw up to seq 3 gets exactly the rest, no duplicates.
  const resumed = await readEvents(s.base, '/v1/sessions/' + session.id + '/events', student.token, () => false, 3);
  assert.deepEqual(types(resumed.events), expected.slice(3));

  // The recording equals the live sequence, and a learner who never joined can replay it.
  const rec = (await s.api.get('/v1/sessions/' + session.id + '/recording', other.token)).body;
  assert.deepEqual(rec.events.map(e => e.type), expected);
  assert.equal(rec.lesson.id, published.id); assert.equal(rec.lesson.sections.length, 2);
  assert.ok(rec.events.every((e, i) => i === 0 || e.at >= rec.events[i - 1].at));
  assert.ok(!JSON.stringify(rec).includes('my private confusion'));

  // An ended class is frozen.
  assert.equal((await s.api.post(C, { action: 'play' }, teacher.token)).status, 409);
  assert.equal((await s.api.post('/v1/sessions/' + session.id + '/join', {}, other.token)).status, 409);
  assert.deepEqual((await s.api.get(S + '?state=ended', other.token)).body.items.map(x => x.id), [session.id]);
  assert.deepEqual((await s.api.get(S + '?state=live', other.token)).body.items, []);

  // Private questions are visible to their author only.
  assert.equal((await s.api.get('/v1/sessions/' + session.id + '/questions', student.token)).body.items.length, 2);
  assert.deepEqual((await s.api.get('/v1/sessions/' + session.id + '/questions', teacher.token)).body.items.map(q => q.text), ['Why does the lamp go out?']);

  // The session pinned its lesson version: publishing v2 does not change the recording.
  await s.api.post('/v1/orgs/' + a.org + '/lessons', { scope: 'published', reviewed: true, parentId: published.id, document: lesson('Circuits v2') }, teacher.token);
  assert.equal((await s.api.get('/v1/sessions/' + session.id + '/recording', student.token)).body.lesson.title, 'Circuits');
});

test('invite-only study party stays private', async t => {
  const s = await start(); t.after(s.stop);
  const { a, teacher, student, other } = await classroom(s);
  const mine = (await s.api.post('/v1/orgs/' + a.org + '/lessons', { scope: 'study', document: lesson('My notes') }, student.token)).body.document;
  const S = '/v1/orgs/' + a.org + '/sessions';
  // A private lesson cannot be opened to the whole organization, even by its owner.
  assert.equal((await s.api.post(S, { lessonId: mine.id, visibility: 'org' }, student.token)).status, 403);
  const party = (await s.api.post(S, { lessonId: mine.id, visibility: 'invite' }, student.token)).body;
  assert.ok(party.joinCode.length >= 8);
  // Without the code it does not exist for others, the teacher and owner included.
  for (const who of [other, teacher, a]) {
    assert.deepEqual((await s.api.get(S + '?state=live', who.token)).body.items, []);
    assert.equal((await s.api.post('/v1/sessions/' + party.id + '/join', { code: 'wrong-code' }, who.token)).status, 404);
    assert.equal((await s.api.get('/v1/sessions/' + party.id + '/recording', who.token)).status, 404);
    assert.equal((await readEvents(s.base, '/v1/sessions/' + party.id + '/events', who.token, () => true)).status, 404);
  }
  assert.equal((await s.api.post('/v1/sessions/' + party.id + '/join', { code: party.joinCode }, other.token)).status, 200);
  assert.equal((await s.api.get('/v1/sessions/' + party.id + '/recording', other.token)).body.lesson.title, 'My notes');
  assert.equal((await s.api.post('/v1/sessions/' + party.id + '/leave', {}, other.token)).status, 200);
  // An invite code must not bypass the rule hiding a learner's private study from staff.
  for (const staff of [teacher, a]) {
    assert.equal((await s.api.post('/v1/sessions/' + party.id + '/join', { code: party.joinCode }, staff.token)).status, 404);
    assert.equal((await s.api.get('/v1/sessions/' + party.id + '/recording', staff.token)).status, 404);
  }
  // A previous student participant promoted to teacher also loses access to the private lesson.
  s.app.db.prepare('UPDATE memberships SET role = ? WHERE org_id = ? AND user_id = ?').run('teacher', a.org, other.id);
  assert.deepEqual((await s.api.get(S + '?state=live', other.token)).body.items, []);
  assert.equal((await s.api.get('/v1/sessions/' + party.id + '/recording', other.token)).status, 404);
  assert.equal((await readEvents(s.base, '/v1/sessions/' + party.id + '/events', other.token, () => true)).status, 404);
  const shared = (await s.api.post('/v1/sessions/' + party.id + '/questions', { text: 'Private study discussion', shared: true }, student.token)).body;
  assert.equal((await s.api.get('/v1/sessions/' + party.id + '/questions', other.token)).status, 404);
  assert.equal((await s.api.post('/v1/questions/' + shared.id + '/answer', { answer: 'Staff reply' }, other.token)).status, 404);
});

test('progress, marked attempts and teacher insights', async t => {
  const s = await start(); t.after(s.stop);
  const { a, teacher, student, other, published } = await classroom(s);
  const P = '/v1/lessons/' + published.id;
  assert.equal((await s.api.get(P + '/progress', student.token)).body.section, 0);
  assert.equal((await s.api.put(P + '/progress', { section: 5 }, student.token)).status, 400);
  await s.api.put(P + '/progress', { section: 1, quizIndex: 1, score: 1, clock: 24, completed: true }, student.token);
  await s.api.put(P + '/progress', { section: 0, clock: 3 }, other.token);
  assert.deepEqual((({ section, clock, completed }) => ({ section, clock, completed }))((await s.api.get(P + '/progress', student.token)).body), { section: 1, clock: 24, completed: true });
  assert.equal((await s.api.get(P + '/progress', other.token)).body.clock, 3, 'progress is per learner');

  // The server marks answers; only the first attempt counts.
  assert.equal((await s.api.post(P + '/attempts', { question: 0, selected: 0 }, student.token)).body.correct, false);
  assert.equal((await s.api.post(P + '/attempts', { question: 0, selected: 1 }, student.token)).body.correct, true);
  assert.equal((await s.api.post(P + '/attempts', { question: 0, selected: 1 }, other.token)).body.correct, true);
  assert.equal((await s.api.post(P + '/attempts', { question: 3, selected: 1 }, other.token)).status, 400);

  const session = (await s.api.post('/v1/orgs/' + a.org + '/sessions', { lessonId: published.id }, teacher.token)).body;
  await s.api.post('/v1/sessions/' + session.id + '/join', {}, student.token);
  await s.api.post('/v1/sessions/' + session.id + '/questions', { text: 'shared question', shared: true }, student.token);
  await s.api.post('/v1/sessions/' + session.id + '/questions', { text: 'secret question', shared: false }, student.token);

  const I = '/v1/orgs/' + a.org + '/insights?lesson=' + published.id;
  assert.equal((await s.api.get(I, student.token)).status, 403);
  const view = (await s.api.get(I, teacher.token)).body;
  assert.equal(view.started, 2); assert.equal(view.completed, 1);
  assert.deepEqual(view.questions.map(q => [q.attempts, q.misses, q.missRate]), [[2, 1, 0.5]]);
  assert.deepEqual(view.sharedQuestions.map(q => q.text), ['shared question']);
  assert.ok(!JSON.stringify(view).includes('secret question'));
});

test('generation jobs: queue, validation, quota, privacy', async t => {
  let calls = 0;
  const generator = async input => { calls++; if (input.topic === 'Broken') return { document: { title: 'Broken', sources: input.sources, sections: [{ title: 'x', narration: 'y', board: 'z', sourceIds: [7] }] } }; return { document: { ...lesson(input.topic), sources: input.sources }, usage: { provider: 'test', inputTokens: 10, outputTokens: 20 } }; };
  const s = await start({ generator, dailyJobLimit: 3 }); t.after(s.stop);
  const { a, student, other } = await classroom(s);
  const J = '/v1/orgs/' + a.org + '/jobs', input = { kind: 'lesson', topic: 'Switches', level: 'Beginner', minutes: 10, sources: ['A closed switch completes a circuit.', 'An open switch breaks the circuit.'] };
  assert.equal((await s.api.post(J, { ...input, minutes: 90 }, student.token)).status, 400);
  assert.equal((await s.api.post(J, { ...input, sources: [] }, student.token)).status, 400);
  const wait = async id => { for (let i = 0; i < 50; i++) { const j = (await s.api.get('/v1/jobs/' + id, student.token)).body; if (j.state === 'ready' || j.state === 'failed') return j; await new Promise(r => setTimeout(r, 20)); } throw new Error('job did not finish'); };
  const ok = (await s.api.post(J, input, student.token)).body;
  assert.equal(ok.state, 'queued');
  const done = await wait(ok.id);
  assert.equal(done.state, 'ready'); assert.equal(done.document.title, 'Switches'); assert.equal(done.document.classType, 'ai'); assert.equal(done.usage.outputTokens, 20);
  assert.equal((await s.api.get('/v1/jobs/' + ok.id, other.token)).status, 404, 'jobs are private to their requester');
  // Output with an unsupported source reference is rejected, not stored as a lesson.
  const bad = await wait((await s.api.post(J, { ...input, topic: 'Broken' }, student.token)).body.id);
  assert.equal(bad.state, 'failed'); assert.equal(bad.document, null);
  // A generated document is still only a private study lesson until a teacher approves it.
  const saved = (await s.api.post('/v1/orgs/' + a.org + '/lessons', { scope: 'study', document: done.document }, student.token)).body.document;
  assert.equal(saved.approval, 'unreviewed'); assert.equal(saved.classType, 'ai');
  await s.api.post(J, input, student.token);
  assert.equal((await s.api.post(J, input, student.token)).status, 429, 'daily limit per organization');
  assert.equal(calls, 3);
});
