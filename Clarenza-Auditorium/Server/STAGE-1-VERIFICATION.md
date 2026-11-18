# Stage 1 — web-view verification

Date: November 18, 2026 (America/New_York). Branch: `claude/hosted-backend`.
Scope: Stage 1 only, primarily R01/R03/R09/R10 plus the existing hosted live-class views.
Stages 2–6 have not started. No paid provider call, dependency installation, database migration,
history rewrite or push to main was performed.

## Changes and demonstrated defects

- Saved drafts previously became inaccessible in the teacher page. **Review draft** now reopens
  their content and publishes a new version in the same family after explicit review. The browser
  regression failed on the original missing button before the fix. The preview says unreviewed.
- Sign-out and organization changes clear draft/source, invite, live and insight state. Late job,
  lesson and list responses cannot restore another account's work. A pending-generation test
  switches accounts before releasing the worker result and confirms the draft stays cleared.
- Rebuild failures clear the previous draft and allow retry. Publish/save buttons suppress duplicate
  submissions while pending. Opening host controls no longer makes the event handler wait for the
  full SSE lifetime; ended controls are disabled and answers restore from the event log.
- Question text and sharing consent reset between rooms and after sending. Private questions are
  visible to their author on return, without appearing in the teacher page, recording or insights.
- Self-paced study restores quiz position/score and section; answering saves progress immediately.
  Writes are serialized, and a failed quiz request re-enables answers for retry. Leaving waits for
  pending progress before browsing; failed progress saving now reports a connection error.
- Teacher tables and grid sizing no longer overflow the phone-width page. Visual review caught
  the overflow; the assertion now compares scroll width with client width, excluding the scrollbar.
- A valid study-party code previously let staff read a student's private lesson: a new HTTP
  assertion failed with 200 instead of 404. The server now denies staff access on join, listing,
  recording, events, questions and answers, including a former participant promoted to teacher.
  Peer student invitations still work. No client-only permission rule was introduced.

## Verified and how

- **Hosted tests: 17 tests/subtests passed, zero failures/skips**, with Node **v24.21.0** and installed
  Chrome **157.0.8103.79** on Windows. Command from `Clarenza-Auditorium/`:

  ```powershell
  $env:CLARENZA_BROWSER = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
  $env:CLARENZA_TEST_ARTIFACTS = '<existing isolated artifact directory>'
  node --test Server/test/*.test.js
  ```

  The original eight API tests are retained. They exercise real HTTP, SQLite, roles, versioning,
  tenant isolation, private study/questions, ordered/resumed SSE, recordings, jobs and first-attempt
  scoring. The new native-CDP browser harness uses Node built-ins and an installed browser; it does
  not substitute a DOM mock or call the page's authoring/room functions to bypass the controls.

- **Two-account browser journey:** create owner/organization, issue and redeem a student invite,
  build an offline source draft, save/reopen, reject publication without review, approve version 2,
  start/join a live class, play/pause/advance by SSE, ask private/shared questions, answer, leave/rejoin,
  end, replay at 2×, seek, study independently, submit a deliberately wrong quiz answer, resume,
  and verify completion and 100% first-attempt miss rate in teacher insights. Assertions verify
  private question absence in teacher/recording/insight output and rejection of student publication
  and insights access. Additional tests cover account changes, delayed job completion and retry.

- **Provider distinction:** the real offline generator was exercised through the browser. It
  produces source passages with no quiz. The separately titled `Browser quiz fixture` uses an
  injected deterministic worker result with a known source-supported quiz; it is visibly marked
  `TEST FIXTURE — not live AI`. This tests the UI/worker/permission/scoring flow, not AI quality.

- **Server entry point/manual browser:** started `node Server/server.js` on localhost:8787 with an
  isolated database and both AI environment settings cleared. Opened `/teacher` and `/student`
  in separate Chrome contexts. Created the organization and invite, registered the student, built
  and saved a source draft, observed the missing reopen path, published a source lesson and opened
  host controls. The automated browser regression then exercised the full final journey above.

