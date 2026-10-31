# BgDiag_Razor

> Collaboration contract: [`../AGENTS.md`](../AGENTS.md)
> Umbrella status & dependency graph: [`../INSTRUCTIONS.md`](../INSTRUCTIONS.md)
> Mission & principles: [`../VISION.md`](../VISION.md)
> Open work: the [tracker](https://github.com/halheinrich/backgammon/issues), label `BgDiag_Razor`.
> Repo: https://github.com/halheinrich/BgDiag_Razor, branch `main`.

## What it is for

The Blazor face of the board. `BackgammonDiagram_Lib` draws a position as SVG
and stays free of any Blazor dependency; this library binds that drawing into
a component, turns clicks on it into events, and adds the two widgets that take
a user's answer to a decision: click-by-click play entry for a checker play,
and an answer row for a cube decision. BgQuiz_Blazor uses all of it;
BgArena_Blazor draws its replays with the view-only board.

C# / .NET 10 Razor Class Library (`Microsoft.NET.Sdk.Razor`), tested with bUnit
over xUnit.

## Using it

Four components, all in namespace `BgDiag_Razor.Components`. Each member's XML
doc states its contract, and `GenerateDocumentationFile` builds them on every
build. This file holds what those cannot: which one to reach for, why each is
built as it is, and what to avoid.

| A host wants | It renders | It passes | It hears |
|---|---|---|---|
| a position shown, perhaps clicked | `BackgammonDiagram` | `Request` | one callback per clickable region |
| a checker play entered by clicks | `BackgammonPlayEntry` | a checker-play decision's `Request` | `OnPlayCompleted`, `OnSubmitRequested` |
| a cube decision answered | `BackgammonDiagram` for the board, `BackgammonCubeActions` for the answer | `Decision`, `Value` | `ValueChanged` |
| the widest the cube row can be, before any cube decision | `BackgammonCubeActionsRuler` | `ShortLabels` | nothing: it measures the root |

A host routes by the record's kind (`BgDecisionData.Match` / `Switch`, or a
type test as here) and builds the request from the record:

```razor
@if (decision is CubeDecision cube)
{
    <BackgammonDiagram Request="DiagramRequest.ForDecision(cube, ranking)" />
    <BackgammonCubeActions @bind-Value="_answer" Decision="cube" />
}
else
{
    <BackgammonPlayEntry @ref="_entry"
                         Request="DiagramRequest.ForDecision(decision, ranking)"
                         OnPlayCompleted="HandlePlay"
                         OnSubmitRequested="SubmitAsync" />
}
```

