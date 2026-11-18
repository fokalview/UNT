'use strict';
// Lesson validation. Same rules as Service/accounts.js lessonValid, so documents move between
// the local pilot and the hosted service unchanged.

function bad(message) { const e = new Error(message); e.status = 400; throw e; }
const isIds = (ids, count) => Array.isArray(ids) && ids.every(i => Number.isInteger(i) && i >= 1 && i <= count);

function validateLesson(l) {
  if (!l || typeof l !== 'object' || typeof l.title !== 'string' || !l.title.trim() || l.title.length > 200) bad('Invalid lesson.');
  if (!Array.isArray(l.sources) || l.sources.length < 1 || l.sources.length > 100 || l.sources.some(s => typeof s !== 'string')) bad('Invalid lesson.');
  if (!Array.isArray(l.sections) || !l.sections.length || l.sections.length > 100) bad('Invalid lesson.');
  for (const s of l.sections) {
    if (!s || typeof s.title !== 'string' || typeof s.narration !== 'string' || typeof s.board !== 'string' || !isIds(s.sourceIds, l.sources.length)) bad('Invalid lesson section.');
  }
  for (const q of l.quiz || []) {
    if (!q || typeof q.question !== 'string' || typeof q.explanation !== 'string' ||
        !Array.isArray(q.options) || q.options.length !== 3 || q.options.some(o => typeof o !== 'string') ||
        !Number.isInteger(q.correct) || q.correct < 0 || q.correct > 2 ||
        !isIds(q.sourceIds, l.sources.length) || !q.sourceIds.length) bad('Invalid quiz.');
  }
  if (JSON.stringify(l).length > 200000) bad('Lesson too large.');
  return l;
}

// Only content fields are stored from a client document. Identity and approval are server-owned.
function contentOf(l) {
  return {
    title: l.title.trim(), subject: typeof l.subject === 'string' ? l.subject : '',
    mode: typeof l.mode === 'string' ? l.mode : '', sourceOrigin: typeof l.sourceOrigin === 'string' ? l.sourceOrigin : '',
    sources: l.sources,
    sections: l.sections.map(s => ({ title: s.title, narration: s.narration, board: s.board, gesture: typeof s.gesture === 'string' ? s.gesture : 'explain', sourceIds: s.sourceIds })),
    quiz: (l.quiz || []).map(q => ({ question: q.question, options: q.options, correct: q.correct, explanation: q.explanation, sourceIds: q.sourceIds })),
  };
}

module.exports = { validateLesson, contentOf };
