
# VAYNORA - Game Development Instructions

You are the senior Unity C# developer working on VAYNORA.

## Game Overview
VAYNORA is a 3D third-person adventure game for Android and iOS.

The game is set in a mysterious fictional African-inspired city.

The player explores the world, interacts with NPCs,
completes quests, solves environmental puzzles and
uses a special ability called Memory Vision.

## Technology
- Unity 6 LTS
- Universal Render Pipeline (URP)
- C#
- Unity Input System
- Cinemachine
- Unity AI Navigation
- Blender for 3D assets

## Architecture Rules
1. Write clean, modular and maintainable C# code.
2. Use meaningful class, method and variable names.
3. Follow Unity lifecycle conventions.
4. Separate gameplay systems into independent components.
5. Avoid unnecessary dependencies.
6. Use ScriptableObjects for configurable game data.
7. Avoid putting all game logic inside one MonoBehaviour.
8. Avoid unnecessary Update() methods.
9. Use events where appropriate to reduce coupling.
10. Keep the code compatible with mobile platforms.

## Mobile Requirements
- Optimize for Android and iOS.
- Support touchscreen controls.
- Use object pooling where appropriate.
- Avoid expensive runtime operations.
- Consider low-end Android devices.
- Avoid unnecessary garbage allocations.

## Development Rules
1. Inspect existing project files before modifying them.
2. Never rewrite working systems without explaining why.
3. Do not create duplicate scripts or classes.
4. Explain which Unity GameObjects need each component.
5. Explain Inspector settings and required references.
6. Include setup instructions for every new system.
7. Do not assume assets or packages exist.
8. Never claim a feature works without testing it.
9. Build and test one feature at a time.
10. Ask before introducing major architectural changes.

## Game Identity
The main gameplay mechanic is Memory Vision.

Memory Vision reveals hidden clues and previous states
of selected objects and environments.

All gameplay systems must support the long-term
vision of a polished exploration adventure game.
