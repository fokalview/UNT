# Clarenza Auditorium Pilot

This revision uses a copy of your original `University Classroom/Demo.unity` auditorium and its models, seating, textures, and blackboard. The source project in Documents/Unity has not been modified. Old recording components and cameras are removed from the working scene; lesson controls, a placeholder presenter, and a learner camera are added.

## Start

Double-click **Launch.cmd**. It starts a local narration service and the Windows application. The launcher shuts down its service when the application closes. Node.js 22+ and Windows PowerShell are required; Node.js is installed on this computer. If Windows blocks the script, inspect the files before deciding whether to run them.

The standalone executable is `Build/Windows/Clarenza.exe`. It can display lessons without the service, but narration and AI require the service.

## First lesson

1. Enter a lesson title in the lobby.
2. Paste text, or enter the full path of a `.txt`, `.md`, or `.pptx` file and choose **Import file**. The PowerPoint importer reads slide text in slide order. Images, speaker notes, animations, and image-only slides are not converted.
3. Choose **Preview source lesson (offline)** to use the exact source passages, or generate a lesson with AI after setup.
4. Review and edit each section's title, narration, and board text. Source references are shown below each section.
5. Save and enter the auditorium. Choose front, middle, or back seating and play the lecture.
6. Open the learning book for transcript passages, notes, AI questions, and practice quizzes.

The included circuit material is sample content for testing the import flow. It does not limit the subject of imported lessons.

## Controls

- Hold right mouse button to look around from your seat.
- **B** opens/closes the learning book.
- **H** starts/stops a highlight. The lookback is adjustable from 0 to 30 seconds in the transcript tab.
- **Escape** closes overlays and pauses the lecture.
- **Board focus** opens an enlarged readable board view.
- **Focus mode** dims peripheral regions and hides sample student figures. It is not headset passthrough or a true blur effect.
- The circuit study room is available from the lobby; Escape opens its menu with a return-to-lobby control.

## AI setup

Run **Launch-With-AI.cmd** instead of Launch.cmd. Enter an OpenAI model ID supporting Responses API Structured Outputs and an API key at the local prompts. The key is entered hidden and kept in the process environment for that session, not saved in project files. API usage is billed by your OpenAI API account; a ChatGPT subscription is not used by this integration. Do not paste a key into the lesson or chat UI.

Close an existing pilot/launcher before changing AI configuration. You may alternatively set `OPENAI_API_KEY` and `OPENAI_MODEL` in your own environment and use the ordinary launcher.

Generation sends the imported source text to OpenAI and returns structured sections, narration, board content, basic gesture cues, and practice questions. Tutoring sends the selected lesson's sources and your question. Private notes are not automatically sent. The server uses `store: false`; this does not claim zero retention by the API provider.

Generated content must be reviewed. References are validated for valid source IDs, but factual citation support still needs review. The bridge rejects invalid lesson/quiz structures, unsupported source IDs, oversized input, and requests carrying a browser Origin header. It binds to localhost only and is a local pilot service, not a production multi-user backend.

No AI credentials were configured during implementation, so live model generation and answer quality have not been validated.

## Narration and professor

Narration uses the installed Windows speech voice locally. It is played from the presenter using Unity spatial audio. The presenter is a simple placeholder figure with procedural pointing/explanation motion. It does not have lip sync, custom topic animations, motion capture, or a live-teacher connection. The lesson script is displayed as a passage-level transcript, not speech recognition. Highlights include passages overlapping the chosen time range, not word-accurate timing.

## Saved work

Saved classes and notes are written under Unity's persistent data folder for **U-Now Technology / Clarenza Auditorium Pilot / Lessons** (normally `%USERPROFILE%\AppData\LocalLow\U-Now Technology\Clarenza Auditorium Pilot\Lessons`). Notes are private to this local installation. They are not encrypted or synchronized. Reusing a lesson title updates the saved lesson with that title. Smoke tests use a separate SmokeTestLessons directory.

## Development

Unity version: **2020.1.0f1**, matching the original local project. The startup scene is `Assets/Clarenza/Scenes/Auditorium.unity`.

Build with Unity batch mode and `-executeMethod Clarenza.BuildAuditorium.Build`. This constructs the working scene from the copied original and validates lesson data before making the Windows build. Run `node Service/test.js` for bridge validation tests. Launch the built player with `--smoke-test` to test and capture the lobby, auditorium, focus, and book screens. This is not a substitute for manual input testing or a headset test.

API implementation reference: https://developers.openai.com/api/docs/guides/structured-outputs

## Next milestones

- Actual friend-party invitations, shared sessions, synchronized avatars, and reconnect behavior.
- A rigged professor with lip sync and richer board interaction.
- Live teaching and voice routing.
- Target headset selection, real XR controls, hand gestures, and supported passthrough.
- Production accounts, content moderation/review, deployment, and educator trials.

