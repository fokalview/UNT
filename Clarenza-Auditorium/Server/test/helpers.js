'use strict';
const fs = require('node:fs'), os = require('node:os'), path = require('node:path');
const { createApp } = require('../app');

// Each test file gets its own temporary database and its own server on a random port.
async function start(options = {}) {
  const dir = options.dir || fs.mkdtempSync(path.join(os.tmpdir(), 'clarenza-test-'));
  const app = createApp({ dbFile: path.join(dir, 'test.db'), heartbeatMs: 200, ...options });
  await new Promise(resolve => app.server.listen(0, '127.0.0.1', resolve));
  const base = 'http://127.0.0.1:' + app.server.address().port;
  async function call(method, route, body, token, headers = {}) {
    const res = await fetch(base + route, { method, headers: { ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}), ...(token ? { Authorization: 'Bearer ' + token } : {}), ...headers }, body: body !== undefined ? JSON.stringify(body) : undefined });
    return { status: res.status, body: await res.json() };
  }
  const api = { get: (r, t) => call('GET', r, undefined, t), post: (r, b, t, h) => call('POST', r, b || {}, t, h), put: (r, b, t) => call('PUT', r, b, t) };
  let n = 0;
  // Creates an owner with a new organization.
  async function owner(orgName = 'Org ' + (++n)) { const r = await api.post('/v1/auth/register', { username: 'owner' + (++n), password: 'isolated-test-password', orgName }); return { token: r.body.token, id: r.body.id, org: r.body.memberships[0].orgId }; }
  // Creates a member of an existing organization through an invite.
  async function member(by, role) { const inv = await api.post('/v1/orgs/' + by.org + '/invites', { role }, by.token); const r = await api.post('/v1/auth/register', { username: role + (++n), password: 'isolated-test-password', invite: inv.body.code }); return { token: r.body.token, id: r.body.id, org: by.org }; }
  return { app, base, api, owner, member, dir, stop: async () => { await app.close(); if (!options.dir) fs.rmSync(dir, { recursive: true, force: true }); } };
}
const lesson = (title = 'Circuits') => ({ title, sources: ['A closed switch completes a circuit.', 'An open switch breaks the circuit.'],
  sections: [{ title: 'Closed', narration: 'A closed switch completes a circuit.', board: 'Closed = complete', sourceIds: [1] }, { title: 'Open', narration: 'An open switch breaks the circuit.', board: 'Open = broken', sourceIds: [2] }],
  quiz: [{ question: 'What does an open switch do?', options: ['Completes the circuit', 'Breaks the circuit', 'Nothing'], correct: 1, explanation: 'An open switch breaks the path.', sourceIds: [2] }] });

// Reads Server-Sent Events from a fetch stream until `until(events)` is true or the stream ends.
async function readEvents(base, route, token, until, lastEventId) {
  const controller = new AbortController();
  const res = await fetch(base + route, { headers: { Authorization: 'Bearer ' + token, ...(lastEventId ? { 'Last-Event-ID': String(lastEventId) } : {}) }, signal: controller.signal });
  const events = [], decoder = new TextDecoder(); let buffer = '';
  try {
    for await (const chunk of res.body) {
      buffer += decoder.decode(chunk, { stream: true });
      let cut;
      while ((cut = buffer.indexOf('\n\n')) >= 0) {
        const block = buffer.slice(0, cut); buffer = buffer.slice(cut + 2);
        if (block.startsWith(':')) { events.push({ type: 'heartbeat' }); }
        else { const type = /^event: (.*)$/m.exec(block)[1], data = JSON.parse(/^data: (.*)$/m.exec(block)[1]); events.push({ type, data }); }
        if (until(events)) { controller.abort(); return { status: res.status, events }; }
      }
    }
  } catch (e) { if (e.name !== 'AbortError') throw e; }
  return { status: res.status, events };
}
module.exports = { start, lesson, readEvents };
