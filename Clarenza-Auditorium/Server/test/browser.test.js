'use strict';
const test = require('node:test'), assert = require('node:assert/strict');
const { start, lesson } = require('./helpers');
const { offlineGenerator } = require('../generator');
const { browser } = require('./browser-driver');
const password = 'isolated-test-password';
const { setTimeout: delay } = require('node:timers/promises');
const fs = require('node:fs'), path = require('node:path');

test('real browser: teacher/student journey, privacy, resume and recovery', { skip: !process.env.CLARENZA_BROWSER, timeout: 120000 }, async t => {
  // Only the explicitly named fixture gets a quiz. All other generation uses the real offline path.
  let releaseGeneration;
  const s = await start({ generator: async input => input.topic === 'Delayed browser fixture'
    ? new Promise(resolve => { releaseGeneration = () => resolve({ document: lesson(input.topic), usage: { provider: 'test' } }); })
    : input.topic === 'Browser quiz fixture'
    ? { document: { ...lesson(input.topic), mode: 'TEST FIXTURE — not live AI' }, usage: { provider: 'test' } }
    : offlineGenerator(input) });
  t.after(s.stop);
  const b = await browser(process.env.CLARENZA_BROWSER); t.after(b.close);
  const teacher = await b.page(s.base + '/teacher'), student = await b.page(s.base + '/student');
  async function signIn(page, username) { await page.fill('#username', username); await page.fill('#password', password); await page.click('#login'); await page.wait(`document.getElementById('who').textContent === ${JSON.stringify(username)}`); }
  await teacher.fill('#username', 'browser.owner'); await teacher.fill('#password', password); await teacher.fill('#orgName', 'Browser Test School'); await teacher.click('#create');
  await teacher.wait('!document.getElementById("desk").hidden');
  await teacher.click('#inviteStudent'); await teacher.wait('document.getElementById("inviteCode").textContent.length > 0');
  const invite = await teacher.eval('document.getElementById("inviteCode").textContent');
  await student.fill('#username', 'browser.student'); await student.fill('#password', password); await student.fill('#invite', invite); await student.click('#register');
  await student.wait('!document.getElementById("browse").hidden');
  const owner = await teacher.eval('({token, id:me.id, org:org()})');
  const learner = await student.eval('({token,id:me.id})');
  const anotherOwner = await s.owner('Other Test School');

  await t.test('offline build, saved draft reopens, explicit approval and versioned publication', async () => {
    await teacher.fill('#lessonTitle', 'Offline browser lesson'); await teacher.fill('#lessonText', 'A closed switch completes a circuit.\n\nAn open switch breaks the circuit.'); await teacher.click('#generate');
    await teacher.wait('!document.getElementById("draftBox").hidden');
    assert.match(await teacher.eval('document.getElementById("draftMode").textContent'), /not AI generated/);
    await teacher.click('#saveDraft'); await teacher.wait('document.getElementById("draftBox").hidden');
    await student.click('#refresh'); assert.match(await student.eval('document.getElementById("lessons").textContent'), /No published/);
    await teacher.button('#lessons', 'Review draft'); await teacher.wait('!document.getElementById("draftBox").hidden');
    await teacher.click('#publish'); await teacher.wait('document.getElementById("status").textContent.includes("review")');
    await teacher.click('#reviewed'); await teacher.click('#publish'); await teacher.wait('document.getElementById("status").textContent.includes("Published")');
    const docs = (await s.api.get('/v1/orgs/' + owner.org + '/lessons?all=1', owner.token)).body.items;
    assert.equal(docs.length, 2); assert.equal(docs[0].familyId, docs[1].familyId); assert.equal(docs.find(d => d.scope === 'published').version, 2);
  });
  if (t.signal.aborted) return;
  // Create the source-backed quiz fixture through the same page and worker, not via DOM injection.
  await teacher.fill('#lessonTitle', 'Browser quiz fixture'); await teacher.click('#generate');
  await teacher.wait('!document.getElementById("draftBox").hidden && document.getElementById("draftTitle").textContent === "Browser quiz fixture"');
  await teacher.click('#reviewed'); await teacher.click('#publish'); await teacher.wait('document.getElementById("draftBox").hidden');
  const published = (await s.api.get('/v1/orgs/' + owner.org + '/lessons', owner.token)).body.items.find(d => d.title === 'Browser quiz fixture');
  // Find the row by its text; all activity below is actual DOM controls and HTTP/SSE.
  await teacher.button('#lessons', 'Start a live class', 'Browser quiz fixture'); await teacher.wait('!document.getElementById("livePanel").hidden');
  await student.click('#refresh'); await student.button('#live', 'Join live'); await student.wait('document.getElementById("roomState").textContent.includes("Live")');
  const sessionId = await teacher.eval('live.id');

  await t.test('host play/pause/section drive student SSE, private and shared questions, answers', async () => {
    await teacher.click('[data-act="play"]'); await student.wait('document.getElementById("roomState").textContent.includes("playing")');
    await teacher.click('[data-act="next"]'); await student.wait('document.getElementById("boardTitle").textContent === "Open"');
    await teacher.click('[data-act="pause"]'); await student.wait('document.getElementById("roomState").textContent.includes("paused")');
    await student.fill('#question', 'Private browser question'); await student.click('#ask'); await student.wait('document.getElementById("status").textContent.includes("privately")');
    assert.ok(!(await teacher.eval('document.body.innerText')).includes('Private browser question'));
    assert.equal((await s.api.get('/v1/sessions/' + sessionId + '/questions', owner.token)).body.items.length, 0);
    await student.fill('#question', 'Shared browser question'); await student.click('#shared'); await student.click('#ask');
    await teacher.wait('document.getElementById("liveQuestions").textContent.includes("Shared browser question")');
    await teacher.fill('#liveQuestions input', 'An open switch breaks the path.'); await teacher.button('#liveQuestions', 'Answer');
    await student.wait('document.getElementById("log").textContent.includes("answered: An open switch")');
    assert.equal((await s.api.post('/v1/orgs/' + owner.org + '/lessons', {scope:'published',reviewed:true,document:lesson()}, learner.token)).status,403);
    assert.equal((await s.api.get('/v1/orgs/' + owner.org + '/insights?lesson=' + published.id, learner.token)).status,403);
  });

  await t.test('leaving/rejoining restores questions and resets explicit sharing consent', async () => {
    await student.fill('#question', 'Unsent private text'); await student.click('#leave'); await student.button('#live', 'Join live');
    await student.wait('document.getElementById("roomState").textContent.includes("Live")');
    assert.equal(await student.eval('document.getElementById("shared").checked'), false);
    assert.equal(await student.eval('document.getElementById("question").value'), '');
    await student.wait('document.getElementById("log").textContent.includes("Private browser question")');
  });

  await t.test('end, replay, speed, seeking, self-paced quiz, resume and insights', async () => {
    await delay(1200); // Give this real recording a visible duration on the mm:ss clock.
    await teacher.click('[data-act="end"]'); await student.wait('document.getElementById("roomState").textContent === "Ended"');
    await student.click('#leave'); await student.button('#ended', 'Watch');
    await student.wait('!document.getElementById("replayControls").hidden'); await student.fill('#rpSpeed', '2'); await student.click('#rpToggle');
    await student.wait('document.getElementById("rpClock").textContent.slice(0,5) !== "00:00"');
    if (await student.eval('document.getElementById("rpToggle").textContent === "Pause"')) await student.click('#rpToggle');
    await student.fill('#rpSection', '1'); await student.wait('document.getElementById("boardTitle").textContent === "Open"');
    assert.ok(!(await student.eval('document.getElementById("log").textContent')).includes('Private browser question'));
    await student.click('#leave');
    await student.button('#lessons', 'Study at my own pace', 'Browser quiz fixture'); await student.wait('!document.getElementById("selfControls").hidden');
    await student.click('#selfNext');
    // Fail one attempt transport, then retry using the actual answer buttons.
    await student.eval(`window.realFetch=window.fetch; window.failAttempt=true; window.fetch=(url,options)=>{if(window.failAttempt && String(url).endsWith('/attempts')){window.failAttempt=false;return Promise.reject(new TypeError('Simulated offline request'));} return window.realFetch(url,options);}`);
    await student.button('#popts', 'Completes the circuit'); await student.wait('document.getElementById("status").textContent.includes("Simulated offline")');
    assert.equal(await student.eval('[...document.getElementById("popts").children].every(b=>!b.disabled)'), true);
    await student.button('#popts', 'Completes the circuit'); await student.wait('!document.getElementById("pnext").hidden'); await student.click('#pnext');
    await student.eval('window.fetch=window.realFetch');
    await student.wait('document.getElementById("pq").textContent.includes("Practice complete")'); await student.click('#leave');
    await student.button('#lessons', 'Study at my own pace', 'Browser quiz fixture'); await student.wait('!document.getElementById("selfControls").hidden');
    assert.equal(await student.eval('document.getElementById("boardTitle").textContent'), 'Open');
    assert.match(await student.eval('document.getElementById("pq").textContent'), /Practice complete/);
    await teacher.button('#lessons', 'Insights', 'Browser quiz fixture'); await teacher.wait('!document.getElementById("insightPanel").hidden');
    assert.match(await teacher.eval('document.getElementById("insightCounts").textContent'), /1 learner\(s\) started, 1 completed/);
    assert.match(await teacher.eval('document.getElementById("insightQuiz").textContent'), /100%/);
    assert.ok(!(await teacher.eval('document.getElementById("insightQuestions").textContent')).includes('Private browser question'));
  });

  await t.test('desktop, tablet and phone layouts have no page overflow', async () => {
    for (const [name, page] of [['teacher', teacher], ['student', student]]) {
      await page.send('Page.bringToFront');
      for (const width of [375, 768, 1440]) {
        await page.send('Emulation.setDeviceMetricsOverride', { width, height: 900, deviceScaleFactor: 1, mobile: false });
        await page.eval('window.scrollTo(0,0)');
        const overflow = await page.eval('document.documentElement.scrollWidth - document.documentElement.clientWidth');
        if (process.env.CLARENZA_TEST_ARTIFACTS) {
          const shot = await page.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
          fs.writeFileSync(path.join(process.env.CLARENZA_TEST_ARTIFACTS, 'stage1-' + name + '-' + width + '.png'), Buffer.from(shot.data, 'base64'));
        }
        assert.ok(overflow <= 1, name + ' overflows by ' + overflow + 'px at ' + width);
      }
    }
  });

  await t.test('sign-out clears authoring and insight data before another account signs in', async () => {
    await teacher.fill('#lessonTitle', 'PRIVATE AUTHORING'); await teacher.fill('#lessonText', 'PRIVATE SOURCE TEXT'); await teacher.click('#generate'); await teacher.wait('!document.getElementById("draftBox").hidden');
    await teacher.click('#signout');
    // Test helper created owner2 (owner() increments once for name and once for username only when no name is supplied).
    const account = (await s.api.get('/v1/me', anotherOwner.token)).body;
    await signIn(teacher, account.username); await teacher.wait('!document.getElementById("desk").hidden');
    assert.equal(await teacher.eval('document.getElementById("draftBox").hidden'), true);
    assert.equal(await teacher.eval('document.getElementById("lessonText").value'), '');
    assert.equal(await teacher.eval('document.getElementById("insightPanel").hidden'), true);
    assert.ok(!(await teacher.eval('document.body.innerText')).includes('PRIVATE AUTHORING'));
  });
  await t.test('a pending generation cannot restore a draft after account switch', async () => {
    await teacher.fill('#lessonTitle', 'Delayed browser fixture'); await teacher.fill('#lessonText', 'A closed switch completes a circuit.'); await teacher.click('#generate');
    await teacher.wait('document.getElementById("status").textContent.includes("Building")');
    await teacher.click('#signout'); await signIn(teacher, 'browser.owner');
    assert.ok(releaseGeneration, 'delayed generator started'); releaseGeneration();
    await delay(1200); // Polling was in flight; the stale completion must be discarded.
    assert.equal(await teacher.eval('document.getElementById("draftBox").hidden'), true);
    assert.equal(await teacher.eval('draft'), null);
  });
  await t.test('failed rebuild clears the previous draft and can be retried', async () => {
    await teacher.fill('#lessonTitle', 'Retry source draft'); await teacher.fill('#lessonText', 'An open switch breaks a circuit.'); await teacher.click('#generate');
    await teacher.wait('!document.getElementById("draftBox").hidden');
    await teacher.fill('#lessonText', ''); await teacher.click('#generate');
    await teacher.wait('document.getElementById("status").textContent.includes("source passages")');
    assert.equal(await teacher.eval('document.getElementById("draftBox").hidden'), true);
    assert.equal(await teacher.eval('draft'), null);
    assert.equal(await teacher.eval('document.getElementById("generate").disabled'), false);
    await teacher.fill('#lessonText', 'A closed switch completes the circuit.'); await teacher.click('#generate');
    await teacher.wait('!document.getElementById("draftBox").hidden');
  });
  assert.deepEqual(teacher.errors, []); assert.deepEqual(student.errors, []);
  assert.equal(teacher.requests.length, 1, 'only the deliberately invalid build should fail');
  assert.equal(teacher.requests[0].status, 400); assert.ok(teacher.requests[0].url.endsWith('/jobs'));
  assert.deepEqual(student.requests, []);
});