The scoped stylesheets (the pills, the play-entry column, the ruler's box)
reach a page in the bundle a Blazor host links as `{Assembly}.styles.css`, so a
host of any of those three components needs that link. `BackgammonDiagram` has
no stylesheet and needs none: its styles are inline.

## Build and test

The solution is `BgDiag_Razor.slnx`. It needs the umbrella checkout: four of its
six projects are siblings reached by relative path. From this directory:

```bash
dotnet build BgDiag_Razor.slnx
```

```bash
dotnet test BgDiag_Razor.slnx
```

The suite takes seconds and reads nothing from `TestData/`.

**What a green run says.** bUnit has no CSS engine and lays nothing out. The
suite checks markup, events, the sources read as text, and the compiled
stylesheet's rules: which selectors reach a rendered element, and which declared
value wins there. It does not check rendered geometry. The figures and layout
claims in this file are separate evidence: live-browser checks recorded in
commit messages and not repeatable from this repo. "What backs the layout
claims" and "The horizontal metrics" below give each one's record and its
limits. Whether the ruler's width agrees with the live row under real fonts is
the consumer's browser check.

## Depends on

All three are project references.

- **BackgammonDiagram_Lib** — `DiagramRequest` and `DiceOrder`, `DiagramOptions`,
  `DiagramRenderer`, `BoardHitRegions` with `HitRect` and `SvgViewBox`,
  `SvgFormat`, and `CubeLabels` with `CubeAnswerSpelling`. `BackgammonDiagram`
  renders through it, `BackgammonPlayEntry` redraws its board with
  `WithWorkingBoard`, `BackgammonCubeActions` labels every pill through
  `CubeLabels`, at the decision, and `BackgammonCubeActionsRuler` draws every
  spelling `CubeLabels.Spellings` lists, at no decision. What each of those does
  is that library's `INSTRUCTIONS.md`'s to state: "DiagramRequest", "Cube
  wording: `CubeLabels`" and "Hit regions".
- **BgDataTypes_Lib** — `CheckerPlayDecision` (the kind play entry takes),
  `CubeDecision` (the kind the cube row answers), `BoardPosition` (its value
  equality is the reset key), `Play` and `CubeAnswer` (on the public surface;
  the enum's declaration order is the offered order). Also reachable through
  the other two, and referenced directly so a change in their edges cannot
  take it away.
- **BgMoveGen** — `MoveEntryState` and `ClickOutcome`, which drive
  `BackgammonPlayEntry`. The NativeAOT interop layer is not consumed.

Test-only:

- **BgDataTypes_Lib.TestSupport** — `TestRecords`, the producer's record
  builders: the one way these tests build the decision records their requests
  are made from, so no test here restates a record's construction.

The cube row's reach is deliberately small. `BackgammonCubeActions` consumes
`CubeAnswer` and `CubeDecision` from BgDataTypes_Lib and `CubeLabels` from
BackgammonDiagram_Lib, and nothing else from either. Its inert copy consumes
`CubeAnswer` and `CubeLabels.Spellings` with its `CubeAnswerSpelling`, and no
decision at all. Neither uses BgMoveGen, since a cube decision has no checker
moves to assemble, or the diagram library's rendering, since the row is
board-free.

## Layout

Two projects here, governed by repo-root `Directory.Build.props` (TFM,
`TreatWarningsAsErrors`, XML doc generation) and `Directory.Packages.props`
(Central Package Management). `BgDiag_Razor.slnx` lists them and the four
outside projects the build reaches.

**`BgDiag_Razor/`** — the Razor class library: four components under
`Components/`, each a `.razor` markup file with its code-behind, and a scoped
`.razor.css` where it styles its own markup. Nothing lives under `wwwroot/`; the
compiled scoped-CSS bundle is the library's only web asset.

- **`BackgammonDiagram`** — the view-only board: the core lib's SVG injected
  beneath a transparent click overlay built from the same hit-region geometry.
- **`BackgammonPlayEntry`** — the stateful play-entry widget for a checker-play
  decision: wraps `BackgammonDiagram`, drives a `MoveEntryState` from its
  clicks, reports each completed `Play`. Its scoped CSS is the bounded-height
  board slot.
- **`BackgammonCubeActions`** — the free-standing cube answer row: one radio
  group of the four `CubeAnswer`s, labelled at the decision, strictly
  controlled. Its scoped CSS is the pill styling, and its markup file holds the
  pill row's markup, which its inert copy draws too.
- **`BackgammonCubeActionsRuler`** — that row's inert copy, for a host to
  measure: every state the row can show, in a box as wide as the widest. Its
  scoped CSS is that box alone.

**`BgDiag_Razor.Tests/`** — bUnit over xUnit: one test class per component, plus
`BgDiagRazorTrimPostureTests` (see "Thin wrapper, by design"). Three helpers
serve the cube classes, and a nested parent serves the play-entry class:

- `CubeDecisions` — the two decisions, gammons possible and not, that the live
  row's tests and its copy's both answer.
- `ComponentSources` — a component's sources as text, comments stripped, for
  what a render cannot pin.
- `CompiledStylesheet` — the rules of the compiled scoped-CSS bundle, and the
  cascade of their declared values onto a rendered element.
- `EntryHost`, inside `BackgammonPlayEntryTests` — a parent that binds the entry
  through its own render tree by parameter name, as compiled Razor does, for
  the tests of the wire from a click to the parent's markup.

## Architecture

### Thin wrapper, by design

All SVG generation and hit-region geometry lives in the core lib; this project
only binds that output into components and surfaces click events.

The wrapper is held trim-safe by its own build. BgQuiz's WebAssembly client
ships this assembly and publishes it trimmed with trim warnings as errors, so
the library's csproj declares `IsTrimmable` and turns on `EnableTrimAnalyzer`
(the Razor SDK enables neither by itself). Under the repo-wide
`TreatWarningsAsErrors`, any construct the trimmer cannot analyze — a
reflection-bound serializer call, a `Type.GetType` on a computed name — becomes
a build error here, in the change that introduces it, rather than a failure at
the consumer's publish (`halheinrich/backgammon#197`). The declaration is pinned
by `BgDiagRazorTrimPostureTests`, which reads the SDK-emitted `IsTrimmable`
assembly metadata; the analyzer switch leaves no such trace, so the build is its
only check. The test project carries neither setting: it ships nowhere.

### Why four components

`BackgammonDiagram` is the view-only primitive: it holds no
position-manipulation state, and view-only consumers (replay viewers, bot-vs-bot
playback, analytics inspection) use it directly. `BackgammonPlayEntry` composes
it and owns the click-by-click state. `BackgammonCubeActions` renders no board
and takes the record, not a `DiagramRequest`: a cube decision has no
click-by-click board state, so unlike the play half there is nothing for an
entry wrapper to encapsulate. A cube consumer renders the position with
`BackgammonDiagram` and places the row wherever its layout wants it, keeping the
board region board-only. The row emits the raw answer; what that answer costs at
the decision (`CubeDecision.CostOf`) is the quiz layer's to ask the producer.

The split keeps the encapsulation clean: a consumer that only displays a
position pays for no click state, a play consumer wires no move-entry state of
its own, and a cube consumer accepts no board-bundled layout to get a row of
radios.

`BackgammonCubeActionsRuler` takes no decision, so it is not routed to. A host
renders it once, before any problem, to size its row.

### BackgammonDiagram: rendering

`OnParametersSet` is the single render hook, and `DiagramRenderer` is static, so
the component holds no renderer state, only the cached outputs. A null `Request`
clears them and renders nothing. Otherwise it caches `RenderSvg` and
`GetHitRegions` and builds `_rootStyle` for the `.bg-diagram` root.

The root's style is inline for two reasons. The aspect ratio tracks
`DiagramOptions.Aspect` at render time, which scoped CSS cannot express; and
`position: relative`, the overlay's positioning context, stays inline so the
overlay never depends on a host having linked the scoped bundle (`a7c8603`).

The drawing goes in as `(MarkupString)_svgMarkup`, inside a child `div` with
`pointer-events: none`. That injection and the separate overlay exist because
the lib returns one SVG string. If it ever exposes a structured render API, the
injection could become a native Razor SVG tree and the overlay `<rect>`s could
join that tree, dropping the pointer-events and stacking plumbing and the
`MarkupString`.

### BackgammonDiagram: the click overlay

Click handling is pure Razor, no JS interop. A second `<svg>` is positioned
absolutely over the drawing and sized from `BoardHitRegions.ViewBox`, so it
aligns 1:1 with the lib's coordinates. Each `HitRect` becomes a `<rect>` with
`fill="transparent"`, `pointer-events="all"`, and an `@onclick`:

- `Points` → one rect per point; `OnPointClicked` with the point index, 1–24.
- `Bar` → `OnBarClicked`, always 25.
- `OnRollTray` (nullable) → `OnTrayClicked`.
- `Candidates` → one rect per candidate row the play panel draws, in ascending
  key order; `OnCandidateClicked` with the key: the candidate's stored index
  into the decision's plays, under any ranking, ceiling or rescue, never its row
  position. Empty unless a Solution-mode checker play's panel is drawn.
- `Dice` (nullable: a cube decision has none) → `OnDiceClicked`. The view
  forwards the click and does not interpret it.

`Cube` and `OpponentTray` get no rect. A point highlight, if a consumer ever
asks for one, belongs here too: drawn from the same `BoardHitRegions`, so the
index-to-rect mapping stays inside `BackgammonDiagram`. `BackgammonPlayEntry`
could then forward `MoveEntryState.LegalNextClicks`, declared
`IReadOnlyCollection<int>`, which lists sources only, not destinations. Its
original design had such a hint parameter, dropped to keep the public surface
small.

**Rect order is a contract.** Rects are emitted in the order above, so the first
rect is point 1, the 25th the bar, and the last the dice wherever dice are
drawn. BgQuiz's e2e and this repo's click tests read them by position.

**Rects are addressable.** Each point's rect carries `data-point="N"`, the index
the callback reports, and each candidate's `data-candidate="i"`, its stored
index, whatever row it is drawn on; no other rect carries either. So
`rect[data-point="7"]` is point 7 and `rect[data-candidate]` is exactly the
drawn rows. These are for out-of-process drivers, since a browser-automation
harness cannot bind an `EventCallback` (`halheinrich/backgammon#13`). They are
plain `data-` attributes rather than `id`s because they repeat across two
diagrams on one page. Styling the rows (a pointer cursor, say) is the host's CSS
against that selector.

**Candidate clicks stop propagating.** The candidate rects carry
`@onclick:stopPropagation="true"`: a click that shows a play is the component's
to report and ends there, so a host's Blazor `@onclick` on an ancestor — the quiz
page's "click elsewhere restores the played position" (`SPEC-quiz-view.md` §1,
showing a play) — never sees it as a click outside the list. Every other region
propagates, since a click on the board is a click outside the list. The stop is
limited to the Blazor scope and does not extend to the HTML DOM: a JavaScript
listener still sees the click, so a host that needs the stop must use a Blazor
handler.

### Host attributes

Every component captures unmatched attributes in `AdditionalAttributes` and
splats them on its root. Of two same-named attributes the later wins, so what a
host's `class` or `style` does depends on where each component splats:

| Component | Splatted | A host `class` | A host `style` |
|---|---|---|---|
| `BackgammonDiagram` | after `class` and `style` | replaces `bg-diagram` | replaces the root's inline style (see the pitfall "Size through the container") |
| `BackgammonPlayEntry` | after `class` | replaces `bg-play-entry`, so the scoped column and the bounded-height contract go with it | applies |
| `BackgammonCubeActions` | after `class` and the group's role and name | replaces `bg-cube-actions`: the live row loses its styling and stops matching its inert copy (`halheinrich/backgammon#338`) | applies |
| `BackgammonCubeActionsRuler` | before its class, `aria-hidden` and `inert` | refused | refused |

`data-` attributes and `id` are safe everywhere, and are how a host finds a
component. `BackgammonPlayEntry`'s splat reaches its own wrapper, a separate
element above the inner diagram's root; it does not forward attributes to the
inner diagram.

### Sizing: the ratio, contain-fit and the board slot

**One dimension sizes the board.** The `.bg-diagram` root carries `aspect-ratio:
<W> / <H>` from `_hitRegions.ViewBox`, the viewBox the overlay uses, so the
ratio has one source. A host gives the box a definite width or a definite
height, through a stretching or sized container (grid track, flex, explicit
dimension), and the box keeps the board's ratio without any host re-encoding it.
The overlay is absolutely positioned at `width/height: 100%`, so it covers the
box under any sizing.

**The contain-fit default rides the same inline style:** `max-height: 100%;
margin-inline: auto`. When the containing block has a definite height, the board
caps to it, the width re-derives through the ratio (CSS transfers the max
constraint across it), and the auto margins centre the letterboxed board. In an
unbounded flow both are inert: a percentage max-height against an indefinite
containing-block height computes to `none`, and the auto margins are zero while
the box fills its row. So width-driven consumers (a plain `max-width` container)
are unchanged.

**`BackgammonPlayEntry` carries that through its wrapper.** It renders the
diagram inside an internal `bg-board-slot` div. Its scoped CSS makes
`.bg-play-entry` a flex column and the slot the shrinkable row (`min-height: 0`;
deliberately the default `flex: 0 1 auto`, so the slot shrinks under a bound but
never grows).

- **Bounded:** a host gives the wrapper a definite height, a real `height` or
  shrinkable-flex-item sizing (`flex: 1 1 0; min-height: 0` in a definite-height
  column). The slot shrinks to the bound, its post-flex height becomes definite,
  and `.bg-diagram`'s `max-height: 100%` caps the board to it.
- **Unbounded:** a column flex with auto height stacks like block flow, the slot
  never grows, and the percentage resolves to none, so the board stays
  width-driven.

The wrapper has no sibling chrome today; the slot keeps the height-cappable
structure explicit so chrome can join it later, as `flex: none` siblings of the
slot, without changing the host's contract.

**What backs the layout claims.** The suite pins the structure these rest on:
the emitted style (`Root_CarriesContainFitDefault` and the aspect-ratio tests),
the diagram inside the slot, and no inline sizing on it (`BoardSlot_WrapsDiagram`,
`BoardSlot_CarriesNoInlineSizing`). No test pins the layout, which bUnit cannot
render. The layout itself rests on live-browser runs recorded in commit messages,
whose harnesses were not kept:

- `a7c8603` (2026-07-07), a scratch replica of the DOM in a browser preview: a
  height-driven grid item resolves to a correct-ratio box, 533×300 at 16:9, with
  the overlay covering it.
- `83329dd` and `492b01a` (2026-07-08), run before implementation: bounded flows
  letterbox with the ratio preserved and centre; with the slot, unbounded flow is
  pixel-identical to the structure before it; a width-driven parent of indefinite
  height (BgArena's `ReplayBoard` shape) is unaffected; and a `max-height` on an
  auto-height ancestor does not engage the contract, because content is sized
  before the clamp and simply overflows it.
- BgQuiz_Blazor `c62a66d` (2026-07-08), the real page under the 16:9 default at
  1280×800 and 1280×430: play entry unbounded at 922×519 and letterboxed to
  500×281, with no page overflow.

Those runs cover the mechanism in one browser, at those sizes, on the page as it
then was. The slot's rules and the inline contain-fit declarations have not
changed since. A change to either, or a host layout unlike these, is a reason to
check in a browser again.

### BackgammonPlayEntry: entering a checker play

The request is a checker-play decision's own: `DiagramRequest.ForDecision` from a
`CheckerPlayDecision`, varied with whatever options the consumer draws it with.

**The board drawn is the request's own redraw.** `_state` is a `MoveEntryState`
built from the record's board and its roll in rolled order. `_renderedRequest`,
what the inner `BackgammonDiagram` gets, is
`Request.WithWorkingBoard(_state.CurrentPosition, diceOrder)`, rebuilt on every
state change and used from the first render. **The board during entry is not a
decision record** (Hal, 2026-09-28, on `halheinrich/backgammon#273`): the
component builds no record, session or display facts. What the redraw keeps is
`WithWorkingBoard`'s to state. The consequence here is that a request's `Mode`
and `ShowXgid` do not show during entry, a working board having no analysis and
no XGID.

**Click routing.** The indices match `MoveEntryState`'s contract:

| Click | Inner callback | Entry does |
|---|---|---|
| a point | `OnPointClicked`, 1–24 | `TryAdvanceFrom(point, diePreference)`, then `TryMakePoint(point)` where that is `Illegal` |
| the bar | `OnBarClicked`, 25 | `TryAdvanceFrom` only: the bar is a source, never a make destination |
| the tray | `OnTrayClicked`, no index | `TryBearOffMax`, the bear-off-max shortcut |
| the dice | `OnDiceClicked` | not entry: see below |

A click on a point holding an own checker advances one move from it, the die
chosen by `diePreference`, the rendered dice order, leftmost first. A point
holding no own checker falls through to making it, hitting a blot there. The
producer's own-occupied guard is what lets that dispatch be board-blind, so the
component never inspects the position. Bearing off one checker is an ordinary
advance from its home point, not a tray click. Any outcome but `Illegal` redraws
the board, and `PlayCompleted` fires `OnPlayCompleted`.

**The dice click is display and submit, not entry.** On a complete play it fires
`OnSubmitRequested`. On an incomplete play it toggles a display-only dice swap (a
no-op for doubles): the `DiceOrder` the working board draws the roll in, and so
which die a one-click advance prefers. The incoming `Request` and the
`MoveEntryState` are untouched, so the swap never disturbs the reset key or
in-progress entry. The component stays submit-oblivious: it signals, and the host
already holds the `Play` from `OnPlayCompleted`.

**What a host can see of entry.** `OnPlayCompleted` and `OnSubmitRequested` are
all it hears, and `UndoLast` and `UndoAll`, called through `@ref`, are all it can
do; `UndoAll` works after completion too, and a play completed again fires
`OnPlayCompleted` again. There is no per-click notification and no `CanUndo`
predicate, and `Request` and `OnSubmitRequested` carry `[EditorRequired]` while
`OnPlayCompleted` does not (`halheinrich/backgammon#22`).

**Reset: the decision's start, by value.** A fresh `MoveEntryState` is built only
when the incoming decision's start — its board, by `BoardPosition`'s value
equality (the one "same position"), and its roll in rolled order — differs from
the start of the current entry. The component keeps the start board; the roll
half is the entry state's own `Die1` / `Die2`. The request is still redrawn each
time, so its options and presentation follow it. This decouples reset from object
identity: a consumer can rebuild a `DiagramRequest` for any reason (parent-state
churn, a settings change) without losing mid-click progress.

**Decision-kind guard.** `OnParametersSet` refuses any request but a checker
play's with an `ArgumentException` whose `ParamName` is `Request`, naming the
correct composition: the intent is to fail loudly at the contract boundary rather
than render an unusable widget. It is a run-time refusal because a
`DiagramRequest` carries its decision as `BgDecisionData`, not its kind, so the
parameter's type cannot require a checker play; whether it could is
`halheinrich/backgammon#306`. The kinded records make the routing a compile-time
fact one step earlier, at the consumer. The cube side needs no such guard:
`BackgammonCubeActions` takes the `CubeDecision` itself.

### BackgammonCubeActions: the answer row

The answers are the umbrella's `SPEC-scoring.md` §3 as amended on
`halheinrich/backgammon#326`, whose type is BgDataTypes_Lib's `CubeAnswer`; what
the fourth answer means, and which of its labels applies at a decision, are that
section's and that type's to state. The `bg-cube-actions` root is itself the
`role="radiogroup"` (`aria-label="Cube decision"`), holding one pill per
`CubeAnswer` member in declaration order, read once into `OfferedAnswers`. All
four are offered at every decision.

**Every pill is labelled at its decision, by the label home.** Each caption is
`CubeLabels.Label(answer, Decision)`, or `ShortLabel` in the short form. Which
label the fourth answer takes is the decision's reading (`CubeDecision.ClaimOf`),
which the label home renders. So a re-wording or a re-ruling at either home
reaches this row without an edit to it.

**The short form is the host's call.** `SPEC-quiz-view.md` §4 ("The action row
under quiz navigation") rules when cube labels abbreviate, and what each button's
tooltip and accessible name are in each form. Only the host knows what else
shares its row, so the host sets `ShortLabels`, and the row measures nothing.
Each pill's `title` is the full label in both forms. Each radio's `aria-label`
follows that section's rule, composed in `AccessibleNameOf` from the label home's
two labels, so the row joins them and spells neither.

**One markup source for the row and its inert copy.** The pill row's markup is
one internal template, `PillRow` (with `PillMarkup` for each pill), written in
`BackgammonCubeActions.razor`. The live row fills in its wiring: on the root, the
group's role and name and then the host's splat (`GroupWiring`), and in each pill
its radio (`RadioOf`). `BackgammonCubeActionsRuler` draws every state with the
same template and no wiring. Scoped CSS reaches only the markup written in its
own component's file, through the scoped-style attribute the compiler writes onto
that markup; matching class names would not carry it. Because the template is
written here, it carries this component's attribute wherever it renders, so this
stylesheet reaches the copy's pills exactly as it reaches the live row's.

**The radio is hidden, not removed.** Each option is a
`<label class="bg-cube-action">` wrapping its own `<input type="radio">`, and the
pill's border, fill and weight are the selected affordance
(`bg-cube-action-selected`), so the native dot stops being painted. The technique
is the transparent native control stretched over its own pill
(`position: absolute; inset: 0; opacity: 0`). The `input` stays rendered,
focusable and in the accessibility tree, keeping arrow-key roving, mutual
exclusion by name and the control's accessible name, and it stays the element at
its own coordinates, so the pill's whole area is its hit target. The keyboard
focus ring moves from the dot to the pill (`:focus-visible`, on `outline` so it
cannot widen the row).

**The radio `name` is generated per instance,** since same-name radios are
mutually exclusive document-wide and a fixed name would cross-link two rows on one
page. It is internal: address the group by its `aria-label` or the
`bg-cube-actions` class, and the pills by `bg-cube-action`.

**Sizing posture.** The root is an inline-flex row that takes only its content
size and carries no external margins, and each pill's height falls out of its own
padding and line-height, roughly a standard button's without encoding any
consumer's button metrics. The consumer places the row and owns the spacing
around it.

**The horizontal metrics are constants set against measurement,** not free
styling. The pills are the widest element of the quiz page's action row. At the
original metrics that row out-widened the checker row and wrapped through the
641–1366px band, adding a line of chrome that cost board pixels wherever the board
is height-bound. Compacting them is the resolution ruled in the umbrella's
`SPEC-quiz-view.md` §2 (the invariance floor, `halheinrich/backgammon#99`): pill
gap 0.75rem to 0.25rem, pill inline padding 0.9rem to 0.45rem, the dot hidden. The
form is unconditional, with no media query, because a producer has no view of the
consumer's layout to gate one on. With one group there is one gap and no visible
group caption, which would re-spend the width; the accessible name rides
`aria-label`, where it costs no pixels.

Each figure below is a browser measurement recorded in the commit named, under the
quiz page's font stack (`7075e32` and `fc0c0a6` state it: 16px Helvetica Neue /
Helvetica / Arial, at 1x). The scratch pages were not kept, and no test pins a
width: `CubeActionsCss_KeepsItsMeasuredCompactMetrics` pins the constants. Other
platforms may resolve wider fallback fonts (`SPEC-quiz-view.md` §2's note on its
own width figures).

| The row | Width | Record |
|---|---|---|
| four compound pills, original metrics | 561.6px, 56% of a 1001.4px action row | `a152d00`, 2026-08-17 |
| the same pills, compacted | 396.0px (395.97) | `a152d00` |
| two groups, since retired | 364.8px | `fc0c0a6`, 2026-09-02 |
| four pills, a response spelled on every one | 509.2px | `fc0c0a6` |
| the four full labels, the fourth reading Too good: 89.3 / 113.9 / 116.0 / 82.2px | 413.5px with three 4px gaps; 418.8px with Double / Pass selected; 419.3px at most over any selection (weight 600 widens the selected caption) | `7075e32`, 2026-09-02 |

The label home's wording is what keeps the row near 413px: No double spells no
implied take. The fourth answer's longer label, No double / Pass, and the short
form have no figure here, because a host measures every state live, under the
fonts rendering, from the inert copy. Whether the row clears the consumer's
one-line contract, and the width at which the host switches to the short form, are
the consumer's measurements to take on its final row (`SPEC-quiz-view.md` §4).

Vertical metrics were deliberately left alone: 0.5rem block padding, a 1.2
line-height and 1px borders make the pill 37.2px tall (`fc0c0a6`, `7075e32`).
`a152d00` kept them as a tap-target floor, citing real-tablet touch data that is
recorded nowhere, and no spec rules a floor. So treat that as an assumption: the
measured fact is the height, and the reason for keeping it is a judgement.

**Value contract: strictly controlled.** `Value` is the selected answer or null,
and the row renders from it and nothing else. There is no component state, because
one radio is one whole answer and no partial answer exists to hold. `ValueChanged`
fires on every selection, with the chosen answer, and never carries null; it
re-fires when the selection moves, with no one-shot lock. Changing `Decision` or
`ShortLabels` relabels the pills at the next render, selecting nothing and firing
no `ValueChanged`. A null `Decision` throws `ArgumentNullException` (`ParamName`
`Decision`) from `OnParametersSet`, since no answer can be labelled without it.

### BackgammonCubeActionsRuler: the inert copy

The umbrella's `SPEC-quiz-view.md` §4, "One budget from the outset", has a host
size its action row from one width budget that holds the cube pills at their
widest, measured under the fonts rendering, before any cube decision is on screen;
that section states the rule and the chain that meets it. The live row cannot
supply that width: it is labelled at its decision, and the fourth answer's label
is that decision's reading. `BackgammonCubeActionsRuler` takes no decision. A host
renders it and measures it.

**What it draws.** One copy of the row for each state the live row can show in the
ruler's form. Each state takes one spelling of each answer, from every combination
of the spellings `CubeLabels.Spellings` lists, drawn once with nothing selected and
once with each pill selected in turn, because the selected pill is drawn heavier.
Today only the fourth answer has two spellings, so a form has ten copies. No state
is assumed to be the widest: the fonts decide. Every combination is drawn because
the label home lists what an answer can read, not which readings occur together;
if more answers had several, the copy could only err wide.

**What a host measures: the root's width,** for example
`getBoundingClientRect().width`. The root, `div.bg-cube-actions-ruler`, stacks the
copies in one column, each at its own width (`display: inline-grid;
justify-items: start`), so it is exactly as wide as the widest copy. Its `width`
and `min-width` are both `max-content`, so no parent can stretch or shrink it and
no copy wraps inside it, even in a zero-width host. A host that needs both forms
renders a ruler for each. It re-measures when the fonts load or change; an observer
on the root sees every change.

**Inert, and its box its own.** The root carries its class, `aria-hidden="true"`
and `inert`, all written after the host's splat, and its stylesheet keeps it
unpainted (`visibility: hidden`), which keeps its layout and so its width. Inside
it there is only the pill row's markup: no input or other control, and no name, id,
role, tabindex or event handler. So even a browser that ignores `inert` finds
nothing to focus or click, and nothing joins or shadows the live row's radio group
when both are on one page. It is still laid out, which is what makes it measurable.
Keeping it out of the page's flow, clipping it so it cannot widen the page (for
example inside an absolutely positioned, zero-size, `overflow: hidden` box), and
placing it where it inherits the live row's fonts are the host's tasks.

