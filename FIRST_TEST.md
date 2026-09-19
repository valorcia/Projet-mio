# PROJECT MIO — First test

Instructions for running PROJECT MIO for the first time. **No Unity experience
needed.** Follow the steps in order.

---

## ⚠️ What exists right now

This build contains the **shared foundation (M0.1)** and a **test harness**.

**The three games — FLOW, POP CHAIN and PACK — are not built yet.**

The test harness is deliberately boring: *tap a square five times.* It is not a
game and it is not meant to be fun. Its only job is to prove the plumbing works
— that your finger is registered, that the screen reacts, that a score is kept,
that a reward is paid and saved, and that the session is logged.

**What you are testing: does the machinery work?** Not: is it fun?

---

## 1. Open the project

1. Install **Unity Hub** → <https://unity.com/download>
2. In Unity Hub, open the **Installs** tab and install **Unity 6** (any
   `6000.0.x` version).
   - On Mac, when asked which modules to add, tick **iOS Build Support**.
3. Go to the **Projects** tab → **Add** → **Add project from disk**.
4. Select the folder containing this file, then click it to open.

The first open takes **5–15 minutes**. Unity is importing the project. A
progress bar that appears stuck is normal. Let it finish.

> If a window appears asking about "API Update" or "Enter Safe Mode", choose
> **No** / **Ignore**, and tell the developer.

---

## 2. Click one menu

At the top of the screen, in the menu bar, click:

### `PROJECT MIO` → `Setup Test Environment`

That is the whole setup. It creates everything needed and repairs anything
missing. **It is safe to run again at any time** — it never overwrites work.

A dialog appears when it finishes. If it says *READY TO TEST*, go to step 4.

---

## 3. Run the validation check

### `PROJECT MIO` → `Validate Project`

A dialog tells you the result, and the full report appears in the **Console**
(bottom of the screen; if you cannot see it, use `Window ▸ General ▸ Console`).

The report ends with one of two lines:

| Result | What to do |
|---|---|
| `PROJECT MIO READY TO TEST` | Continue to step 4. |
| `PROJECT MIO NOT READY` | The report lists numbered corrective actions. Do them, then run validation again. |

Most problems are fixed by running `Setup Test Environment` once more. If a
problem mentions **compile errors**, stop and send the Console text to the
developer — that is not something to fix from a menu.

Lines marked `[skip]` are **not errors.** They mean "not built yet" — FLOW will
show as skipped until it exists.

---

## 4. Launch the Harness

### `PROJECT MIO` → `Open Harness Test`

Then press the **▶ Play button** at the top-centre of the screen.

Press **▶ again** to stop.

> Tip: click the **Game** tab and set the aspect dropdown to a portrait phone
> ratio (e.g. `9:16`) so it looks like a phone.

---

## 5. Launch FLOW

### `PROJECT MIO` → `Open FLOW Test`

**This menu is greyed out and cannot be clicked yet.** That is correct and
expected: FLOW does not exist in this build.

The moment FLOW is implemented, this menu becomes clickable and opens the FLOW
scene, with no setup needed from you.

---

## 6. What success looks like

In the Harness, tapping (or clicking) the square should give you **all** of
this:

| You do | You should see |
|---|---|
| Look at the screen | A coloured square that gently pulses |
| **Click the square** | It pops, particles burst out, the square jumps to a new place |
| Watch the top bar | The bar fills a bit more with each hit |
| **Click away from the square** | A small, gentle wobble. Nothing harsh |
| Hit the square **5 times** | A dark panel: **DONE!**, your score, and the reward earned |
| Tap anywhere on that panel | A new session starts immediately |
| Look at the **top right** | Three numbers — your wallet. These **grow** each time you win |
| Stop Play, press Play again | The wallet numbers are **still there** |

That last row is the most important one: it proves the save system works.

**Something is wrong if:**

