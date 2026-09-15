# Dependencies

## Runtime scripts (core local play)
- TurnManager, PitZone, MarbleController, SwipeLaunchController
- SmoothFollowCamera, HUDManager, AudioManager, VFXManager
- GameDifficultyConfig, AIMarbleController (PvAI)

## Optional / online (referenced, not required for local kit Play)
- NetworkMatchState, NetworkSessionManager, CloudMatchManager, QuickMatchManager, …

## Assets
- Marble meshes/materials, pit meshes, physics materials
- HUD canvas + fonts, SFX clips, VFX particle refs

## Project settings to document before export
- Layers / tags used by marbles, pits, ground
- Physics collision matrix
- Input System package (if used)

## Map / décor — NOT in kit
- Phase2_P0/P1_Dressing, Village_Reference_Upgrade
- Environment_Village_Dressing, fairway décor, fences art (map-specific)