**States it cannot take.** Being inert and unpainted, the copy is never hovered,
focused or active, so its width is the live row's widest only while those states
change no geometry: the pitfall "Hover, focus and active change only paint" is
the rule.

## Pitfalls

### BackgammonPlayEntry

- **The reset key is the decision's start by value, not record or request
  identity.** To force a reset, pass a decision with a different board or roll.
  Re-passing the same start, in a freshly built request or another record, keeps
  the entry in progress. A host that wants a fresh entry on the same start calls
  `UndoAll`, or mounts a fresh component with `@key`, as BgQuiz does per problem,
  since two problems of a run can share a position.
- **`UndoLast` and `UndoAll` invoke `StateHasChanged`,** which requires the Blazor
  Dispatcher. Real consumers (button click handlers) are already on it; bUnit tests
  must wrap the call in `cut.InvokeAsync(...)`.
- **A pass position never fires `OnPlayCompleted`, but its dice click submits.**
  With no legal play, `MoveEntryState.IsComplete` is true at construction. Point,
  bar and tray clicks are no-ops, no `Play` is reported, and a dice click fires
  `OnSubmitRequested`, because that is the complete-play branch. A host that can
  present one handles it itself and does not assume a submit follows a play.
- **Only a checker-play decision's request is entered.** A cube decision's request,
  a board's (`DiagramRequest.ForBoard`) or a working board's throws. Render cube
  positions with `BackgammonDiagram` and take the answer with
  `BackgammonCubeActions`; the kind branch lives with the consumer.
