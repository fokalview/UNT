'use strict';
// Entry point: node Server/server.js
// Environment: PORT (default 8787), HOST (default 127.0.0.1), CLARENZA_DB (default ./clarenza.db),
// OPENAI_API_KEY + OPENAI_MODEL (optional; without them lesson jobs use the offline source layout).
const path = require('node:path');
const { createApp } = require('./app');
const { offlineGenerator, openAiGenerator } = require('./generator');

const useAi = process.env.OPENAI_API_KEY && process.env.OPENAI_MODEL;
const app = createApp({
  dbFile: process.env.CLARENZA_DB || path.join(__dirname, 'clarenza.db'),
  generator: useAi ? openAiGenerator({ apiKey: process.env.OPENAI_API_KEY, model: process.env.OPENAI_MODEL }) : offlineGenerator,
});
const port = Number(process.env.PORT) || 8787, host = process.env.HOST || '127.0.0.1';
app.server.listen(port, host, () => console.log('Clarenza hosted service on http://' + host + ':' + port + ' — AI ' + (useAi ? 'configured' : 'not configured') + '. Open /watch in a browser.'));
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => app.close().then(() => process.exit(0)));
