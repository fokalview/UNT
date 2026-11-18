# Clarenza product brief and next-build plan
Date: 2026-10-05

## Decision
Proceed with a bounded two-class pilot, not a public launch. Product: an immersive teacher multiplier, with VR as the primary experience, AR as a subsequent delivery mode, and lightweight phone access. Students request private AI classes; teachers publish reviewed lessons. Live teaching is a separate later capability.

## Audience and hypothesis
Initial buyer hypothesis: one Nigerian school or tutoring organization. Users: one teacher and a small student cohort with known device access. Validate availability of shared headsets, phones, connectivity and data budgets before choosing deployment. No institutional demand or learning gains have been proven.

## Current evidence
Local Windows Unity pilot includes preserved auditorium, teacher presentation and roaming, positional narration, local accounts, role-gated publishing, source-text/PPTX import, quizzes, notes and a circuit lab. Service/server.js contains source-based lesson and tutor API calls, but live AI quality is unvalidated. It binds to localhost, and is not a remote multi-user backend. Real headset VR, AR, shared sessions and cross-device synchronization are not delivered.

## Ordered work and acceptance gates
1. Lesson identity and approval: immutable lesson ID, version, source provenance, creator, approval status; private AI versus published teacher class. Gate: a student cannot publish, change approval state, or read another student's private lesson, including direct requests. Edits to approved content create a draft requiring approval.
2. AI class request: topic, level, duration, language, optional files; retrieve approved sources or ask for material when none is available. Generate a single structured package of slides, narration, questions, explanations and source references. Gate: one real generated circuit lesson validated against its sources; malformed output, unsupported sources, timeout and retry behavior tested. No fabricated success or silent mock fallback.
3. Teacher class: import, preview, edit, approve, publish a version. Gate: a second account can attend the published circuit lesson; teacher changes do not silently rewrite an in-progress lesson.
4. Private help and progress: pause/bookmark lecture, ask about current passage, follow-up explanation, resume; persist quiz attempts and progress per user and lesson version. Gate: restart and resume; unsupported answers identified; notes remain private.
5. Early headset feasibility in parallel with data-contract planning, before major UI expansion: select actual hardware, test existing scene performance and interaction, isolate any Unity/XR migration. Gate: read board, operate book and quiz, hear directional voice, and complete a seated session on hardware. Do not infer headset support from desktop tests. Schedule AR placement/passthrough only after a target device is known.
6. Teacher feedback: aggregate common quiz errors and explicitly submitted questions with clear student disclosure. Gate: teacher can issue one reviewed clarification to affected learners without receiving private notes automatically.
7. Network service and lightweight phone client: authenticated hosted service, HTTPS, durable storage, account isolation, generation jobs, quotas, caching and progress synchronization; phone lesson reader with offline downloaded content and queued progress. Gate: real headset/desktop and phone share a learner record and recover from disconnects. Local Windows service and voice generation are not deployable as-is for this architecture.
8. Pilot: one teacher, one topic, two class types, initially 10-20 consenting learners as a proposed operational cohort, not a powered efficacy study. Baseline and unfamiliar-problem assessment, later retention check, teacher minutes, completion, data use, AI cost and comfort. Agree thresholds before running; evaluate results before expanding.

## ECC application
Applied product-lens to diagnose scope and order priorities. Candidate skills for implementation: product-capability for executable acceptance criteria; api-design and contract-first for lesson/progress interfaces; cost-aware-llm-pipeline for caching and budgets; security-review for isolation and credentials; eval-harness for factual support and quiz quality; browser-qa for the phone/browser experience. Read relevant skills when implementing. ECC's Chrome DevTools can assist web QA; it cannot establish Unity animation quality, headset comfort or real AR tracking. No delegated agents were used.

## Dependencies
Before paid live-AI checks: configured provider credentials and a bounded spend decision. NotebookLM adapter only for verified supported APIs; slide/quiz automation must not depend on undocumented endpoints. Before hardware claims: exact headset and physical access. Before pilot: partner teacher and approved curriculum/resources.

## Deferred
Full multiplayer, live-teacher streaming, broad AR device support, new environments, cosmetic redesign and unrestricted arbitrary-topic simulation generation. Preserve the auditorium and modern Codex direction.

## Success measure
More students reaching a defined learning standard per teacher-hour, with sustainable data and inference costs. No numeric multiplication or learning-gain claim until measured.