- **Never hand the drawn request back in.** The inner diagram's request is a
  working board's: it presents no decision, so passing it as `Request` is refused,
  and it would restart entry from a mid-entry board if it were not. Keep the
  decision's own request as the parameter.

### BackgammonCubeActions

- **`Value == null` means nothing is selected, and gating submit is the
  consumer's.** One radio is one whole answer, so there is no half-answered state.
  The row has no view of a submit button and does not withhold, warn or nag; gate
  the button on `Value.HasValue`.
- **Strictly controlled: write the answer back.** A consumer that binds
  `ValueChanged` but never writes the answer into `Value` (or doesn't use
  `@bind-Value`) never adopts it: the selection snaps back at the next render, the
  consumer's own field remaining the single source of truth. A consumer wanting
  one-shot semantics advances to the next problem on the first callback.
- **Clearing between problems is the consumer's job.** There is no request and no
  start-keyed reset here. Set `Value` to null when advancing (with `@bind-Value`,
  null the bound field), or the previous answer stays selected.
- **`ValueChanged` and `Decision` are `[EditorRequired]`; `ShortLabels` is not.**
  Razor does not error on an unrecognized component attribute, so a misspelt one
  splats silently. RZ2012 surfaces a missing required parameter, and building with
  warnings as errors makes that a hard gate; `@bind-Value` satisfies the first. A
  misspelt `ShortLabels` fails safe: it leaves the full labels, the ruled default,
  which can only cost width, never meaning.
