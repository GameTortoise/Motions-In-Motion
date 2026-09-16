# Build and submit Motions in Motions

## Build

1. Open this project on `Newbranchfixed` in **Unity 6000.6.0f1**. Install **Web Build Support** for that editor through Unity Hub if it is missing.
2. Save your scenes. Select **Court > Build WebGL Submission**. This builds the enabled scene list to `Builds/CourtWebGL`, with Development Build off, Gzip compression, decompression fallback, and background execution enabled.
3. Wait for the build to finish. The first Web build can take a long time; later builds reuse the compiler cache.
4. Zip the **contents** of `Builds/CourtWebGL`. Opening the ZIP must immediately show `index.html`, `Build/`, and `TemplateData/` (plus `StreamingAssets/` if Unity generated it). Do not zip the enclosing `CourtWebGL` directory or the Unity project.

For the standard Unity workflow, open **File > Build Profiles**, select **Web**, switch platform, and use the shared scene list: `MainMenu` first, then `CaseSelection`, `MainGame`, and `evidenceSelection`. Keep Development Build off. Under **Player Settings > Publishing Settings**, select **Gzip** and enable **Decompression Fallback**. Use **Build And Run** to test through Unity's local web server; opening `index.html` directly from File Explorer is insufficient.

## itch.io

Create or edit a project, choose **HTML** as its kind, upload the ZIP, and mark it **This file will be played in the browser**. Use a 1280 × 720 embed with fullscreen available, or click-to-launch fullscreen. Keep click-to-play enabled so the initial interaction permits audio. Save and preview before publishing. The same uploaded game serves both the display host and the controllers.

See [itch.io's HTML5 upload guide](https://itch.io/docs/creators/html5). Its current archive limits are 1,000 files, 500 MB extracted in total, and 200 MB per extracted file.

## Jamzo

Open your jam's submission form and upload the same browser-game ZIP where it requests a game file. Use a ZIP with `index.html` at its root, as required by [Jamzo's published browser submission format](https://jamzo.com/jams/scorealpha). Follow the specific jam's form for title, description, controls, artwork, and team details; its account-only submission UI may differ.

For the [Jackbox Multiplayer Jam](https://jamzo.com/jams/jackbox), describe this as requiring **one display host plus three participants** (or five or more; four participants are intentionally blocked). Include your premade-asset credits and identify AI assistance with implementation, debugging, and testing, as the jam asks. Keep the existing PurrNet service/relay configuration; a web hosting ZIP does not replace the multiplayer backend.

## Final hosted check

Open the uploaded game on the display and three separate participant devices/browser sessions. Create a lobby on the display, join from the controllers, ready all participants, and start. Confirm the host selects a case, one controller is judge, and the others become opposing lawyers. On each team, submit evidence, draw wheels, and verify delivery. Test an objection, its paused clock and music, and a flipped car followed by a new request. Keep the display tab visible during play.

Local transport tests and a successful Web build do not independently verify a hosting service's iframe, mobile browser, or external relay behavior. Preview the uploaded version before submitting the jam entry.

Unity references: [Web build output](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-building.html) and [decompression fallback](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-deploying.html).
