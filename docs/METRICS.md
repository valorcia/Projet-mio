# Metrics

Every finished session produces one `MetricReport`. All rule sets share
`PrototypeRunner`, so these fields mean exactly the same thing in every data
set — without that, comparing two prototypes against each other would be
meaningless, which is the entire point of M0.

## Wire format

One JSON object per line (JSONL), appended to
`Application.persistentDataPath/mio-metrics.jsonl`.

| Wire field | C# field | Meaning |
|---|---|---|
| `session_id` | `SessionId` | GUID, unique per session. Groups replays of one sitting. |
| `prototype_id` | `PrototypeId` | Which rule set produced this. |
| `seed` | `Seed` | Replays the exact session. |
| `session_start` | `SessionStartUtc` | ISO-8601 UTC. |
| `session_end` | `SessionEndUtc` | ISO-8601 UTC. |
| `session_duration` | `SessionDuration` | Seconds of *simulated* play. |
| `time_to_first_input` | `TimeToFirstInput` | Seconds to the first press. `-1` if never touched. |
| `input_count` | `InputCount` | Distinct touches. |
| `successful_actions` | `SuccessfulActions` | Decisions that worked. |
| `failed_actions` | `FailedActions` | Decisions that did not. |
| `score` | `Score` | Final score. |
| `progress` | `Progress` | Win-condition fill at resolution, 0..1. |
| `completion_status` | `CompletionStatus` | `Won` / `Lost` / `Abandoned`. |
| `replay_requested` | `ReplayRequested` | This session was started by a replay request. |
| `attempt_index` | `AttemptIndex` | 0 for the first session of a sitting, 1+ after. |
| `rewards` | `Rewards` | Nested object: `energy`, `material`, `coin`. |

## The two that decide M0

**`time_to_first_input`** is the headline number. M0 passes if a stranger can
play without being told the controls, so time to first touch is the closest
proxy we have for "did the screen explain itself". A session where it is `-1`
is a prototype that failed outright.

**`replay_requested`** is the fun signal. Nobody replays something they did not
enjoy. Replay rate per prototype is the cheapest read on which design is worth
carrying forward.

## Counting rules

Chosen deliberately, because a sloppy definition poisons the success rate:

- **`session_duration` is accumulated from `deltaTime`,** not read off the wall
  clock, so a frame hitch or an editor breakpoint cannot corrupt it.
  `session_start`/`session_end` are wall clock, for ordering the log only.
- **`input_count` counts presses.** One drag is one input, not the hundred move
  events it generates. A release expresses no new intention.
- **Only a press sets `time_to_first_input`.** Moves and releases from a
  cancelled touch do not.
- **A slip is not a failed action.** A touch outside the play area, or a
  cancelled drag, expresses no decision, so rule sets must not count it.
- **Restarting mid-session records `Abandoned`** rather than dropping the data
  point, so a sitting has no silent holes.
- **Backgrounding the app abandons the session,** so a phone left in a pocket
  cannot bank a bogus twenty-minute session.
- **`Abandoned` pays no resources**, or quitting a bad session would be a
  farming strategy.

## Sinks

| Sink | Use |
|---|---|
| `ConsoleMetricsSink` | `Debug.Log`, for the editor. |
| `JsonlMetricsSink` | Newline-delimited JSON to `persistentDataPath`. |
| `CompositeMetricsSink` | Fans out to several. |
| `InMemoryMetricsSink` | Tests. |
| `NullMetricsSink` | Default, discards. |

JSONL rather than a JSON array so a crashed or force-quit session still leaves
every completed session readable — which matters when the whole point is
collecting data off a playtester's phone. Numbers are written with invariant
culture, so a French-locale device cannot emit `1,25` and break the parse.

If the file cannot be written, the sink disables itself and logs a warning.
Telemetry must never take gameplay down with it.

Disable file logging per scene with **Log Metrics To File** on the bootstrap.

## A/B testing a tuning change

1. On the config asset, turn **off** `Randomise Seed` and set a `Fixed Seed`.
2. Play a few sessions, change one number, play the same seed again.

Because the simulation is deterministic, both sets run on an identical session,
so the difference in the numbers is the tuning change and nothing else.
