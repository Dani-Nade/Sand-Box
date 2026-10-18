# Wall climb

Jump toward a wall while holding Space and W to grab it. Keep holding forward to climb; A/D shift sideways. Release and press Space again to push off. Release forward, look away, or crouch to drop. Reach a clear ledge to mantle automatically. The gun lowers and both hands alternate reaches during the climb.

Forward-facing climbs take priority over the existing sideways wall run. Landing and lasso release refresh climb space. Repeated grabs in the same airtime don't create unlimited height. A ceiling blocks the climb through the CharacterController.

## Reference and tuning

Based on the original movement research in [Climb Fundamentals](https://apexmovement.tech/wiki/tech/General%20Tech%3EWall%20Tech%3EClimb%20Fundamentals%3EClimb%20Fundamentals), [Apex engine movement measurements](https://apexmovement.tech/wiki/articles/Wiki%20help%3EHow%20the%20Apex%20engine%20handles%20Movement), and [collected velocities](https://apexmovement.tech/wiki/articles/Wiki%20help%3EVelocities%20collected%20in%20one%20place). Research uses 1 Hammer unit = 0.0254 metres.

| Parameter | Default |
| --- | --- |
| Initial climb speed | 5.715 m/s (225 hu/s) |
| Facing limit | Dot product 0.7 (about 45.57 degrees) |
| Upward climb cutoff above the grounded baseline | 3.7338 m (147 hu) |
| Final upward boost | 0.7112 m (28 hu) |
| Maximum ledge including mantle | 6.9342 m (273 hu) |
| Detach penalty on remaining climb space | 3.2512 m (128 hu) |

These reproduce the normal climb's broad behavior and measured limits, rather than claiming an exact copy of Apex's proprietary controller. Slowdown, reattachment delay, side speed, hand animation, and mantle timing are project tuning. Advanced zone-dependent bounce penalties, analog climb locks, and legend-specific abilities are not reproduced. This project's existing gravity, jump height, and sideways wall run also affect the overall feel.
