# Two-class pilot — 2026-10-05

## Delivered
- Teacher publishing and private student AI requests share the preserved auditorium.
- Request topic, level, approximate 5–30 minute duration, language and optional sources.
- Without supplied sources, select matching teacher-approved material from the local catalog. Matching uses title keywords; this is not web research or semantic retrieval. No suitable source produces a clear error.
- Generated sections include text slides, narration and quiz references. Slides are board text, not designed image decks. NotebookLM is not connected.
- Teachers save unpublished drafts or explicitly approve publication; students cannot publish. Server-owned identities, ownership and approval records override client-supplied metadata.
- Revisions create immutable versions; older published records remain available. Stale-parent revision requests are rejected. The catalog currently lists all versions.
- Quiz options, correct answer and explanation can be reviewed before publication. Human approval records an action, not independent factual certification.
- Local progress records the passage and quiz state every ten seconds and at key actions. Resume restarts narration at the passage boundary, not an exact audio timestamp. Progress is per account and lesson version, not synchronized across devices.
- Asking the AI tutor pauses narration. Notes for legacy lessons retain their existing title-based filenames; new versioned lessons use ID-based note filenames.

## Use
Extract and run Launch.cmd. Existing local accounts remain in their existing data directory. Teacher: import, preview, save draft or approve publication. Use Revise on your own catalog item to create another version. Student: open a published class or choose Create a private AI class / study lesson. For a topic-only request, publish suitable teacher-approved source material first.

## Verification
Service and account tests passed. New tests cover draft isolation, forged approval, ownership, immutable old versions, stale revisions, source selection, missing credentials, incomplete model output and mocked generation. Unity's built-player test published a source lesson using an isolated teacher account, fetched it as a student, verified identity metadata and restored progress. Existing narration, gesture, board and note tests passed. New lobby layout screenshot reviewed. Test accounts were isolated from real accounts and are not included in the release.

## Needs user input
Live AI is unconfigured. Choose provider/model and a test spending cap; enter the key via Launch-With-AI.cmd locally, never in chat. No paid AI calls were made. Mocked response tests do not establish factual quality, language quality or duration accuracy.
Choose the exact headset and provide physical testing access before headset/AR validation. No claim of VR/AR hardware support is made by this Windows release.

## Deferred
NotebookLM integration, production remote hosting, cross-device synchronization, teacher analytics, live instruction and multiplayer remain unimplemented. No original Unity project assets or real account records were modified by automated tests. ECC contract-first guided the shared identity schema and generated Unity metadata type; no subagents were used.
