# Home screen update — 2026-10-06

The landing screen now has a dedicated dashboard instead of a long import form:
- Two main choices: teacher classes and private AI classes.
- Sidebar: Home, Teacher classes, My library, Create an AI class, role-gated Teacher studio, Study tools, Account & settings.
- Quick access to the private library, read-aloud and circuit lab.
- Separate published-class and private-library views, with search and empty states.
- Separate teacher/student creation page. Existing import, AI request, review, approval and revision behavior is preserved.
- Administrator provisioning and service configuration are on Account & settings.
- Returning from the auditorium opens Home; saving a draft opens My library.

The home design uses a quiet dark background, cyan accents and a simple Codex motif. The original auditorium remains the teaching environment. No new assets, paid services or backend API changes are required.

This release does not redesign sign-in or implement new VR/AR, online AI, multiplayer or cloud capabilities. The existing local Windows pilot limitations remain.

Validation: Windows build passed; dashboard, creation and library captures visually checked. Existing class/progress, narration, board, gesture and read-aloud built-player checks passed. Full manual click-by-click testing and headset validation were not performed.
