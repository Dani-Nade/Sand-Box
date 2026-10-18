# Level-based roguelike prototype

Open `Assets/_Project/Scenes/RoguelikeRun.unity` and press Play. It is the first scene in Build Settings. You can also use **Esc > Start New Run** from the test arena. The arena and its hand-edited layout are preserved.

Movement accepts keyboard/mouse and gamepad bindings only; the template's generic joystick/XR axes are ignored. After starting Play or returning focus, movement must reach neutral once before it is accepted. If you held a movement key while starting, release it and press it again. This prevents stale editor input from starting an automatic walk without resetting your key bindings.

## Run loop

1. Fight every enemy in the current room. The objective shows the remaining count.
2. A full clear pauses combat and offers **three distinct random gun upgrades**. Click one or press **1 / 2 / 3**. There is no purchase cost and no reward reroll by waiting or opening menus.
3. The selected upgrade applies to every gun for the rest of the run, and the next room starts automatically with refilled magazines. Health carries forward; Vampiric Rounds can recover it through kills.
4. The final room also gives an upgrade choice, then shows the victory screen. Dying or falling out of the level ends the run. **Start A New Run** resets gun upgrades, base-stat investments, health, ammo, and room progress.

Kills no longer grant stat points. Every room clear gives one base-stat point, independent of the free gun reward. The existing three starting stat points remain. Hold **Tab** during combat to invest those points in weapon damage, magic damage or health and inspect your earned gun upgrades; gun upgrades cannot be purchased there. Escape cannot dismiss the mandatory reward screen. Combat, projectiles, movement and weapon input are blocked while choosing.

## Levels

| Level | Layout | Enemies |
| --- | --- | --- |
| The Courtyard | Open sightlines, two low cover blocks, central column | 4, 50 HP each |
| Crossfire Hall | Four pillars, central cover, flanking lanes | 6, 65 HP each |
| The Crucible | Climbable walls, cover, central pillar and two lasso beams | 8, 80 HP each |

Enemies alternate between melee pursuers and ranged skirmishers. Yellow bodies signal a 0.45-second attack windup; ranged enemies fire visible, dodgeable bolts that cover blocks. Their gold head hitboxes take double direct-hit damage. Unlike arena dummies, enemies do not respawn. These are deliberately basic greybox encounters, not final enemy AI or Hades' full progression system.

## Implementation

- `RoguelikeRun` owns encounter membership, progression, reward validation, modal time ownership and fresh-run resets. Only deaths registered in the current room count. The final-shot input is debounced, and phase changes prevent duplicate rewards.
- `UpgradeDraft` uses one random generator per run, excludes capped upgrades and never duplicates a choice. If fewer than three eligible upgrades remain, it shows the available ones; when none remain, recovery/continue replaces the cards.
- `RunLevelFactory` builds the three layouts; `RunEnemy` and `RunEnemyProjectile` provide prototype combat without a navmesh bake.
- `RunRewardPanel` provides the objective, three-card selection, victory and defeat screens.
- `RoguelikeRunBuilder` clones the saved arena's player/weapon/UI rig into a **separate scene**, rewires player references and creates a room preview. **Sand House > Build Roguelike Run** regenerates only the new scene (save your edits first). It never rebuilds or saves TestArena.
- Between rooms, the old room is deactivated before disposal, owned dynamite is removed and weapon reloads are cancelled/restocked. Stats and upgrades stay on the same player. Arena checkpoint respawning is disabled only in the run scene.

## Reference and validation

The inspiration is Hades' run-only, choose-one-of-three boon reward structure, not a copy of its art, exact room routing, or reward probabilities: [Hades boon rewards](https://hadeswiki.com/wiki/artifacts/boon-reward), [Supergiant's Hades page](https://www.supergiantgames.com/games/hades/).

Run the PlayMode tests in the Unity Test Runner. `GunUpgradeTests` covers the ten gun effects and the read-only stats UI; `RunFlowTests` covers room clears, random drafts/caps, invalid/double selections, no kill currency, full completion, defeat/falling, and resets. `RunSceneSmokeTests` loads the saved scene with the actual player/input/weapons, shoots a spawned enemy and advances through its reward UI.