- **Never label an answer here, never read a rule, never withhold a pill.** Every
  caption, accessible name and tooltip is `CubeLabels`' at `Decision`. Switching on
  the answer, or reading the gammon fact or the session from the decision, would be
  a second spelling or a second derivation beside the one home, and nothing about
  the row's state or a position withdraws an answer (`SPEC-scoring.md` §3).
- **Never decide the short form here.** Whether the row fits depends on everything
  else in the host's row. Don't add a width threshold, a media query or a
  measurement to the component. Don't drop the full label from `title` or
  `aria-label` in the short form, or the visible short label from `aria-label`: the
  name must contain the text shown (`SPEC-quiz-view.md` §4).
- **A `Value` outside the four is a caller bug, not a fallback case.** It renders
  nothing selected. Don't "help" by remapping it onto a neighbouring pill; the tests
  pin that nothing lights.
- **Keep the radio a real control.** Never hide it with `display: none`,
  `visibility: hidden` or a `hidden` attribute, which take it out of the tab order
  and out of AT's reach, killing arrow-key roving and the row's accessible names;
  nor with a zeroed size, which leaves it no hit area. The page looks identical
  either way, so this regresses silently. The pill's `:focus-visible` outline is the
  other half of the affordance: without it a keyboard user has no visible cursor.
