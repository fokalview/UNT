# Restoration and Codex release notes

## Delivered
- Restored BlueSuitFree01 from the original project with its existing face/suit materials and texture dependencies. Unity resolved the legacy prefab and validated its humanoid avatar.
- Enabled Unity's bundled Animation module, previously absent from the pilot. No external package purchase, Unity upgrade or render-pipeline migration.
- Imported two existing idle candidates rather than the complete motion library; retained the original library notice.
- Preserved the auditorium; adjusted ambient lighting, added a warm professor light, and enabled 4x antialiasing.
- Added a procedural 3D Crystal Codex shell with hinged covers, a metal spine, faceted crystals, a docked closed state and opaque reading surfaces.
- Connected existing transcript, highlight, notes, tutor, quiz and separate-browser controls to the two-page layout. Existing account and publication permissions are unchanged.

## Verification and limits
- Service and account HTTP regression tests passed, including denial of student publishing and isolation of study uploads.
- Unity build and lesson-import checks passed during integration.
- Runtime check passed local narration playback, highlight lookback, saved-note readback, and rendered all four book tabs. The teacher played a humanoid clip, with changing arm rotation measured at runtime.
- Browser self-test passed rendering, URL handling and tab lifecycle. External YouTube playback and sign-in remain manually unverified.
- This is a first functional Codex implementation, not a reproduction of the polished concept illustration. Hand tracking, page curling, lip sync and VR browser surfaces are not implemented.
- Teacher idle retargeting needs further visual refinement. The first candidate held its arms too widely; the alternate crouched and turned sideways. Both were rejected visually. Animation playback is disabled in the shipped prefab, leaving the original reference pose until a suitable clip/retargeting fix is available. There is no validated teaching gesture or walk cycle.
- No live AI requests were tested because no API credentials were supplied. Tutor/quiz UI reuses the prior implementation; no claim of educational accuracy is made by the interface test.
- No measured before/after performance comparison. Unity 2020 continues to emit JobTempAlloc warnings in runtime logs; these require separate investigation before performance sign-off.
- Original Unity assets, accounts, and user lessons were not edited by these changes. Smoke tests use the separate SmokeTestLessons directory.

No administrator approval was required. Remaining limitations are development and validation work, not permission blockers.


## Tablet presentation update
The teacher now uses a restrained procedural upper-body sequence rather than the rejected imported idle clips: hold a tablet, reach and tap, turn toward the board, present the new passage, then return to rest over about 2.8 seconds. New board text is revealed about 0.75 seconds into the sequence. Quick slide changes restart the gesture. The existing body/skin assets are reused; no locomotion, chalk writing or lip sync is implied. This supersedes the earlier static-reference-pose limitation. Finger contact and anatomical motion are approximate and should be reviewed in the running app.

## Teacher roaming and positional narration
During lecture playback, the teacher chooses among three stair routes derived from the original stair tread meshes, walks up to a randomly selected height, returns along the same aisle, and pauses between trips. The approach uses a path behind the teaching desk. Pausing the lecture or selecting Teacher: stationary stops travel; a tablet gesture also suspends travel. The student camera remains seated.
The narration AudioSource is attached to a child of the teacher at head height. Unity 3D spatial blend is 1, distance rolloff is linear (4–50 metres), and Doppler is disabled to avoid speech pitch shifts. This supplies positional stereo and distance attenuation; it is not a binaural/HRTF plugin, room acoustics or wall occlusion simulation. Headphones are recommended for a manual listening check.
Foot movement uses procedural alternating steps and stair-surface raycasts. It is an initial gait, not motion-captured stair locomotion. No dynamic pathfinding around students is implemented; travel stays on predefined mesh-derived stair routes.

## Coordinated teacher update
The teacher faces the class before starting a presentation or narration. Speech waits for the 2.8-second tablet/point sequence to finish; a six-second silent reading interval separates automatic passages. Roaming occurs during silent intervals and resumes its existing route after explanations. Turns use small procedural steps, translation accelerates gradually, and stationary feet ease into their resting positions. The presentation no longer twists the entire visual body and planted feet. Board text appears after the tap and remains fully visible.
Focus mode and Board focus request a return along visited waypoints, rather than cutting across seats; the teacher stays at the teaching area afterward. Return travel respects lecture pause, stationary mode and narration, so it can take multiple reading intervals to complete. Leaving focus restores roaming. These are procedural improvements, not motion-capture or a guarantee of perfect foot contact. Board-pointing is a general cue, not semantic pointing to individual bullet items. No lip sync or handwritten board animation was added.
