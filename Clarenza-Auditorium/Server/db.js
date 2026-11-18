'use strict';
// SQLite store for the hosted Clarenza service. One file, real transactions.
const { DatabaseSync } = require('node:sqlite');

const SCHEMA = `
CREATE TABLE IF NOT EXISTS users (
  id TEXT PRIMARY KEY, username TEXT NOT NULL, username_key TEXT NOT NULL UNIQUE,
  salt TEXT NOT NULL, hash TEXT NOT NULL, created_at INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS auth_sessions (
  token_hash TEXT PRIMARY KEY, user_id TEXT NOT NULL REFERENCES users(id), expires_at INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS orgs (
  id TEXT PRIMARY KEY, name TEXT NOT NULL, created_by TEXT NOT NULL REFERENCES users(id), created_at INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS memberships (
  org_id TEXT NOT NULL REFERENCES orgs(id), user_id TEXT NOT NULL REFERENCES users(id),
  role TEXT NOT NULL CHECK (role IN ('owner','teacher','student')), PRIMARY KEY (org_id, user_id));
CREATE TABLE IF NOT EXISTS invites (
  code_hash TEXT PRIMARY KEY, org_id TEXT NOT NULL REFERENCES orgs(id),
  role TEXT NOT NULL CHECK (role IN ('teacher','student')), created_by TEXT NOT NULL,
  expires_at INTEGER NOT NULL, used_by TEXT);
CREATE TABLE IF NOT EXISTS lessons (
  id TEXT PRIMARY KEY, org_id TEXT NOT NULL REFERENCES orgs(id), family_id TEXT NOT NULL, parent_id TEXT,
  version INTEGER NOT NULL, owner_id TEXT NOT NULL REFERENCES users(id),
  scope TEXT NOT NULL CHECK (scope IN ('study','draft','published')),
  approval TEXT NOT NULL, approved_by TEXT, approved_at TEXT, class_type TEXT NOT NULL,
  title TEXT NOT NULL, document TEXT NOT NULL, created_at INTEGER NOT NULL,
  UNIQUE (family_id, version));
CREATE INDEX IF NOT EXISTS lessons_org ON lessons(org_id, scope);
CREATE TABLE IF NOT EXISTS sessions (
  id TEXT PRIMARY KEY, org_id TEXT NOT NULL REFERENCES orgs(id), lesson_id TEXT NOT NULL REFERENCES lessons(id),
  host_id TEXT NOT NULL REFERENCES users(id), title TEXT NOT NULL,
  state TEXT NOT NULL CHECK (state IN ('live','ended')),
  visibility TEXT NOT NULL CHECK (visibility IN ('org','invite')), join_code_hash TEXT,
  section INTEGER NOT NULL DEFAULT 0, playing INTEGER NOT NULL DEFAULT 0,
  started_at INTEGER NOT NULL, ended_at INTEGER);
CREATE INDEX IF NOT EXISTS sessions_org ON sessions(org_id, state);
CREATE TABLE IF NOT EXISTS session_members (
  session_id TEXT NOT NULL REFERENCES sessions(id), user_id TEXT NOT NULL REFERENCES users(id),
  joined_at INTEGER NOT NULL, left_at INTEGER, PRIMARY KEY (session_id, user_id));
CREATE TABLE IF NOT EXISTS session_events (
  session_id TEXT NOT NULL REFERENCES sessions(id), seq INTEGER NOT NULL, at_ms INTEGER NOT NULL,
  actor_id TEXT NOT NULL, type TEXT NOT NULL, payload TEXT NOT NULL, PRIMARY KEY (session_id, seq));
CREATE TABLE IF NOT EXISTS progress (
  user_id TEXT NOT NULL REFERENCES users(id), lesson_id TEXT NOT NULL REFERENCES lessons(id),
  section INTEGER NOT NULL, quiz_index INTEGER NOT NULL, score INTEGER NOT NULL, clock REAL NOT NULL,
  completed INTEGER NOT NULL DEFAULT 0, updated_at INTEGER NOT NULL, PRIMARY KEY (user_id, lesson_id));
CREATE TABLE IF NOT EXISTS quiz_attempts (
  user_id TEXT NOT NULL REFERENCES users(id), lesson_id TEXT NOT NULL REFERENCES lessons(id),
  question INTEGER NOT NULL, selected INTEGER NOT NULL, correct INTEGER NOT NULL, at INTEGER NOT NULL,
  PRIMARY KEY (user_id, lesson_id, question));
CREATE TABLE IF NOT EXISTS questions (
  id TEXT PRIMARY KEY, org_id TEXT NOT NULL, session_id TEXT NOT NULL REFERENCES sessions(id),
  lesson_id TEXT NOT NULL, user_id TEXT NOT NULL REFERENCES users(id), section INTEGER NOT NULL,
  text TEXT NOT NULL, shared INTEGER NOT NULL, answer TEXT, answered_by TEXT, created_at INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS build_events (
  org_id TEXT NOT NULL, family_id TEXT NOT NULL, seq INTEGER NOT NULL, at INTEGER NOT NULL,
  actor_id TEXT NOT NULL, type TEXT NOT NULL, lesson_id TEXT NOT NULL, summary TEXT NOT NULL,
  PRIMARY KEY (family_id, seq));
CREATE TABLE IF NOT EXISTS jobs (
  id TEXT PRIMARY KEY, org_id TEXT NOT NULL REFERENCES orgs(id), user_id TEXT NOT NULL REFERENCES users(id),
  kind TEXT NOT NULL, state TEXT NOT NULL CHECK (state IN ('queued','running','ready','failed')),
  input TEXT NOT NULL, output TEXT, error TEXT, usage TEXT, created_at INTEGER NOT NULL, finished_at INTEGER);
`;

function open(file) {
  const db = new DatabaseSync(file);
  db.exec('PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;');
  db.exec(SCHEMA); // idempotent: every statement is IF NOT EXISTS
  return db;
}

module.exports = { open };
