'use strict';
// Dependency-free Chromium driver. Uses a separately launched, temporary browser profile.
const fs = require('node:fs'), os = require('node:os'), path = require('node:path');
const { spawn } = require('node:child_process');
const { setTimeout: delay } = require('node:timers/promises');

async function browser(executable) {
  const profile = fs.mkdtempSync(path.join(process.env.CLARENZA_TEST_ARTIFACTS || os.tmpdir(), 'clarenza-browser-'));
  const child = spawn(executable, ['--headless=new', '--remote-debugging-port=0', '--remote-debugging-address=127.0.0.1', '--user-data-dir=' + profile, '--no-first-run', '--no-default-browser-check', 'about:blank'], { windowsHide: true, stdio: 'ignore' });
  let launchError; child.on('error', e => { launchError = e; });
  const portFile = path.join(profile, 'DevToolsActivePort');
  for (let i = 0; !fs.existsSync(portFile); i++) {
    if (launchError) throw launchError;
    if (i >= 150 || child.exitCode !== null) throw new Error('Chromium did not start: ' + executable);
    await delay(100);
  }
  const port = fs.readFileSync(portFile, 'utf8').split('\n')[0], sockets = [];
  async function page(url) {
    const target = await (await fetch('http://127.0.0.1:' + port + '/json/new?' + encodeURIComponent(url), { method: 'PUT' })).json();
    const ws = new WebSocket(target.webSocketDebuggerUrl); sockets.push(ws);
    await new Promise((resolve, reject) => { ws.addEventListener('open', resolve, { once: true }); ws.addEventListener('error', reject, { once: true }); });
    let next = 0; const pending = new Map(), errors = [], requests = [];
    ws.addEventListener('message', event => {
      const m = JSON.parse(event.data);
      if (m.id) { const p = pending.get(m.id); if (!p) return; pending.delete(m.id); clearTimeout(p.timer); m.error ? p.reject(new Error(m.error.message)) : p.resolve(m.result); }
      if (m.method === 'Runtime.exceptionThrown') errors.push(m.params.exceptionDetails.text + ': ' + (m.params.exceptionDetails.exception?.description || ''));
      if (m.method === 'Network.responseReceived' && m.params.response.status >= 400 && !m.params.response.url.endsWith('/favicon.ico')) requests.push({ url: m.params.response.url, status: m.params.response.status });
    });
    function send(method, params = {}) {
      return new Promise((resolve, reject) => {
        const id = ++next, timer = setTimeout(() => { pending.delete(id); reject(new Error('CDP timeout: ' + method)); }, 15000);
        pending.set(id, { resolve, reject, timer }); ws.send(JSON.stringify({ id, method, params }));
      });
    }
    await send('Runtime.enable'); await send('Page.enable'); await send('Network.enable');
    ws.addEventListener('message', event => { if (JSON.parse(event.data).method === 'Page.javascriptDialogOpening') send('Page.handleJavaScriptDialog', { accept: true }).catch(() => {}); });
    async function evalJs(expression) {
      const r = await send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true, userGesture: true });
      if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description || r.exceptionDetails.text);
      return r.result.value;
    }
    async function wait(expression, label = expression) {
      for (let i = 0; i < 150; i++) { if (await evalJs(expression)) return; await delay(50); }
      throw new Error('Browser assertion timed out: ' + label + '\n' + await evalJs('document.body.innerText'));
    }
    await wait('location.href === ' + JSON.stringify(url) + ' && document.readyState === "complete"');
    async function click(selector) {
      await wait(`(() => { const e=document.querySelector(${JSON.stringify(selector)}); return e && !e.disabled && e.getClientRects().length; })()`, 'clickable ' + selector);
      const point = await evalJs(`(() => { const e = document.querySelector(${JSON.stringify(selector)}); if (!e || e.disabled || !e.getClientRects().length) throw Error('Not clickable: ' + ${JSON.stringify(selector)}); e.scrollIntoView({block:'center'}); const r = e.getBoundingClientRect(); return {x:r.x+r.width/2,y:r.y+r.height/2}; })()`);
      await send('Input.dispatchMouseEvent', { type: 'mousePressed', button: 'left', clickCount: 1, ...point });
      await send('Input.dispatchMouseEvent', { type: 'mouseReleased', button: 'left', clickCount: 1, ...point });
    }
    async function fill(selector, value) {
      await evalJs(`(() => { const e = document.querySelector(${JSON.stringify(selector)}); e.focus(); e.value = ${JSON.stringify(value)}; e.dispatchEvent(new Event('input',{bubbles:true})); e.dispatchEvent(new Event('change',{bubbles:true})); })()`);
    }
    async function button(list, text, rowText = '') {
      const find = `[...document.querySelectorAll(${JSON.stringify(list + ' button')})].find(e => !e.disabled && e.getClientRects().length && e.textContent === ${JSON.stringify(text)} && (!${JSON.stringify(rowText)} || e.closest('li')?.textContent.includes(${JSON.stringify(rowText)})))`;
      await wait(`!!(${find})`, 'button ' + text);
      const point = await evalJs(`(() => { const b = ${find}; if (!b) throw Error('Missing button: ' + ${JSON.stringify(text)}); b.scrollIntoView({block:'center'}); const r=b.getBoundingClientRect(); return {x:r.x+r.width/2,y:r.y+r.height/2}; })()`);
      await send('Input.dispatchMouseEvent', { type: 'mousePressed', button: 'left', clickCount: 1, ...point });
      await send('Input.dispatchMouseEvent', { type: 'mouseReleased', button: 'left', clickCount: 1, ...point });
    }
    return { send, eval: evalJs, wait, click, fill, button, errors, requests };
  }
  return { page, profile, close: async () => { for (const ws of sockets) ws.close(); if (child.exitCode === null) { const exited = new Promise(resolve => child.once('exit', resolve)); child.kill(); await exited; } } };
}
module.exports = { browser };
