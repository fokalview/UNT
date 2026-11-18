# Read-aloud update — 2026-10-05

Implements R07 from CLARENZA-AI-IMPLEMENTATION-GUIDE.md in the Windows pilot.

## Use
Run Launch.cmd. Choose **Read aloud / Export audio** in the lobby or the Crystal Codex.
- Paste text, or load the current passage, your notes, or the latest tutor answer.
- Choose Slower, Normal or Faster, then Listen.
- Pause, Resume, Stop or Listen again.
- Export WAV writes a uniquely named recording; the full destination is shown in the panel.
- Close the panel, then resume the lecture yourself.

The professor pauses while personal audio is open. Read-aloud uses a separate non-spatial audio source. Text and speech remain on this computer through the installed Windows voice; no AI key or paid API is required. Limit: 5,000 characters per request. For larger documents, paste an excerpt. Speed changes regenerate speech and are reflected in exports. Editing text invalidates the old recording so it cannot be accidentally exported as the new text.

Exports are stored under the app's local Lessons/AudioExports/account-ID folder. Explicitly exported files remain after sign-out; they are ordinary local files, not encrypted account-isolated storage. The personal player clears its text, clip and export destination on sign-out/sign-in. Close stops playback; it does not automatically restart the lecture.

## Failure behavior and limits
Stop invalidates an in-flight response and aborts its HTTP request, so it cannot later play. The local service may finish already-started synthesis before its bounded timeout/cleanup. Unavailable services, empty/oversize text and unwritable output directories show errors. Native selection capture, MP3, voice cloning, cloud voices, arbitrary language voice selection, mobile/headset speech and accessibility certification are not included.

## Scope
This release implements local R07 and its regression checks. It does not execute every roadmap item. Live AI still needs provider credentials and an authorized spend cap; physical VR/AR remains dependent on target hardware and testing access. NotebookLM is not connected.
