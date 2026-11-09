# Stream Deck integration

Adapted from Nicolas’s D:\streamdeck generator and command plugin. The application builds profiles from its own current Star Citizen control catalogue and actionmaps.xml, rather than the source project’s fixed default-key command list. It supports the 15-key 5×3 and XL 8×4 models, paginated folders, keyboard/mouse chords and button-held actions. Unsupported or explicitly cleared inputs stay unavailable.

The bundled plugin is Windows x64 (its Koffi native dependency is x64). Install dependencies with npm ci in com.nlebel.starcitizen.sdPlugin, then package from this directory with npm exec --yes --package @elgato/cli -- streamdeck pack com.nlebel.starcitizen.sdPlugin --output . --force. Commit the resulting .streamDeckPlugin; the server embeds it for both normal and single-file builds. Dependency licenses are inside the installer.

Changes to the source plugin: generated profiles can override tap behavior and key colour; long labels wrap onto the key face. The focus guard and physical scan-code sender are retained.

Button artwork lives in `com.nlebel.starcitizen.sdPlugin/bin/key-art.js`, served to the builder as `/api/streamdeck/art.js` and imported by the plugin. Function and category choices come from `data/appearance.json`; the generated profile carries the chosen icon and colour. The preview uses the same renderer and keyboard hints. Native folder titles remain Stream Deck titles over wordless artwork, so each folder has only one label.

After changing folder symbols or colours, run `node tools/render-streamdeck-art.mjs` from the repository root (Node with built-in WebSocket and local Chrome; set `CHROME_PATH` if needed). Commit `integrations/streamdeck/folders/*.png`, `back.png` and `next.png`. After changing plugin code, raise both the plugin manifest version and `StreamDeckProfiles.PluginVersion`, then repack the installer above. The packed-source test catches a stale installer.

Open in Stream Deck uses the Windows file association to hand off a generated .streamDeckProfile. It does not edit live ProfilesV3 files. Hardware input and import still need testing with both target devices.

The default category presets are transcribed from D:\streamdeck/tools/gen-profile.mjs into presets.json. Each command is linked to a specific action-map/action id; its original tap/hold behavior is retained. The two mining-power presets turn the current mouse-wheel axis into positive/negative pulses only while that axis is still bound to maxis_z. Extended is unselected by default. Other actions can be added through All game commands.
