# Validation — first auditorium pilot

Completed on September 29, 2026 using Unity 2020.1.0f1 on Windows.

Passed:

- Windows player compilation and build; 72 original seat locations detected.
- Lesson source chunk mapping, empty-source rejection, and invalid-citation rejection.
- PowerPoint text extraction with numeric slide ordering (1, 2, 10).
- Local service structure validation, source-reference bounds, invalid quiz answers, empty input, rejected browser-origin requests, and health endpoint.
- Mocked Responses API request/response contract and incomplete-response handling. This does not validate a live model.
- Windows speech synthesis produces valid WAV audio.
- Built-player narration decoding and playback advance successfully.
- Runtime startup, source lesson preview, auditorium entry, seating change, focus overlay, learning book, ten-second highlight lookback, and notes save/readback.
- Screenshots of lobby, original auditorium, focus, and book reviewed.
- PowerShell launcher parses without syntax errors.

Not verified:

- Live OpenAI generation, factual grounding/citation quality, or cloud error behavior with real credentials. No API key/model was configured.
- Full manual mouse/keyboard usability testing. Automated checks call application operations directly; they do not simulate every click.
- Real-headset VR, passthrough, hand tracking, multiplayer, live instruction, or accessibility testing with learners.

Known implementation limits are listed in README.md. Focus currently dims edges rather than blurring. Transcript timestamps are passage-level. The professor and students are placeholder geometry; narration is local Windows TTS. The lecture text is progressively revealed on the original board, not a generated handwriting animation.

Browser validation: compiled x64 WinForms companion using the official WebView2 SDK 1.0.4258.31. Automated runtime test passed WebView2 initialization, local HTML rendering, URL/search normalization, unsupported scheme rejection, tab creation and disposal. Evidence: Build/Windows/Browser/browser-test.txt and browser-test.png. YouTube video playback, downloads, external sign-in and headset rendering are not verified by this test.

Account update validation: Unity auditorium rebuild passed. Service regression tests passed. HTTP permission tests passed for incorrect-password rejection, server-assigned roles, admin-only account creation, teacher publishing, unauthenticated access rejection, student publishing denial even with a forged role field, study ownership, cross-account denial, catalog visibility, and session revocation on logout. Run `node Service/accounts.test.js` and `node Service/test.js`. No real accounts or passwords were provisioned during testing; test accounts used an isolated temporary directory, removed afterward. Interactive login UI and multi-device deployment have not been manually tested.

Study lab validation: Unity Windows build passed. Runtime smoke test passed lamp activation, circuit progression and restart, and captured welcome, experiment, quiz and completion screens. Experiment screenshot visually checked for layout and readability. Evidence: Build/Windows/Evidence/circuit.png. This does not verify headset input or multiplayer.

2026-10-04 restoration pass: Account and service regressions passed. Codex runtime rendering, local narration, highlight lookback and note readback passed, including all four book pages. WebView2 self-test passed. Teacher avatar/clip validity was confirmed, but visual review rejected both existing idle candidates: wide arm posture and crouching/sideways posture. Shipping prefab deliberately disables idle playback; professor remains in its reference pose. This is an unresolved animation-quality limitation, not completed character animation. No performance comparison or real API response validation was performed. See RESTORATION-NOTES.md.

Tablet teacher release: final Windows build passed. Runtime gesture check confirmed the presentation sequence returns to rest; screenshots captured tablet tap, presentation and rest. Narration, highlight lookback, note readback and auditorium smoke checks passed on the final build. Poses were visually reviewed from the front seat. Motion remains procedural, with approximate finger/tablet contact rather than a production motion-capture performance.

Roaming teacher release (2026-10-04): Windows build passed. Final player passed a complete first-route ascent/descent and return (34.4 metres), head-height audio attachment assertion, narration playback, gesture and auditorium smoke checks. Back-seat stair screenshot reviewed. Three routes are generated from original stair geometry; only the first was exercised end-to-end by the automated test. Gait is procedural and remains a first pass; foot contact and turning need further visual polish. Stereo panning/distance were configured but not manually listening-tested. No HRTF, occlusion or headset acoustic validation. Existing Unity JobTempAlloc warnings remain unresolved.

Coordinated teacher release (2026-10-04): Unity Windows build passed. Movement integration run passed a full 34.4-metre stair route, paused-position stability, focus return and staying home. Stair and focus-return screenshots reviewed. After removing the typewriter overwrite, the final build passed BOARD_STABILITY_TEST_PASSED, LESSON_TIMING_TEST_PASSED, NARRATION_PLAYER_TEST_PASSED, TEACHER_GESTURE_TEST_PASSED and AUDITORIUM_RUNTIME_SMOKE_PASSED. Timing test includes pause while narration is pending and confirms speech begins only after the teacher settles. This is automated operation-level testing, not full manual usability or five-minute learner acceptance testing. Movement code was unchanged between the route-tested build and final board-text correction.

2026-10-05 two-class release: final Unity Windows build passed. TWO_CLASS_UNITY_CONTRACT_AND_RESUME_PASSED verifies real local HTTP teacher publication, student retrieval, generated identity fields and persisted passage resume with isolated accounts. Final player also passed gesture, board stability, narration, lesson timing and auditorium smoke checks. Service/test.js, accounts.test.js and classes.test.js passed. AI provider responses in classes.test.js were mocked: no live generation, source-grounding quality or paid calls were tested. New request-form screenshot reviewed. No headset, AR, remote multi-user deployment or cross-device sync validation. See TWO-CLASS-UPDATE.md.

2026-10-05 read-aloud release (R07): final Windows build and READ_ALOUD_RUNTIME_TEST_PASSED passed. Checked blank/oversize input, cancellation, pause/resume, unique WAV export/decoding, invalid export directory, loading notes/tutor answer, unavailable service and private-state clearing. Existing class contract/resume, gesture, board stability, narration, timing and auditorium smoke checks passed. Service/speech.test.js generated real WAV at slower/normal/faster settings; service, account and class regression tests passed. Read-aloud screenshot reviewed; native Windows SoundPlayer completed playback of exported sample. No subjective listening quality or assistive-technology certification claimed. Deliberate unavailable-service test logged connection refusal; existing JobTempAlloc warning persists. No paid AI requests.

2026-10-06 home release: final Unity build passed. Home dashboard, private-class creation and library screenshots visually reviewed. --home-test captured Home, creation, library and tools pages; these are operation-level screenshots, not simulated mouse navigation. Final player passed class contract/progress resume, teacher gesture, board stability, narration, lesson timing, read-aloud and auditorium smoke checks. Existing headset/live AI limitations and JobTempAlloc warning remain. No service/API change in this release.
