# PROJECT MIO

Mobile-first casual social world-building game.

**Current milestone: M0.2 — FLOW.**

M0.1 delivered the shared architecture. M0.2 adds the first real prototype,
**FLOW**, on top of it: hold a finger and steer a stream through orbs and
blocks. The M0.1 test harness remains alongside it as an architecture check.

There is no town, no MIO companion, no social systems and no backend. That is
deliberate.

---

## What is here

| Layer | Assembly | Contents |
|---|---|---|
| Core | `Mio.Core` | Rules contract, session runner, metrics, economy, profile — **pure C#, no `UnityEngine`** |
| Unity | `Mio.Unity` | Input routing, feedback, views, HUD, composition root |
| Editor | `Mio.Editor` | Project and scene generation |
| Tests | `Mio.Tests.EditMode` | 97 tests |

### The test harness

`TestRuleset` is **not a game and must never ship.** It exists to exercise every
seam of the foundation exactly once: tap a square five times. Doing so proves
that normalised input arrives, actions are counted, semantic cues are emitted,
score and progress rise, a reward is evaluated, the wallet persists it and a
metric report comes out the other end.

---

## Opening the project

Requires **Unity 6** (6000.0.x).

**Testing this for the first time, or handing it to someone who does not use
Unity? Use [`FIRST_TEST.md`](FIRST_TEST.md) instead of this section.**

1. Open this folder as a Unity project.
2. **PROJECT MIO ▸ Setup Test Environment** — creates and repairs the tuning
   assets, the scenes, the EventSystem and the build scene list. Idempotent:
   safe to re-run, never overwrites tuning.
3. **PROJECT MIO ▸ Validate Project** — preflight check. Ends with
   `PROJECT MIO READY TO TEST` or `PROJECT MIO NOT READY` plus the exact
   corrective actions.
4. **PROJECT MIO ▸ Open Harness Test** — opens the scene. Press Play.

| Menu | Purpose |
|---|---|
| `Setup Test Environment` | Create/repair everything needed to run |
| `Validate Project` | Preflight report with corrective actions |
| `Open Harness Test` | Open the M0.1 harness scene |
| `Open FLOW Test` | Open the FLOW scene |
| `Advanced ▸ Reset Player Wallet` | Clear the saved balance |
| `Advanced ▸ Open Metrics Folder` | Reveal `mio-metrics.jsonl` |
| `Advanced ▸ Apply Mobile Player Settings` | Portrait-first mobile defaults |

Scenes are discovered from a catalogue keyed by type name, so the FLOW scene
and its menu start working automatically once `Mio.Core.Flow.FlowRules` and
`Mio.Unity.Config.FlowConfigAsset` exist. No tooling edits needed.

Scenes are generated rather than committed: a scene is a camera plus one
`PrototypeBootstrap` component, and the whole game is built in code at runtime.
That keeps everything diffable and mergeable instead of buried in scene YAML.

> On first open Unity generates `.meta` files and the rest of
> `ProjectSettings/`. Commit those — they keep asset references stable.

---

## Architecture

The one rule that shapes everything:

> **Gameplay logic never references `UnityEngine`.**

`Mio.Core` is marked `noEngineReferences`, so the compiler enforces it. A `using
UnityEngine;` in a Core file fails the build — in CI, in seconds, with no Unity
licence. That single constraint buys:

- **Testable rules.** 97 tests run in under a second on a plain .NET runner.
- **Reproducible sessions.** A rule set is deterministic given a seed and a
  `(deltaTime, input)` sequence, so the same seed behaves identically in the
  editor, on a device and in CI.
- **UI separated from game logic** by construction, not by convention.
- **No accidental `PlayerPrefs` access.** Gameplay literally cannot see it.

### The seams

| Seam | Responsibility |
|---|---|
| `IPrototypeRules` | Everything a rule set must implement: `Id`, `Status`, `Score`, `Progress01`, `SuccessfulActions`, `FailedActions`, plus `Begin`/`Tick`/`HandleInput`. |
| `PrototypeRunner` | Drives any rule set; owns the seed, session timing, metrics, reward evaluation and wallet deposit. |
| `InputCommand` | One finger, normalised to the play field. **Rules never see screen pixels.** |
| `FeedbackCue` | Rules emit *semantic* cues only. They may never name an audio clip, particle prefab, animation or vibration. |
| `RewardTable` | `Evaluate(status, score) → ResourceBundle`, from pure data. No economy value is hard-coded in rules. |
| `IProfileStore` | Persistence behind an interface. `PlayerPrefs` is **temporary prototype storage** and is touched by exactly one class. |

### Tuning

Every number a designer might change lives in a ScriptableObject under
`Assets/Mio/Settings/`: `TestRulesetConfig`, `RewardTable`, `Palette`,
`FeedbackProfile`.

---

## Running the tests

```bash
dotnet test tools/MioCore.Tests/MioCore.Tests.csproj
```

`tools/` compiles the *same source files* Unity compiles — nothing is copied, so
the suites cannot drift. In the editor the same tests run under
**Window ▸ General ▸ Test Runner ▸ EditMode**.

`tools/MioUnity.Verify` additionally compiles the Unity and Editor assemblies
against stubbed Unity APIs. That does not prove the real API is used correctly —
only the editor does that — but it catches typos and broken calls between our
own classes in seconds rather than after a full import.

CI runs both on every push.

---

## Roadmap

| Milestone | State |
|---|---|
| **M0.1** Shared foundation + test harness | done |
| **M0.2** FLOW | done — POP CHAIN and PACK not started |
| M1 | town, MIO, social — not started |

POP CHAIN and PACK are specified in [`docs/DESIGN.md`](docs/DESIGN.md) and
have an earlier exploratory implementation in git history at `f6645b3`, which
predates the M0.1 contract and needs the same adaptation FLOW received.