- Clicking does nothing at all → input is broken
- No particles or movement → feedback is broken
- The wallet resets to `0 / 0 / 0` after restarting → saving is broken
- Red messages in the Console

Report any of these, and copy the red Console text with them.

---

## 7. Run the automated tests

### `Window` → `General` → `Test Runner`

In the window that opens, click the **EditMode** tab, then **Run All**.

**Expected: 82 tests, all green.** It takes a couple of seconds.

A red test is a real failure — send a screenshot to the developer.

---

## 8. Make an iOS development build

You need a **Mac**, **Xcode** installed, and an Apple ID.

**In Unity:**

1. `File` → `Build Profiles` (in older versions: `Build Settings`)
2. Select **iOS** in the platform list → **Switch Platform**
   (this takes several minutes the first time)
3. Confirm the scene list contains `M0_TestHarness` and that it is ticked.
   If not, run `PROJECT MIO ▸ Setup Test Environment` again.
4. Tick **Development Build**
5. Click **Build**, and choose a new empty folder, e.g. `Builds/iOS`

Unity produces an **Xcode project**, not an app. Then:

**In Xcode:**

6. Open `Unity-iPhone.xcodeproj` from the folder you chose
7. Select the **Unity-iPhone** target → **Signing & Capabilities** tab
8. Tick **Automatically manage signing**, and pick your Apple ID under **Team**
   (add it via `Xcode ▸ Settings ▸ Accounts` if the list is empty)
9. Plug in your iPhone and select it at the top of the window
10. Press **▶ Run**

**On the iPhone, the first time:** the app will refuse to open. Go to
`Settings ▸ General ▸ VPN & Device Management`, tap your Apple ID, and tap
**Trust**. Then open the app again.

> A free Apple ID build **stops working after 7 days**. Rebuild to renew it.
> This is an Apple restriction, not a bug.

---

## 9. Where to find metrics and logs

Every finished session writes one line to a file called **`mio-metrics.jsonl`**.

**Quickest way:** `PROJECT MIO` → `Advanced` → `Open Metrics Folder`
(this opens the folder in Finder/Explorer).

Manually:

| Where you played | Location |
|---|---|
| Unity on Mac | `~/Library/Application Support/Valorcia/PROJECT MIO/` |
| Unity on Windows | `%USERPROFILE%\AppData\LocalLow\Valorcia\PROJECT MIO\` |
| iPhone | Xcode → `Window ▸ Devices and Simulators` → select the device → select the app → gear icon → **Download Container** |

One line per session. Each line looks roughly like:

```json
{"session_id":"a3f...","prototype_id":"TestHarness","session_duration":8.4,
 "time_to_first_input":1.2,"input_count":6,"successful_actions":5,
 "failed_actions":1,"score":500,"completion_status":"Won","replay_requested":false, ...}
```

The two numbers that matter most:

- **`time_to_first_input`** — how many seconds before the tester touched the
  screen. This tells us whether the screen explained itself. `-1` means they
  never touched it at all, which would be a total failure.
- **`replay_requested`** — whether they chose to play again. Nobody replays
  something they did not enjoy.

**Live logs** while playing in Unity are in the **Console**
(`Window ▸ General ▸ Console`). Each finished session prints a
`[MIO metrics]` line.

---

## Useful extras

| Menu | What it does |
|---|---|
| `PROJECT MIO ▸ Advanced ▸ Reset Player Wallet` | Clears the saved balance, to test a first-run experience |
| `PROJECT MIO ▸ Advanced ▸ Open Metrics Folder` | Opens the folder containing the session log |
| `PROJECT MIO ▸ Advanced ▸ Apply Mobile Player Settings` | Re-applies portrait orientation and mobile defaults |

---

## If you get stuck

Send the developer:

1. Which step number you were on
2. A screenshot of the screen
3. The **red** text from the Console (`Window ▸ General ▸ Console`)
4. The output of `PROJECT MIO ▸ Validate Project`
