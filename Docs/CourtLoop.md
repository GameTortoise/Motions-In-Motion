# Court loop

The display host runs the trial and car physics. Controllers own a `Networking` object and submit PurrNet RPCs; the server validates the role, evidence pile, drawing limits, and team reservation. A buffered snapshot supplies the current case, evidence, speaker, clock, verdict, and team locks to every controller.

The lobby requires three participants in addition to its owner. Four participants are rejected with the requested chat message. The existing developer override is unchanged. Roles persist across CaseSelection and MainGame. One participant is judge; the remaining participants alternate prosecution/defense.

`CourtAssets` references the existing case assets, evidence canvas/card, paper, wood, fonts, Goobers, and music. `CourtProjectBuilder.Build` rebuilds those references, copies the original evidence canvas into its reusable prefab, creates the case-selection spawner and terrain finish lines, and validates the schedule. The generated runtime assets and scene changes are checked in; running this builder is not necessary to play.

Each team reserves one delivery while its submitting player draws. Cars use the existing wheel installer and spinner. A successful arrival destroys the car and shifts the two-card evidence board, or starts an objection pause. Flips, cancellation, disconnected senders, and a two-minute delivery timeout release reservations. Only the judge may submit the verdict after the trial clock finishes.

## Validation commands

Use Unity 6000.6.0f1, with WebGL Build Support installed. Close the project in the editor before using batch mode, or run against an isolated project copy.

- `-batchmode -nographics -quit -projectPath <project> -executeMethod CourtProjectBuilder.Build`
- `-batchmode -nographics -quit -projectPath <project> -executeMethod CourtProjectBuilder.BuildWebGL`
- `-batchmode -nographics -quit -projectPath <project> -executeMethod CourtProjectBuilder.BuildSmoke`

The smoke build is written to `Builds/CourtSmoke/CourtSmoke.exe`. Launch one instance with `-court-host`, wait for its local WebSocket listener on port 15091, then launch five controller instances. Give each process its own `-logFile`. `-batchmode -nographics` is supported. Successful runs log `COURT NETWORK SMOKE PASSED` on the host and `COURT CLIENT SMOKE PASSED` on all five controllers.

The test uses actual separate PurrNet processes and checks roles across scene changes, the selected case, competing team requests, fragmented PNG wheel uploads, finish-line collisions, destroyed-car cleanup, flips, cancellation, role/evidence rejection, objection pause/resume, all speaking boundaries, and the judge's verdict. Clock boundaries are accelerated by the development-only harness; production gameplay always uses the real 20-minute schedule. The harness is only instantiated in the generated test scene.

The test transport is local WebSocket. Internet lobby/service availability and browser autoplay policies are separate from these local transport checks. A browser host and controllers should additionally be exercised through the normal lobby on the intended hosting service before a public release.
