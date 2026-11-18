# Clarenza hosted service

A multi-tenant backend for Clarenza: organizations (teams), org-scoped lessons with the same
draft / approve / version rules as the local pilot, shared class sessions, recordings that anyone
with access can replay at their own pace, shared questions, teacher insights, a build log per
lesson, and a job queue for lesson generation.

It sits beside the local Windows pilot. `Service/` (the local bridge on port 47831) is unchanged
and the Unity app does not talk to this server yet. See "Not done" below.

## Run

Requires Node.js 22.5 or newer. No `npm install`: it uses only Node built-ins (`node:http`,
`node:sqlite`, `node:crypto`).

```
node Server/server.js
```

Then open one of the two views:

- **Teacher view**, http://127.0.0.1:8787/teacher — create an organization, invite teachers and students,
  build a lesson from pasted source text, review and publish it, start and control a live class, answer
  shared questions, and read insights per lesson.
- **Student view**, http://127.0.0.1:8787/student — join with an invite, follow a live class, replay a
  recording at your own speed, study a published lesson at your own pace, take the practice quiz, ask
  private or shared questions. Progress is saved to the account.

| Variable | Default | Meaning |
| --- | --- | --- |
| `PORT` | 8787 | Listen port |
| `HOST` | 127.0.0.1 | Listen address. Keep it on localhost behind a reverse proxy that terminates HTTPS |
| `CLARENZA_DB` | `Server/clarenza.db` | SQLite file. Back this file up; it is all the state |
| `OPENAI_API_KEY`, `OPENAI_MODEL` | unset | When both are set, lesson jobs call the OpenAI Responses API. Unset, jobs lay the supplied passages out as sections and say so |

First use: register with a username, a password of 12+ characters and an organization name
(`POST /v1/auth/register`). That account is the organization's owner. Create invites for teachers
and students; they register with the invite code, from the watch page or the API.

Run the tests with `node --test Server/test/*.test.js`.

## How it works

A class session is a small state record plus an ordered event log (`started`, `joined`, `play`,
`pause`, `goto`, `question`, `answer`, `left`, `ended`). Live viewers receive events over
Server-Sent Events. When the host ends the class the log is frozen and becomes the recording:
a client replays it against the pinned lesson version with its own clock, speed and seeking.
No video is stored.

## Rules enforced by the server

- Every lesson, session, question and job belongs to one organization. A caller who is not a member
  gets `404`, so nothing reveals that the resource exists.
- Roles are `owner`, `teacher`, `student`. Students can save only private `study` lessons.
  `published` requires `reviewed: true` from a teacher or owner.
- Identity and approval fields (`id`, `familyId`, `version`, `ownerId`, `approval`, `approvedBy`,
  `classType`) are assigned by the server. Values sent in a document are ignored.
- Published versions are immutable. A revision is a new row with the next version; revising from
  a stale parent returns `409`.
- A private study lesson is visible only to its owner, including from organization owners.
- Only the host controls a session. An ended session cannot be changed.
- A question is private unless its author sets `shared: true`. Private questions are never
  broadcast, never in a recording and never in insights.
- Insights contain counts, per-question miss rates and shared questions. They never contain notes
  or private questions.
- Passwords: scrypt with a per-user salt, constant-time comparison. Session tokens and invite and
  join codes are stored as SHA-256 hashes. Login, registration, invite and join attempts are rate limited.
- Requests carrying a foreign `Origin` are refused for anything but `GET`; no CORS headers are sent.

## Routes

All bodies are JSON. All routes except register, login, `/health` and the two pages need
`Authorization: Bearer <token>`.