- **Don't "tidy" the hidden radio into the sr-only `clip-path` recipe.** It looks
  more idiomatic and keeps the accessibility tree correct, so it is the plausible
  wrong turn, but `clip-path` clips hit-testing as well as painting. Clipped to a
  1px box, the input stops being the element at its own centre (`elementFromPoint`
  returns the label), and anything driving the real control by pointer breaks. It
  was tried first and reverted: it made Playwright's `CheckAsync` fail its
  actionability check, which is how the BgQuiz e2e suite answers cube problems
  (`a152d00`). The stylesheet tests pin the technique.
- **The row's horizontal metrics are load-bearing at the consumer.** Re-widening the
  gap, the inline padding or the dot reopens the wrap, so take a fresh measurement
  first, and don't "restore" the dot for symmetry with some other control. Don't
  shave the block padding to save more width either: the compaction was horizontal
  only, and that would trade a layout win for a smaller tap target.
- **The pill row's markup is shared: edit it in `PillRow`, and draw both rows from
  it.** A pill written anywhere else carries another component's scoped-style
  attribute or none, and this stylesheet does not reach it, however exactly its
  class names match. The copy would then measure a different row, and nothing on
  screen would show it.
- **Style the pills through their classes, never through the row's wiring.** The
  copy has no `role` or `aria-label` on its root and no radio in a pill. A rule keyed
  to the wiring, such as `[role="radiogroup"] .bg-cube-action`, or the selected state
  written as `.bg-cube-action:has(input:checked)`, reaches the live row alone, so
  whatever width it sets is missing from the copy's measurement. The selected state
  is the `bg-cube-action-selected` class for this reason as well. Pinned against the
  compiled stylesheet.
