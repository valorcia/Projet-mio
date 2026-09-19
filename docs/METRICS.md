# M0 metrics

Every finished attempt produces one `MetricReport`. All three prototypes share
`PrototypeRunner`, so these fields mean exactly the same thing in all three data
sets — without that, comparing the prototypes against each other would be
meaningless, which is the entire point of M0.

## The required set

| Field | Meaning |
|---|---|
| `SessionDuration` | Seconds from run start to resolution. |
| `FirstInteractionTime` | Seconds from start to the **first press**. `-1` if the player never touched the screen. |
| `SuccessfulActions` | Player decisions that worked. |
| `FailedActions` | Player decisions that did not. |
| `Completed` | Whether the run was won. |
| `AttemptIndex` / `IsReplay` | `0` for the first run of a session, `1+` for each replay. |
| `Score` | Final score. |

Plus `Prototype`, `Seed`, `Status`, `Rewards` and a per-prototype `Custom` bag.

## The two that decide M0

**`FirstInteractionTime`** is the headline number. M0 passes if a stranger can
play without being told the controls, so *time to first touch* is the closest
proxy we have for "did the screen explain itself". A run where
`HadInteraction` is false is a prototype that failed outright.

**`IsReplay`** is the fun signal. Nobody replays something they did not enjoy.
Replay rate per prototype is the cheapest read on which of the three is worth
carrying into M1.

## Counting rules

These were chosen carefully, because a sloppy definition poisons the success
rate:

- Only a **press** sets `FirstInteractionTime`. A stray move or release from a
  cancelled touch is not an intent to play.
- A tap that misses the board entirely is **not** a failed action. Missing the
  grid is a slip, not a wrong decision.
- In PACK, releasing a piece back over the tray is a **cancel**, not a failure.
  Changing your mind is not a mistake.
- Restarting mid-run records the interrupted attempt as `Abandoned` rather than
  dropping it, so a session's data has no silent holes.
- Backgrounding the app mid-run abandons it, so a phone left in a pocket cannot
  bank a bogus twenty-minute session.
- `Abandoned` runs pay no resources.

## Custom counters

| Prototype | Counters |
|---|---|
| FLOW | `collected`, `missed`, `hazardsHit`, `progress`, `gatesTotal` |
| POP CHAIN | `pops`, `badTaps`, `cellsCleared`, `longestChain`, `avgGroupSize` |
| PACK | `placements`, `rejected`, `linesCleared`, `bestCombo`, `occupancy` |

## Getting the data off a device

Reports go to two sinks by default:

- **Console** — `Debug.Log`, for the editor.
- **JSON file** — one JSON object per line, appended to
  `Application.persistentDataPath/mio-metrics.jsonl`.

Newline-delimited JSON rather than a single array, so a crashed or force-quit
session still leaves every completed attempt readable. That matters when the
whole point is collecting data off a playtester's phone. Numbers are written
with invariant culture, so a French-locale device cannot emit `1,25` and break
the parse.

If the file cannot be written the sink disables itself and logs a warning.
Telemetry must never take gameplay down with it.

Disable file logging per scene with `Log Metrics To File` on the bootstrap.

## A/B testing a tuning change

1. On the prototype's config asset, turn **off** `Randomise Seed` and set a
   `Fixed Seed`.
2. Play a few runs, change one number, play the same seed again.

Because the simulation is deterministic, both sets run on an identical board or
track, so the difference in the numbers is the tuning change and nothing else.
