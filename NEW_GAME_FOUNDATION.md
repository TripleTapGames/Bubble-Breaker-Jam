# New game foundation

## Playable sample

GameScene enables GameManager's Load Sample Level On Start option and assigns the existing tray prefab. Press Play to generate nine linked balls (three red, three blue, three yellow) and three matching capacity-three trays at the assigned tray column anchors. Pickup positions are sampled from the conveyor spline and landing positions are generated inside each tray. Temporary visible funnel walls are generated for the sample.

Temporary interaction: click/tap a leaf ball (one with no remaining children) to release it. Work upward to clear the branch. This is a test rule, not the final branch mechanic. The on-screen Retry button rebuilds the sample after clearing all pooled balls. Disable Load Sample Level On Start to use another level builder.

The sample is authored in GameManager.SampleLevel.cs, separate from the shared pipeline. It currently uses fixed positions and balanced colors rather than a general level-data authoring system.

This project uses `NEW_GAME_HANDOFF.md` as the behavioral reference for the shared ball pipeline.

## Confirmed direction

- The upper playfield is branch-shaped, with linked balls rather than a grid.
- Branch construction, link rules, exposure rules, input, and release conditions are intentionally pending.
- Falling physics, funnel extraction, conveyor capacity/seating, matching tray collection, retry cleanup, deadlock loss, and guarded fast finish follow the handoff.
- Tutorials, analytics, monetization, and the old grid schema are not inherited requirements.

## Branch integration contract

The future branch builder should use `BranchSpawnController` only at the shared-system boundary:

1. Call `CreateLinkedBall(color, worldPosition, linkParent)` for each authored/generated branch ball.
2. Keep branch/link visuals and exposure logic in the future branch system.
3. Call `ReleaseBall(...)` for an immediate release, or `ScheduleRelease(...)` for a staggered release.
4. Do not directly change Rigidbody2D settings; `Ball.Release` transfers ownership from the branch to falling physics.
5. On retry, `GameManager.RetryLevel` cancels delayed releases, clears belt/trays/funnel, releases every active pooled ball, and returns to `Ready`.

`BranchSpawnController` does not currently auto-spawn anything. This prevents a temporary random implementation from becoming an accidental design constraint.

## Shared runtime invariants

- A ball has one movement owner, represented by `BallState`.
- Funnel waiting balls remain dynamic and solid; only the selected extraction ball becomes kinematic/trigger.
- Belt occupancy counts only attached balls. Seating is animated and only fully seated balls can be collected.
- Trays reserve capacity at acceptance time and resolve moving landing targets every frame.
- A tray column accepts only its front stage and rejects intake while advancing.
- Fast finish requires no unreleased/scheduled/falling/funnel/seating balls, an immediately collectable ball, and an exact queued-color match for all remaining tray stages.

## Centralized scene wiring

`GameManager` is the scene composition root. Assign `BallPool`, `BranchSpawnController`, `FunnelController`, `ConveyorBelt`, `SplineConveyorBelt2D`, and the complete `TrayColumn` array only on `GameManager`. At startup it injects those shared dependencies, subscribes to trays, and runs the gameplay update loop. It does not search the hierarchy.

No controller-to-controller Inspector references are required. Authored local references still belong to their owner: the funnel exit belongs to `FunnelController`, while pickup bands, tray roots, and landing slots belong to each `TrayColumn`.

If tray columns are created or replaced at runtime, call `GameManager.SetTrayColumns(newColumns)`.

## Scene work still pending

The current `GameScene` contains the board and spline belt presentation. Add the new shared controllers, assign them once on `GameManager`, and configure the authored funnel geometry, funnel exit, pickup bands, landing slots, tray stages, and ball prefab. The upper branch objects should wait until the exact branch mechanism is specified.