- **Rendering:** captured and visually reviewed teacher/student views at **375, 768 and 1440 px**.
  Final DOM assertions found no horizontal page overflow. Browser regression found no unhandled
  JavaScript exceptions and no unexpected HTTP errors; the one expected 400 is the blank-source
  failure test. Manual DevTools consoles were also clear. This is not a full accessibility audit.

- **Local Windows service, unchanged:** all four commands passed:

  ```text
  node Service/test.js
  node Service/accounts.test.js
  node Service/classes.test.js
  node Service/speech.test.js
  ```

  Speech tests synthesized real local WAV at three rates and checked headers/input rejection.
  Provider calls in the existing generation tests were mocked, not paid.

- **Existing Unity Windows player regression:** copied the existing desktop binary to the isolated
  test workspace and ran it against this branch's unchanged local service on port 47831 with
  isolated fixture accounts. The branch's `AuditoriumApp.cs` SHA-256 matched the existing project
  source. No new Unity binary is claimed. Runs exited **0**:

  ```text
  Clarenza.exe --smoke-test --class-test --walk-test --home-test --read-aloud-test
  Clarenza.exe --smoke-test --study-lab-test
  ```

  Confirmed markers: `TWO_CLASS_UNITY_CONTRACT_AND_RESUME_PASSED`,
  `TEACHER_GESTURE_TEST_PASSED`, `BOARD_STABILITY_TEST_PASSED`,
  `NARRATION_PLAYER_TEST_PASSED`, `LESSON_TIMING_TEST_PASSED`,
  `TEACHER_STAIRS_TEST_PASSED`, `TEACHER_FOCUS_AND_PAUSE_TEST_PASSED`,
  `READ_ALOUD_RUNTIME_TEST_PASSED`, `AUDITORIUM_RUNTIME_SMOKE_PASSED`,
  and `CLARENZA_RUNTIME_SMOKE_PASSED`.

- **Repository:** `git diff --check` passed. Stage 1 changes contain source, tests and this report;
  no API keys, browser profiles, database files, Unity caches, builds or third-party asset packs.

## Not verified

- Live AI generation/tutoring, real provider cost, cloud speech or factual model quality. Spend: $0.
- Unity on the hosted API, two physical PCs in one built-player class, headset/AR behavior, or any
  later-stage feature. No Unity upgrade or fresh build was needed for these server/web changes.
- Production hosting, HTTPS/proxy, Internet outages, load, multi-process fan-out, or operational
  backup/restore. Tests use isolated local databases, not real learner records.
- Node 22 on this Windows host, Safari/Firefox, physical mobile devices, screen readers, a complete
  keyboard-only journey, WCAG conformance or performance/Core Web Vitals. No visual baseline
  exists, so visual-regression comparison is inconclusive; screenshots were manually reviewed.
- The known Unity `JobTempAlloc`/allocation-lifetime warnings remain (4,581 `JobTempAlloc`
  occurrences in the combined smoke log). Passing assertions does not resolve those warnings or
  certify performance. No changes to Unity code were made in Stage 1.

## Reproduction and artifacts

The browser test launches its own Chromium process/profile. `CLARENZA_BROWSER` must name an
existing installed executable; without it the browser test is explicitly skipped. No download
or package installation occurs. Test databases are disposable fixtures; the separate manual
database and browser profiles are retained in the task's `work/` directory, outside Git.

Task workspace: `C:\Users\Mikal H. W. Brown\Documents\Codex\2026-11-18\whre`.
Evidence retained under `work/`: `stage1-teacher-{375,768,1440}.png`,
`stage1-student-{375,768,1440}.png`, `stage1-unity-smoke.log`, `stage1-study-lab.log`.
The user-facing report and screenshots are copied to `outputs/Stage-1-Verification/`.

Stage 1 is ready for review. Stop here and wait for the user's instruction before Stage 2.
