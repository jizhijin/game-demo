# Greenhouse Alarm

A small Unity 3D arena-survival shooter prototype. Move to avoid enemies while the player automatically fires at the nearest target. A timed dash can reflect hostile shots, turning a defensive move into a counterattack.

## Controls

- WASD or arrow keys: move
- Space: dash in the movement direction (or the last direction)
- R: restart after the run ends

## Current prototype

- One fixed, top-down arena
- Automatic shots against the nearest enemy
- Pink melee chasers with 2 HP and orange ranged shooters with 1 HP
- Dashing briefly protects the player and reflects nearby hostile shots
- Player has 5 HP; the run ends at 0 HP

The scene uses Unity primitive meshes and a small custom unlit shader. No kill-based upgrades, levels, menus, or camera switching are implemented yet. See `Descriptive Document.docx` for the assignment summary and planned mechanics.

## Open in Unity

Open this folder as a Unity `6000.6.0f1` project and open `Assets/Scenes/Greenhouse.unity`. The scene is authored with a visible arena, player, sample enemies, and camera. The main game controller exposes movement, spawn, and enemy-cap values in the Inspector.

## Play in browser

GitHub Pages serves the WebGL build from `docs/`:

https://jizhijin.github.io/game-demo/

The Unity source build is also in `Build/WebGLPlayer/`.

## Team

- Jizhi Jin
- Hector R. Medina — discussion, design, and script writing

AI use was approved for this assignment. See `AI_USAGE.md` for the brief disclosure.

