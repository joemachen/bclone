# QA checklist: Phase 5 — the walk that closes the phase

**Decisions:** D641 (the gate, item 3), D744 (this checklist, `phase-5-exit-gate.md §5`). D164/D168: the document and
the walk are different items — **only Joe's walk closes item 3.**
**Status:** 📝 **WRITTEN (D744, 2026-11-14); ⏸️ NOT WALKED.** Joe walks it; his walk is also item 5's played run (its
log is scanned for `ERROR` afterwards) and house tiers piece 5's play.

---

## How to walk it

- **Start:** `.\run.bat` in PowerShell. The game writes its own log the moment it opens — nothing to switch on. When you
  are done, just quit the game normally; Claude reads the newest logs in `src/Bclone.Game/logs/` and `logs/`.
- **Two runs cover everything.** **Run A** is a new game you play forward from the founding (sections 1–4). **Run B**
  is one of your old late saves — your Hollowmere (Year 84, both houses learned) is ideal — for what takes decades to
  reach (sections 5–6). Fast-forward freely; nothing here needs watching in real time.
- **Each line:** *do this* → *you should see this*. Tick it, or write one sentence of what you saw instead. A ❌ with a
  sentence is as useful as a ✅ — it is what the walk is for.
- **Not on this list on purpose:** how the hills, grass and trees *look* (the art lane's), and anything from Phase 4.5's
  leftovers (full settings, ironman).

---

## Run A — a new game

### 1. The new-game screen and the founding (Years 1–3)

- [ ] **New game → Hilliness: *Hilly*.** The three choices are there (*Gentle*, *Rolling*, *Hilly*), *Rolling* picked to
  begin with. The preview changes when you change it.
- [ ] **Found the valley.** A cart, a pile, people. The first huts go up.
- [ ] **Click a hillside tile** (*What's here*). It says the slope — *"A gentle slope — a little slower to cross"* or *"Steep
  ground — slow to cross, up or down"*.
- [ ] **Watch someone walk across steep ground** and open their card. It says *climbing* where it would say *walking*.
- [ ] **Mark a builder's hut on wooded ground** (try it at an angle). Its outline shows straight away while the trees
  are felled under it, and it goes up when they are (D726).
- [ ] **Open Settings → turn on *Build queue*.** A window lists every site waiting, the next one at the top, with its
  materials. **▲ and ▼** move a site up and down, and the site's own card agrees with the new place. Turn it off again
  and the window goes (D730).

### 2. The first stores and the first winter (Years 1–5)

- [ ] **A granary goes up; open a family's home card.** The line about food now names **where they shop and how far** —
  e.g. *"Fed and warm — larder at 80%; they shop at Granary 1, 6 tiles away, when it falls to half."* (D744, new in this
  slice). With the larder low it reads *"…somebody is going to Granary 1, 6 tiles away."*
- [ ] **Paint housing far from every store** (well past the village). The brush warns: *"That corner is N tiles from the
  nearest shop; the village budgets 8. Families there will spend their days walking for food."* Close to a store it says
  nothing. It warns and never forbids — you can still paint there (D744).
- [ ] **A forester at work through the year.** In spring, summer and autumn they plant saplings; **in winter they plant
  nothing** (felling goes on) (D728).
- [ ] **The first winter.** People go in to get warm and come out again; nobody freezes in a fed village with firewood.

### 3. The market, nomads and the trades (Years 6–20)

- [ ] **A market beside the homes.** Home cards that used the granary now name the market, if it is the nearer one.
- [ ] **Nomads (around Years 8–12), with *nomads may come to the valley* ticked on the villagers bar.** The moment the game drops to normal
  speed for them, **the pop-up is already up** — you are never left wondering why it slowed. While it is open the band
  waits at the valley's edge; after *Go on* they walk in (D732).
- [ ] **Hunting lodge → leather.** After about fifty leather has been handled, **the tailor's shop** is learned; build it
  and coats are made. A coated villager lasts longer outdoors in winter (D659–D667).
- [ ] **A smithy, then the iron mine.** After the first iron tool is made the mine is learned; it digs from a seam
  (D449–D451).
- [ ] **The quarry**, once enough stone has been dug by hand: it digs rock that never runs out (D434–D447).

### 4. The workshop and the handcart (around Year 20)

- [ ] **About 1,500 logs felled** (over the whole game) → **the workshop** is learned. Build it: the carpenter makes
  handcarts (D675–D682).
- [ ] **A household with a handcart** fetches more in one trip; the home card's shop line still names the same store
  the cart goes to.
- [ ] **A trade with a cart** (the woodcutter, the quarrier) carries loads in it (D692–D698).

---

## Run B — a late save (your Hollowmere, Year 84)

### 5. House tiers (cottages from about Year 25–32, manors from about Year 70–86)

These two switches went into the game without being played (D723, merged at your word: *"Merge it now"*). This is
their first play.

- [ ] **Load the save.** Cabins, cottages and manors stand as they did. Older houses are rebuilt one at a time, worst
  first.
- [ ] **Build bar → Homes: *Cabins · Cottages · Manors* and a *Rebuild older homes* tickbox beside them** (shown only
  once the cottage is learned). **Untick it:** a rebuild that was waiting is called off (the village says so); one
  already started still finishes. New homes are still built as whatever you picked.
- [ ] **Tick it again:** the next rebuild is raised straight away.
- [ ] **Open a stone house's home card → Settings → *Keep this house as it is*.** Tick it: that house is skipped by the
  rebuilds, and if it was waiting, the next house takes its place. It is still mended when worn. Untick it: it waits
  its turn again.
- [ ] **Save, quit, load.** Both switches come back as you left them.

### 6. The late game in passing

- [ ] **A bridge** (wooden once ten people have lived in the valley; stone after a thousand crossings) is crossed.
- [ ] **The mill and the bakery** turn wheat into bread.
- [ ] **Trade** (after the town hall): the boat docks at the trading post.
- [ ] **Nothing on screen says *ERROR*,** and nothing stops or freezes.

---

## When you are done

Quit normally, then tell Claude **"walked"** with any ❌ lines. Claude scans the two newest logs for `ERROR` (item 5),
writes your verdicts into `phase-5-exit-gate.md §3` and this file, and the gate closes when the five items hold.
