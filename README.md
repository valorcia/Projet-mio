# PROJECT MIO

Mobile-first casual social world-building game.

**Current milestone: M0 — Core Fun Prototype.**

The M0 success criterion is that we can put three builds on a phone and hand
them to someone without explaining the controls. Everything in this repository
serves that and nothing else. **There is no town yet, and that is deliberate.**

---

## What is here

Three isolated gameplay prototypes:

| | Prototype | Verb | Run length |
|---|---|---|---|
| **A** | **FLOW** | hold and steer | ~45 s |
| **B** | **POP CHAIN** | tap | ~45 s |
| **C** | **PACK** | drag | ~60 s |

Each one starts immediately, is played with one finger, needs no written
tutorial, gives clear win/fail feedback, awards three placeholder resources,
and logs the M0 metric set.

See [`docs/DESIGN.md`](docs/DESIGN.md) for what each prototype actually does and
**which parts of it were an interpretation that needs your sign-off**, and
[`docs/METRICS.md`](docs/METRICS.md) for the data each run produces.

---

## Opening the project

Requires **Unity 6** (6000.0.x).

1. Open this folder as a Unity project.
2. Run **Tools ▸ MIO ▸ Set Up Project** once.
   This creates the tuning assets, generates the three scenes into
   `Assets/Mio/Scenes/`, and applies portrait-first mobile player settings.
3. Open `Assets/Mio/Scenes/A_Flow.unity` (or `B_PopChain`, `C_Pack`) and press
   Play.

The scenes are generated rather than committed. A prototype scene is a camera
and one `PrototypeBootstrap` component; the entire game is built in code at
runtime. That keeps the prototypes diffable and mergeable instead of buried in
scene YAML, and means they can be regenerated after any refactor.

> On first open Unity will generate `.meta` files and the rest of
> `ProjectSettings/`. Commit those — they keep asset references stable from then
> on.

---

## Architecture

The one rule that shapes everything:

> **Gameplay logic never references `UnityEngine`.**

```
Assets/Mio/
  Core/      Mio.Core       pure C#, no engine — rules, metrics, economy
  Unity/     Mio.Unity      presentation only — views, input, juice, HUD
  Editor/    Mio.Editor     project + scene generation
  Tests/     Mio.Tests.EditMode
```

`Mio.Core` is marked `noEngineReferences`, so the compiler enforces the rule.

Why it is worth the discipline:

- **The rules are testable.** 93 tests run in under a second on a plain .NET
  runner, with no Unity licence and no editor.
- **Runs are reproducible.** A prototype is deterministic given a seed and a
  `(deltaTime, input)` sequence. The same seed gives the same board in the
  editor, on device and in CI, so two tunings can be A/B compared on an
  identical layout.
- **UI is separated from game logic**, as the brief requires, by construction
  rather than by convention.

### The seams

| Seam | What it does |
|---|---|
| `IPrototypeRules` | Everything a prototype must implement. Three implementations, one contract. |
| `PrototypeRunner` | Drives any rules object, owns metrics and payout, so all three report identical numbers. |
| `FeedbackCue` | Rules say *what happened*; the Unity layer decides how it looks, sounds and feels. |
| `InputCommand` | One finger, normalised to the play field, so tuning is resolution independent. |
| `RewardTable` | Outcome → resources, from pure data. No economy value is hard-coded in gameplay. |
| `IProfileStore` | Save behind an interface. Today PlayerPrefs; tomorrow whatever the town needs. |

### Tuning

Every number a designer might want to change lives in a ScriptableObject under
`Assets/Mio/Settings/`:

- `FlowConfig`, `PopChainConfig`, `PackConfig` — gameplay parameters
- `RewardTable` — the payout curve
- `Palette` — all placeholder art, since M0 draws itself from flat rectangles
- `FeedbackProfile` — particles, shake, punch, sound clips and haptic strength
  per cue

Nothing in `Assets/Mio/Core` reads a magic number that is not in one of these.

---

## Running the tests

```bash
dotnet test tools/MioCore.Tests/MioCore.Tests.csproj
```

`tools/` compiles the *same source files* Unity compiles — nothing is copied or
mirrored, so the suites cannot drift apart. In the editor the same tests run
under **Window ▸ General ▸ Test Runner ▸ EditMode**.

`tools/MioUnity.Verify` additionally compiles the Unity and Editor assemblies
against stubbed Unity APIs. That does not prove the real API is used correctly —
only the editor does that — but it catches typos, renamed methods and broken
calls between our own classes in seconds rather than after a full import.

CI runs both on every push.

---

## Working agreement

From the project brief, and worth keeping visible:

- Explain the implementation before building it; list files, dependencies and
  risks.
- Build the smallest functional version, test it, report it.
- **Do not expand scope without explicit instruction.**
- No new major game features without approval.
- When uncertain, preserve modularity rather than adding complexity.
