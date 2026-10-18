# Gun upgrades

Gun upgrades are **free level-clear rewards**, not kill rewards or purchases. In the RoguelikeRun scene, clear all enemies, choose one of three random eligible upgrades (click or 1/2/3), and automatically enter the next level. The choice screen pauses combat. Tab shows a read-only list of upgrades and lets you invest base-stat points on the left. Kills do not award points; each level clear awards one base-stat point separately from the free gun upgrade.

Upgrades belong to the player for the current run and apply to the revolver, rifle, shotgun, and future HitscanWeapon guns. Switching/reloading/respawning at the arena checkpoint keeps them. A fresh player/new run starts without upgrades. Dynamite is unchanged. Shared WeaponData assets are never mutated.

| Upgrade | Per stack | Maximum stacks |
| --- | --- | --- |
| Rapid Fire | +15% fire rate (interval divided by 1 + 0.15 x stacks) | 10 |
| Heavy Rounds | +20% gun damage, multiplicative with WEAPON DMG | 10 |
| Multishot | +1 full-damage bullet, +2 degrees spread; one ammo per trigger pull | 3 |
| Ricochet | +1 wall bounce, retains 75% damage per bounce | 3 |
| Piercing Rounds | +1 additional enemy per bullet | 5 |
| Extended Magazine | +25% base magazine, rounded up; reload to fill added capacity | 8 |
| Quick Reload | Reload duration x 0.8, including each round of tube reloads | 5 |
| Explosive Rounds | Terminal-impact splash, 30% of bullet gun damage, radius 2.5m | 3 |
| Arcane Rounds | +20% base bullet damage, multiplied by MAGIC DMG, not WEAPON DMG | 5 |
| Vampiric Rounds | Gun kills heal 2% of current maximum health | 5 |

Headshot multipliers apply to direct gun + arcane damage together. Splash has distance falloff (100% to 30%), requires line of sight, ignores the shooter, damages each target once, cannot headshot or trigger more explosions. Pierced enemies are damaged once per bullet even with multiple colliders. Ricochets only reflect off non-damageable surfaces; distance falloff uses total path length, and each bullet gets a finite shared bounce/pierce budget. Vampiric healing also works on splash kills, never dead targets or non-gun kills, and cannot exceed maximum health.

`UpgradeDraft.Roll(upgrades, random)` samples up to three distinct eligible upgrades without replacement. Capped upgrades are excluded. `RoguelikeRun.TrySelectReward(index)` validates the current offer and grants exactly one through `GunUpgrades.TryGrant(type)`. The old purchase API is removed. `Changed`, `CanGrant`, `GetStacks`, `MaxStacks`, and the catalog descriptions remain available. If a future long run exhausts the pool, fewer choices are shown; if all upgrades are capped, a recovery/continue button avoids a softlock.

Tests: Unity Test Runner > PlayMode > GunUpgradeTests, RunFlowTests, and RunSceneSmokeTests. See [RoguelikeRun.md](RoguelikeRun.md) for the level loop and launch instructions.