| Method and path | Purpose |
| --- | --- |
| `POST /v1/auth/register` | `{username, password, orgName}` creates an owner and organization; `{username, password, invite}` joins one |
| `POST /v1/auth/login`, `POST /v1/auth/logout`, `GET /v1/me` | Session token (8 hours) and memberships |
| `POST /v1/orgs` | Create another organization |
| `GET /v1/orgs/:org/members` | Members and roles |
| `POST /v1/orgs/:org/invites` | `{role, hours?}` returns a single-use code, shown once |
| `POST /v1/invites/accept` | `{code}` for a signed-in user |
| `GET /v1/orgs/:org/lessons` | Latest published version per family plus your own newer work; `?all=1` for every visible version |
| `POST /v1/orgs/:org/lessons` | `{scope: study|draft|published, reviewed?, parentId?, document}` |
| `GET /v1/lessons/:id`, `GET /v1/lessons/:id/history` | A lesson; the versions of its family you may see |
| `GET /v1/orgs/:org/families/:family/build` | Build log: how the lesson was made, step by step |
| `PUT /v1/lessons/:id/progress`, `GET` same | `{section, quizIndex, score, clock, completed}` per learner |
| `POST /v1/lessons/:id/attempts` | `{question, selected}`; the server marks it. First attempt counts |
| `POST /v1/orgs/:org/sessions` | `{lessonId, visibility: org|invite, title?}`. `invite` returns a `joinCode` |
| `GET /v1/orgs/:org/sessions?state=live|ended` | Sessions you may see |
| `POST /v1/sessions/:id/join` (`{code}` for invite-only), `POST /v1/sessions/:id/leave` | Presence |
| `POST /v1/sessions/:id/control` | Host: `{action: play|pause|goto|end, section?}` |
| `GET /v1/sessions/:id/events` | Server-Sent Events: `snapshot`, then events; send `Last-Event-ID` to resume |
| `GET /v1/sessions/:id/recording` | `{session, lesson, events}` |
| `POST /v1/sessions/:id/questions`, `GET` same | `{text, section?, shared}`; list returns shared ones and your own |
| `POST /v1/questions/:id/answer` | Host or teacher answers a shared question |
| `GET /v1/orgs/:org/insights?lesson=` | Teacher view |
| `POST /v1/orgs/:org/jobs`, `GET /v1/jobs/:id` | `{kind: "lesson", topic, level, minutes, language?, sources[]}` then poll |

## Validation

Tested on 2026-11-18 with Node v22.23.5 on Linux, `node --test`, 8 tests, all passing, each on
its own temporary database and a real HTTP server:

- Registration, invites (single use, expiry, role limits), login, logout, token expiry, login rate limit.
- Students cannot draft or publish, including with forged fields; server-assigned identity and approval.
- Private study lessons hidden from other students, teachers and the owner across get, list, history and build log.
- Versioning: v1 to v2 to v3, stale-parent conflict, latest-per-family listing.
- A second organization gets `404` on every resource type and cannot start a session on a foreign lesson.
- Sessions: host-only control, gap-free ordered events, heartbeat, resume from `Last-Event-ID`,
  recording equal to the live sequence, frozen after end, pinned lesson version.
- Invite-only sessions invisible without the code, to the owner as well.
- Private questions absent from the stream, the recording and insights.
- Progress per learner, server-marked attempts, insight counts.
- Jobs: validation, queue to ready, rejection of output citing a missing source, per-organization daily limit.
- Sign-in and data survive a server restart. Foreign `Origin` and non-JSON bodies are refused.

Each of five deliberately broken rules (host check, lesson privacy, membership check, private
question broadcast, resume) made a test fail, so the tests do detect those faults.

Not verified:

- The teacher and student pages in a real browser. Their scripts were syntax-checked, every element
  id they use exists, and both pages are served; nobody has clicked through them yet.
- The OpenAI generator against the real API. No key was used and no paid call was made.
- HTTPS, a reverse proxy, load, many concurrent streams, or more than one server process.
  Rate limits and stream fan-out live in one process's memory.
- Any Unity integration.
- `node:sqlite` prints an "experimental" warning on Node 22. It is stable in later Node releases;
  check before upgrading.

## Not done

- The Unity client still calls the local service. Pointing it here is the next stage
  (an API client class, organization picker, join and replay screens).
- Speech. The local pilot uses Windows speech; this server has no narration audio.
- AI tutor on the server, password reset, account deletion, admin removal of members, backups.
- A data policy for learners. Required before real students use a hosted copy.