- **Hover, focus and active change only paint.** A rule keyed to `:hover`, `:focus`,
  `:focus-visible`, `:focus-within` or `:active` may change paint, or repeat what the
  element has without the state (as the selected-and-hovered rule repeats the
  selected weight). A geometry-bearing declaration that differs from the stateless
  element (weight, padding, border width, gap) would widen the live row past the
  budget its host measured. `StatesTheCopyCannotTake_ChangeNoGeometry` enforces this
  against the compiled stylesheet.

### BackgammonCubeActionsRuler

- **Measure the root, and nothing inside it.** The copies are the row's states in no
  promised order, and the root's height is not a row's.
- **Place it where the live row's fonts are.** Like the live row, the copy inherits
  its font from where it sits; a ruler under other font settings measures another
  row. Host styles that reach the live row through an ancestor of the host's (e.g.
  `.action-row .bg-cube-action`) must reach the ruler too, or the two widths part.
- **Host selectors keyed on the row's classes match the copies too.** The copies
  carry `bg-cube-actions` and `bg-cube-action`, being the same markup. A host or test
  that finds the live pills by class scopes its selector to the live row's container,
  or excludes `.bg-cube-actions-ruler`.
- **Hide it with clipping, never with `display: none`.** It is already unpainted and
  inert. `display: none` on it or any ancestor leaves it no box, and every width it
  reports is zero.
