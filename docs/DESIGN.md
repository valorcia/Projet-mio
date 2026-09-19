# M0 prototype designs

## ⚠️ Read this first — these are interpretations

The brief named the three prototypes **FLOW**, **POP CHAIN** and **PACK** but
did not specify their mechanics. Rather than block, each has been built to the
most standard reading of its name, with every parameter exposed as data.

**Each prototype's mechanic is one file.** If a reading is wrong, that
prototype's rules file is rewritten and nothing else in the project changes —
the runner, metrics, economy, input, feedback and views all stay as they are.
That is the main reason the architecture is shaped the way it is.

Please confirm or correct the three readings below before we tune anything.

---

## Shared rules

All three prototypes:

- start the instant the scene opens — no menu, no start button
- are played with **one finger**, with any second touch ignored outright
- run on a countdown, and win by filling a meter before it expires
- resolve to `Won`, `Lost` or `Abandoned`
- pay out via the shared `RewardTable`, where `Abandoned` pays nothing so
  quitting a bad run is never a strategy
- log the same metric set

The play field is a fixed **9:16 box letterboxed into the screen**. All
gameplay coordinates are normalised to it, so a tall phone changes the
letterbox and nothing else. A tuning value means the same thing on every
device.

---

## A — FLOW

**Verb: hold and steer.** ~45 seconds.

A stream head sits at a fixed height and chases your finger left and right. A
track of **orbs** and **blocks** scrolls down to meet it. Pass through an orb to
fill the meter; slide around a block.

- Orb taken → score, meter up
- Orb missed → small meter penalty
- Block struck → larger meter penalty
- Meter full → **win**. Clock expires → **lose**.

The head *chases* the finger rather than snapping to it, which is what makes
the stream feel like it has weight. Lifting your finger leaves it where it is
rather than recentring, so a re-grip is forgiving.

**Fairness guarantee.** The whole track is generated up front from the seed,
and each gate is placed within reach of the previous one, given the head's
speed and the gap between gates. A test plays 40 seeds perfectly and asserts
zero misses. This matters because an unwinnable track would make a playtest
fail for a reason that has nothing to do with whether the design is fun.

**Why it should read without a tutorial:** the stream is already following your
thumb the moment you touch the screen, and that is the entire control scheme.

---

## B — POP CHAIN

**Verb: tap.** ~45 seconds.

A grid of coloured blobs. Tap any blob touching a twin: the whole connected
group bursts, the board falls in, and new blobs drop from the top.

- Group of 2+ → pops. Score scales with group size **and** the chain
  multiplier.
- Pop again within the chain window → multiplier steps up (capped)
- Let the window lapse → multiplier resets to 1
- Target score → **win**. Clock expires → **lose**.

The chain window is the whole game: it rewards reading the board ahead instead
of tapping at random, and it is the single number most worth A/B testing.

**No dead ends, ever.** The board refills after every pop, and if a refill
happens to leave no legal move it reshuffles. A player can never be stranded
looking at a grid they cannot play.

**Why it should read without a tutorial:** the board is always full, always has
a legal move, and the first tap either bursts something or wobbles. There is one
verb and it is the most obvious one on a touchscreen.

---

## C — PACK

**Verb: drag.** ~60 seconds.

A container grid and a tray of three pieces. Drag a piece onto the grid. Fill a
whole row or column and it clears.

- Legal drop → score per cell covered
- Row and/or column completed → line bonus, plus a combo bonus per extra line
- Illegal drop **on the board** → rejected, piece returns to the tray
- Releasing back over the tray → cancel, **not** counted as a mistake
- Tray refills only once all three pieces are spent
- Target score → **win**. Clock expires, or no tray piece fits anywhere →
  **lose**.

The held piece floats **above** the finger and previews its landing spot as a
ghost, green when it will land and red when it will not. That colour swap is
the entire rules explanation.

Two behaviours worth knowing:

- A row and a column that intersect **both** clear. Both are detected before
  either is cleared, so the shared cell does not make the second look
  incomplete.
- Filling the board completely is a jackpot, not a loss: every row and column
  completes at once and the board empties. This emerged from the rules rather
  than being designed, and there is a test pinning it so it cannot regress
  silently.

**Why it should read without a tutorial:** drag-and-drop with a live ghost is
self-teaching. You learn "it has to fit" by watching the ghost turn red, not by
reading it.

---

## Feel

Feedback is data, not code. Rules emit a `FeedbackCue` saying *what happened*
and how big it was; `FeedbackProfile` maps each cue to particles, screen shake,
a scale punch, a sound and a haptic strength. Feel can be retuned live in the
inspector during a playtest without any risk of changing the simulation.

Defaults are set so the prototypes feel alive before anyone has authored a
single sound, and so that **failure is quiet and gentle** — no clip assigned is
simply silent, and the failure cues are deliberately small. This is a game an
eight-year-old should not feel told off by.

Sound and haptics are wired as **hooks**. Clips are optional and unassigned by
default. Haptics are throttled and, since Unity only offers one blunt
`Vibrate()` call, light cues are currently suppressed rather than turning the
phone into a buzzer; when a real haptics plugin lands, one file changes.

---

## Open questions for sign-off

1. **Are the three mechanics above the intended readings?** This is the one
   that matters. Everything else is a number.
2. Placeholder resources are currently `Energy`, `Material`, `Coin` — generic
   on purpose. Do you want thematic names now, or keep them neutral until the
   town exists?
3. Should a lost run pay out at all? It currently pays participation, on the
   theory that a 45-second run should never feel like wasted time.
4. Target run lengths are 45/45/60 s against the brief's 30–60 s. PACK is over
   because a 60-second board gives a more legible dead-end. Worth pulling back?