This project includes your existing third-party auditorium assets for local development. Nothing has been published to GitHub. Review their license before redistributing the source or making a public release.

## Desktop browser
Open **Browser / YouTube** from the lobby or learning book. The lecture pauses; close the browser to return and press Play lecture to resume.
Includes multiple tabs, URL/search bar, Back/Forward, Reload/Stop, a YouTube shortcut and the standard WebView2 download interface. Sessions/cookies use a separate local Clarenza profile in `%LOCALAPPDATA%\Clarenza\Browser`.
This is a separate Windows browser window, not a browser texture inside a VR headset. External website compatibility, YouTube playback, protected media and account sign-in require manual verification. Some sites restrict embedded browsers.
Requires the Microsoft Edge WebView2 Evergreen runtime (already present on this development computer): https://developer.microsoft.com/en-us/microsoft-edge/webview2/
Browser source: Browser/Browser.cs. Rebuild with Browser/Build.ps1 -SdkPath <extracted Microsoft.Web.WebView2 1.0.4258.31 NuGet directory>. Microsoft notices accompany the browser binaries.

## Accounts and lesson permissions
1. On your own computer, run **Create-Account.cmd** once. Choose a username, the role `admin`, and a password of at least 12 characters.
2. Start **Launch.cmd** and sign in. In the admin lobby, create separate teacher and student accounts.
3. Teachers import material, review it, then choose **Publish lesson and enter auditorium**. The lesson appears in the class catalog for all accounts on this local service.
4. Students can open published classes but cannot edit or publish them. They must choose **Open private study mode** before importing their own material. Saving there makes a private study lesson, visible only to that account. Teachers can also switch to private study mode.
5. Sign out before changing users. Sessions expire after eight hours or when the service restarts.

Role permissions are enforced by the service, including direct API requests. Passwords are salted and hashed with scrypt; the app keeps session tokens in memory. Administrators can create accounts but cannot read another user's private study uploads through the catalog API.

This is local pilot authentication, not a hosted school identity system. The computer owner has access to the data files and provisioning tool and can create accounts; Windows users sharing one OS login are not isolated from those files. Keep Create-Account.cmd on the administrator's computer when preparing a future hosted deployment. Account and lesson data lives under `%LOCALAPPDATA%\Clarenza\Accounts`; personal notes remain local to Unity and are separated by account ID. Browser cookies are a separate, shared local browser profile; sign out of websites before handing the computer to another person.

Earlier file-based lessons remain untouched in Unity's previous Lessons folder. They are not automatically published into the new account catalog. Re-import the source material as a teacher to publish it deliberately. Password resets, enrollment-specific catalogs, cloud synchronization, and school SSO are not included yet. PowerPoint import still extracts slide text; it does not reproduce slide images, embedded video or animations.

Study lab visual update: rebuilt the circuit room with a warm workbench, dark experiment mat, supported table geometry, wood wall slats, window panels, floor seams, planter and component housings. Redesigned the interface with solid high-contrast panels, teal controls, a scrollable lesson area and a direct Learning lobby button. The circuit activity and quiz are preserved. This is still the introductory circuit lab, not a general-purpose simulation for every subject.

Latest release: restored suited professor and functional Crystal Codex. See QUICKSTART.md and RESTORATION-NOTES.md for current instructions and validation limits. The original statement about a primitive professor is superseded for this release; sample student figures remain placeholders.

Roaming teacher update: randomized destinations on three auditorium stair routes, pauses between trips, slide-gesture stops, and a stationary toggle. Narration uses an attached head-height 3D AudioSource with distance attenuation, stereo positioning and no Doppler. This is not binaural/HRTF audio. Walking uses procedural leg poses and remains prototype quality.

Coordinated presentation update: face class, tap tablet, reveal the complete slide, point, then narrate. Six-second reading intervals allow restrained roaming between explanations. Focus requests a route-based return to the teaching area. Turns and foot settling are smoother procedural approximations; see RESTORATION-NOTES.md for limitations.

Two-class update (2026-10-05): see TWO-CLASS-UPDATE.md. Teacher drafts and explicit approvals, immutable versions, private AI class requests, editable quiz review and local passage/quiz resume are now implemented. Live AI remains unvalidated; hosted multi-user service and real headset support remain future work.

Read-aloud release: local personal text-to-audio, three synthesis speed settings and WAV export are available from lobby/Codex. Lecture stays paused. Limit 5,000 characters; see READ-ALOUD-UPDATE.md. Service version 4 requires closing older launchers before starting this release.

2026-10-06 home update: dedicated dashboard, sidebar and separate creation/catalog/tools/account pages. See HOME-UPDATE.md. Teacher studio is role-gated; auditorium and learning features remain in place.
