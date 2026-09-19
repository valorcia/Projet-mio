# PROJECT MIO

Mobile-first casual social world-building game.

**Current milestone: M0.1 — Shared prototype foundation.**

M0.1 delivers the shared architecture that all gameplay prototypes will sit on,
and nothing else. There is **no gameplay** in this milestone: the only rule set
in the repository is a deliberately boring test harness whose sole job is to
prove the foundation works end to end.

There is no town, no MIO companion, no social systems and no backend. That is
deliberate.

---

## What is here

| Layer | Assembly | Contents |
|---|---|---|
| Core | `Mio.Core` | Rules contract, session runner, metrics, economy, profile — **pure C#, no `UnityEngine`** |
| Unity | `Mio.Unity` | Input routing, feedback, views, HUD, composition root |
| Editor | `Mio.Editor` | Project and scene generation |
| Tests | `Mio.Tests.EditMode` | 75 tests |

### The test harness

`TestRuleset` is **not a game and must never ship.** It exists to exercise every
seam of the foundation exactly once: tap a square five times. Doing so proves
that normalised input arrives, actions are counted, semantic cues are emitted,
score and progress rise, a reward is evaluated, the wallet persists it and a
metric report comes out the other end.

---

## Opening the project

Requires **Unity 6** (6000.0.x).

1. Open this folder as a Unity project.
2. Run **Tools ▸ MIO ▸ Set Up Project** once. This creates the tuning assets,
   generates `Assets/Mio/Scenes/M0_TestHarness.unity`, and applies
   portrait-first mobile player settings.
3. Open that scene and press Play. Tap the square.

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

- **Testable rules.** 75 tests run in under a second on a plain .NET runner.
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
| **M0.1** Shared foundation + test harness | this milestone |
| **M0.2** FLOW, POP CHAIN, PACK | not started — spec in [`docs/DESIGN.md`](docs/DESIGN.md) |
| M1 | town, MIO, social — not started |

An earlier exploratory implementation of the three prototypes exists in git
history at commit `f6645b3` and can be restored when M0.2 begins. It predates
the M0.1 contract and would need updating.