- **It takes no `class` or `style` from a host.** Either, in any letter case, could
  only restyle the box that makes its width the widest copy's, so either is refused
  with an `ArgumentException` (`ParamName` `AdditionalAttributes`) rather than dropped
  silently. Give it a `data-` attribute to find it by. Styling
  `.bg-cube-actions-ruler` from a host stylesheet would defeat the box the same way;
  don't.
- **Never caption the live row from `Spellings`, nor the copy at a decision.** The
  two rows share how a pill is drawn, not how it is captioned. Pinned on both sources.
- **Nothing in its own stylesheet may reach the copies.** That stylesheet's one rule
  is the box. A `::deep` rule, or any rule on the copies' rows or pills, would make
  the copy's geometry differ from the live row's. Pinned.

### BackgammonDiagram and sizing

- **Never interpolate a fractional number into a Razor markup attribute.** Blazor
  formats interpolated values with the thread culture, and WASM adopts the browser
  locale, so `x="@r.Width"` for `30.8` emits `"30,8"` on an nb-NO browser, an invalid
  SVG attribute a browser parses as `0`: the bar and the `viewBox` silently break.
  A Norway beta tester hit this in production (`a635202`, 2026-07-15), and
  `Overlay_MarkupIsCultureInvariant_UnderCommaDecimalLocale` pins the fix. Route every
  geometry number through the lib's formatter: `SvgFormat.Number(value)` for a scalar
  attribute, `SvgViewBox.ToAttributeString()` for the `viewBox`. CSS numbers are a
  separate case: `_rootStyle` uses `FormattableString.Invariant` because
  `SvgFormat.Number`'s `"0.##"` rounding would truncate the ratio's precision.
- **Take the overlay's geometry from `GetHitRegions(Request, Options)`, every
  render.** Clicks land on the wrong elements if the overlay's viewBox ever diverges
  from the drawing's. Orientation and on-roll tray positioning depend on match state,
  not just the options, so don't "optimize" by caching hit regions keyed on options
  alone.
- **Keep the drawing's `div` and the overlay as they are: two direct children of the
  root.** The overlay sits on top because it is absolutely positioned and the
  drawing's `div` is not, against the root's inline `position: relative`; the `div`'s
  `pointer-events: none` keeps the drawing out of hit-testing. BgQuiz reads the
  structure too (`.bg-diagram > div > svg` in its stylesheet, `.bg-diagram > svg` in
  its e2e), so don't reorder or re-wrap them.
- **`MarkupString` is trusted-output-only.** The injected SVG is produced by
  `BackgammonDiagram_Lib`. Never pass externally supplied HTML through it in this
  component: Blazor skips encoding it, and anything from outside the lib would be an
  XSS vector.
- **Give the board one definite dimension, not none and not both.** The box has no
  fixed pixel size, so a consumer that constrains neither dimension gets a zero or
  ambiguous box, and one that pins both (e.g. `width: 100%; height: 100%`) makes
  `aspect-ratio` a no-op.
- **Size through the container, not a `style` splat.** A host `style` replaces the
  root's inline style and drops the ratio, the contain-fit default and the overlay's
  `position: relative`. And because those declarations are inline, a host stylesheet
  cannot override them without `!important`: a consumer that wants overflow instead of
  contain-fit sizes the container rather than fighting the default.
- **Bound with a definite height, never `max-height` alone.** A `max-height` on the
  wrapper or any auto-height ancestor does not engage the contract (see "What backs
  the layout claims"). Bound with a real `height`, such as `height: 100%` under a
  definite-height container, or by making the element a shrinkable flex item
  (`flex: 1 1 0; min-height: 0` in a definite-height column).
- **Bound the wrapper; don't style inside the slot.** Consumer CSS that sets
  `height`, `flex` or `display` on `.bg-board-slot` replaces the mechanism rather than
  configuring it. The letterbox is only guaranteed with the producer's values in
  force.
- **Never `display: contents` on the wrapper.** It dissolves the flex column that
  gives the slot its definite post-flex height.
