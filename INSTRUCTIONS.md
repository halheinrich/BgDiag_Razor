# BgDiag_Razor

> Collaboration contract: [`../AGENTS.md`](../AGENTS.md)
> Umbrella status & dependency graph: [`../INSTRUCTIONS.md`](../INSTRUCTIONS.md)
> Mission & principles: [`../VISION.md`](../VISION.md)

## Stack

C# / .NET 10 / Razor Class Library (`Microsoft.NET.Sdk.Razor`) / bUnit.
Visual Studio 2026 on Windows.

## Solution

`D:\Users\Hal\Documents\Visual Studio 2026\Projects\backgammon\BgDiag_Razor\BgDiag_Razor.slnx`

## Repo

https://github.com/halheinrich/BgDiag_Razor — branch `main`.

## Depends on

- **BackgammonDiagram_Lib** — `DiagramRequest`, `DiagramOptions`,
  `DiagramRenderer`, `BoardHitRegions`, `SvgViewBox`, `HitRect`. Referenced
  as a project reference, not a package.
- **BgDataTypes_Lib** — `BoardState` (class), `Play` (struct), `Move`
  (readonly record struct), `CubeOwner` (enum), `CubeAction` (enum),
  `CubeDecisionPair` (readonly record struct). Move primitives and the
  mutable board live in the shared-data layer; consumed here by
  `BackgammonPlayEntry` (`Play` on the public surface, `BoardState.FromMop`
  for state construction), by `BackgammonCubeEntry` (`CubeAction` for its
  button groups, `CubeDecisionPair` on the public surface), and by tests.
  Referenced as a project reference (also reachable transitively via
  BackgammonDiagram_Lib and BgMoveGen, but the explicit ref documents the
  direct dependency and insulates against future transitive-edge churn).
- **BgMoveGen** — `MoveEntryState`, `ClickOutcome`. Drives
  `BackgammonPlayEntry`'s click-by-click play assembly. Referenced as a
  project reference. Transitively brings `BgMoveGen`'s standalone surface;
  this subproject does not consume the NativeAOT interop layer.

`BackgammonCubeEntry` consumes `CubeAction` / `CubeDecisionPair` from
`BgDataTypes_Lib` for its two-group atomic decision surface. No `BgMoveGen`
use — cube decisions have no checker-move state to drive.

## Directory tree

Source-only — excludes `.gitignore`, `.github/`, and build artifacts.

```
BgDiag_Razor.slnx
BgDiag_Razor/
  BgDiag_Razor.csproj
  _Imports.razor
  Components/
    BackgammonDiagram.razor           — markup + transparent click overlay
    BackgammonDiagram.razor.cs        — code-behind, parameters, lifecycle
    BackgammonPlayEntry.razor         — wraps BackgammonDiagram, drives state
    BackgammonPlayEntry.razor.cs      — code-behind, parameters, click routing
    BackgammonCubeEntry.razor         — wraps BackgammonDiagram, two action groups
    BackgammonCubeEntry.razor.cs      — code-behind, parameters, decision routing
  wwwroot/
BgDiag_Razor.Tests/
  BgDiag_Razor.Tests.csproj
  BackgammonDiagramTests.cs           — bUnit rendering + event-callback tests
  BackgammonPlayEntryTests.cs         — bUnit play-entry contract tests
  BackgammonCubeEntryTests.cs         — bUnit cube-entry contract tests
```

## Architecture

### Thin wrapper, by design

This subproject exists so that `BackgammonDiagram_Lib` can stay free of any
Blazor / Razor dependency. All SVG generation and hit-region geometry lives
in the core lib; this project only binds that output into a Blazor component
and surfaces click events.

### Three components: view-only, play-entry, cube-entry

`BackgammonDiagram` is the **view-only primitive** — given a `DiagramRequest`
it renders the position and surfaces click events. It holds no
position-manipulation state. View-only consumers (replay viewers, bot-vs-bot
playback, analytics inspection) use it directly.

`BackgammonPlayEntry` is the **stateful play-entry widget** — it composes
`BackgammonDiagram` and drives a `BgMoveGen.MoveEntryState` from its click
events, rebuilding the displayed `Mop` from the intermediate position after
each legal click and reporting the assembled `Play` once the user has
clicked a complete legal sequence. Handles play decisions only
(`Decision.IsCube == false`); cube decisions throw at the contract boundary.

`BackgammonCubeEntry` is the **cube-entry sibling** to `BackgammonPlayEntry`
— it composes `BackgammonDiagram` and presents the cube decision as two
independent atomic button groups (doubler: No Double / Double; taker:
Take / Pass), firing `EventCallback<CubeDecisionPair>` once both halves are
selected. It emits the user's raw answer; scoring it against the correct
action is the future quiz layer's job. Handles cube decisions only
(`Decision.IsCube == true`); play decisions throw at the contract boundary.
Consumers route by `Decision.IsCube`.

The split keeps the encapsulation rule clean: a consumer that just wants to
display a position should not pay for click-by-click state machinery, and a
consumer driving an interactive decision should not have to wire state
externally to a view-only component or branch internally on cube vs play.

### Render pipeline

`BackgammonDiagram` is a `ComponentBase` with `[Parameter]` `Request` (nullable
`DiagramRequest`) and `Options` (`DiagramOptions`, defaulted). `DiagramRenderer`
is a static class in the lib, so rendering is a direct static call — the
component holds no renderer state, only the cached outputs.

`OnParametersSet` is the single render hook:

- If `Request` is null, both `_svgMarkup` and `_hitRegions` are cleared and
  the component renders nothing.
- Otherwise, it calls `DiagramRenderer.RenderSvg(Request, Options)` and
  `DiagramRenderer.GetHitRegions(Request, Options)` and caches both in fields.

The markup then injects `_svgMarkup` via `(MarkupString)_svgMarkup` inside a
child `div` that has `pointer-events: none` so clicks fall through to the
overlay.

### Transparent SVG click overlay

Click handling is pure Razor — no JS interop. A second `<svg>` element is
positioned absolutely over the rendered diagram, sized via the
`BoardHitRegions.ViewBox` so it aligns 1:1 with the lib's SVG coordinate
system. Each `HitRect` in the region map becomes a `<rect>` with
`fill="transparent"`, `pointer-events="all"`, and an `@onclick` that invokes
the matching `EventCallback`:

- `Points` dictionary → one `<rect>` per point, invokes `OnPointClicked` with
  the point index (1–24).
- `Bar` → invokes `OnBarClicked(25)`.
- `Cube` (nullable) → invokes `OnCubeClicked`.
- `OnRollTray` (the on-roll player's bearing-off tray, nullable) → invokes
  `OnTrayClicked`.

The overlay is the second child of the outer wrapper so it sits above the
pointer-events-disabled diagram in stacking order.

### BackgammonDiagram — catch-all attributes

`[Parameter(CaptureUnmatchedValues = true)] Dictionary<string, object>? AdditionalAttributes`
is splatted onto the outer `bg-diagram` wrapper `div` via `@attributes`, so
consumers can pass `style`, `id`, `class`, etc. without modifying the component.

### BackgammonPlayEntry — render pipeline

`BackgammonPlayEntry` takes the same `DiagramRequest` / `DiagramOptions`
shape as `BackgammonDiagram` plus an `EventCallback<Play> OnPlayCompleted`.
Internally:

- `_state` holds a `MoveEntryState`, constructed from
  `BoardState.FromMop(Request.Position.Mop)` and `Request.Decision.Dice`.
- `_renderedRequest` is the `DiagramRequest` actually handed to the inner
  `BackgammonDiagram`; it is rebuilt on every state change via
  `DiagramRequest.Builder.From(Request)` with `Mop` patched from
  `_state.Current.ToMop()`. Other fields (names, cube, orientation) flow
  through unchanged.
- Click handlers implement **one-click source-advance**: `OnPointClicked` /
  `OnBarClicked` route through
  `_state.TryAdvanceFrom(point, diePreference)` — a single click commits one
  move from that source, the die chosen by `diePreference` (the rendered dice
  order, leftmost first; see the dice swap below). `OnTrayClicked` routes
  through `_state.TryBearOffMax()` — the bear-off-max shortcut, which bears off
  the maximum number of checkers when that is unambiguous. Both rebuild
  `_renderedRequest` on any outcome other than `Illegal`; `PlayCompleted` fires
  `OnPlayCompleted`.
- The dice click (`OnDiceClicked`) is display/submit, not entry: on a complete
  play it fires `OnSubmitRequested`; on an incomplete play it toggles a
  display-only dice swap (a no-op for doubles) that reorders only the rendered
  dice — and thereby which die a one-click advance prefers. The incoming
  `Request` and `MoveEntryState` are untouched, so the swap never disturbs the
  reset key or in-progress entry.

`AdditionalAttributes` is splatted onto `BackgammonPlayEntry`'s own outer
`bg-play-entry` wrapper `div` — a separate wrapper above the inner
`BackgammonDiagram`'s `bg-diagram` wrapper, not the same element. Consumers
that style the play-entry widget target `bg-play-entry`; the inner diagram's
splat surface is reached via the inner component's own parameter, which
this component does not forward.

### Reset semantics — value equality on `(Mop, Dice)`

A fresh `MoveEntryState` is constructed only when the incoming `Request`'s
starting `(Position.Mop, Decision.Dice)` differs value-wise from the
previously cached pair. Re-passing a request with the same starting position
and dice — even a distinct object reference — preserves any in-progress
click state. Different starting position or dice triggers a reset.

This decouples reset behavior from object identity: consumers can rebuild a
`DiagramRequest` for any reason (parent-state churn, attribute change, etc.)
without losing mid-click progress, while genuinely advancing to a new
problem unambiguously resets.

### Cube-decision guard

Cube decisions (signaled by `Decision.IsCube == true`) are not handled by
`BackgammonPlayEntry`. `OnParametersSet` throws `NotImplementedException`
naming `BackgammonCubeEntry` as the correct sibling. The intent is to fail
loudly at the contract boundary rather than silently render an unusable
widget. `BackgammonCubeEntry` carries the symmetric guard — play decisions
throw on it.

### BackgammonCubeEntry — render pipeline

`BackgammonCubeEntry` takes the same `DiagramRequest` / `DiagramOptions`
shape as `BackgammonPlayEntry` plus an
`EventCallback<CubeDecisionPair> OnCubeDecisionCompleted`. The composition
is structurally simpler than the play-entry widget:

- The inner `BackgammonDiagram` receives `Request` unchanged — cube
  decisions do not transform the position, so there is no Mop rebuilding
  step and no `_renderedRequest` cache.
- No `MoveEntryState`, no `BgMoveGen` dependency.
- Two `role="group"` button groups render alongside the inner diagram
  inside the `bg-cube-entry` wrapper:
  - **Doubler** (`bg-cube-doubler`): "No Double" (`CubeAction.NoDouble`) /
    "Double" (`CubeAction.Double`).
  - **Taker** (`bg-cube-taker`): "Take" (`CubeAction.Take`) /
    "Pass" (`CubeAction.Pass`).
  Each option is a `<button class="bg-cube-action">`; the currently chosen
  option in a group additionally carries `bg-cube-action-selected` for a
  visible selected state.

The two `(action, label)` mappings are private static tables in the
code-behind, one per group. Single-sourced for now; if a second consumer
ever needs the same labels, lift to a shared helper at that point.

`AdditionalAttributes` is splatted onto `BackgammonCubeEntry`'s own outer
`bg-cube-entry` wrapper `div`, parallel to `BackgammonPlayEntry`'s
`bg-play-entry` wrapper. Consumers that style the cube-entry widget
target `bg-cube-entry`; the two action groups are `bg-cube-doubler` and
`bg-cube-taker`.

### BackgammonCubeEntry — gating, re-fire, and reset

The component holds two provisional fields, `CubeAction? _doubler` and
`CubeAction? _taker`. Each button click sets its group's field and marks
that button selected; there is no lock, so re-selecting within a group is
allowed. `OnCubeDecisionCompleted` fires whenever *both* halves are set
after a click, carrying `new CubeDecisionPair(_doubler, _taker)` — so a
selection change after completion re-fires with the updated pair and the
consumer always holds the current complete answer. Selecting only one half
does not fire.

Because the doubler group can only produce a doubler-half action and the
taker group a taker-half action, the `CubeDecisionPair` constructed here
always satisfies that type's half-guards — construction never throws in
this component. The component emits the raw answer only; it does not encode
which combination is correct, and holds no aggregate "is this the right
cube action" judgment. Scoring the pair is the future quiz layer's
responsibility.

**Reset semantics** mirror `BackgammonPlayEntry`'s value-equality key,
with dice dropped: cube decisions carry no dice (`[0, 0]` by the data-layer
invariant), so the starting `Position.Mop` alone identifies the problem.
`_doubler` / `_taker` reset to null only when the incoming `Request`'s `Mop`
differs value-wise from the cached one; re-passing the same logical
position — even a distinct object reference — preserves provisional state.

### Click index conventions

Match `MoveEntryState`'s contract and the inner diagram's event surface:

- `1..24` — regular board points (advance source: a click commits a move from
  that point via `TryAdvanceFrom`).
- `25` — on-roll player's bar (advance source / entry, if a bar checker is
  present).

`OnPointClicked` carries 1–24 and `OnBarClicked` always emits 25 — both routed
to `TryAdvanceFrom`. `OnTrayClicked` is parameter-less; it is not a click index —
it routes to `TryBearOffMax` (the bear-off-max shortcut). Bearing off a single
checker is an ordinary advance from its home point, not a tray click.

### Test project

bUnit + xUnit, targets .NET 10. `BackgammonDiagramTests` cover the view-only
primitive (markup, hit-region overlay, callback wiring). `BackgammonPlayEntryTests`
cover the play-entry contract: legal-completion firing, illegal no-ops,
post-completion no-ops, undo round-trip via replay, value-equality reset on
`(Mop, Dice)` change, identity preservation on equal `(Mop, Dice)`,
cube-decision rejection. `BackgammonCubeEntryTests` cover the cube-entry
contract: render shape (inner diagram + both two-button groups), null-request
empty render, splat surface, play-decision rejection (symmetric guard),
selecting one doubler + one taker fires `OnCubeDecisionCompleted` exactly
once with the matching `CubeDecisionPair` (parameterized over the full 2×2
grid), single-half selection does not fire (gating), re-selecting a half
after completion re-fires with the latest pair, and the selected button
carries the `bg-cube-action-selected` state class.

## Public API

All three components live in namespace `BgDiag_Razor.Components`.

### `BackgammonDiagram`

**Parameters:**

- `DiagramRequest? Request` — the position and match state to render. Null
  renders nothing.
- `DiagramOptions Options` — rendering options (defaults to `new()`).
- `Dictionary<string, object>? AdditionalAttributes` — splatted onto the
  outer wrapper `div`.

**EventCallbacks:**

- `EventCallback<int> OnPointClicked` — fired with point index 1–24.
- `EventCallback<int> OnBarClicked` — fired with 25.
- `EventCallback OnCubeClicked` — fired when the cube region is clicked.
- `EventCallback OnTrayClicked` — fired when the on-roll player's bearing-off
  tray is clicked.
- `EventCallback OnDiceClicked` — fired when the dice region is clicked
  (rendered only when a dice hit-region exists, i.e. not for cube decisions).
  The view forwards the click; it does not interpret it.

### `BackgammonPlayEntry`

**Parameters:**

- `DiagramRequest? Request` (required) — initial position and dice. Null
  renders nothing. Cube decisions (`Decision.IsCube == true`) throw
  `NotImplementedException`.
- `DiagramOptions Options` — forwarded to the inner diagram.
- `Dictionary<string, object>? AdditionalAttributes` — splatted onto the
  outer wrapper `div`.

**EventCallbacks:**

- `EventCallback<Play> OnPlayCompleted` — fires once when the click sequence
  assembles a complete legal `Play`. Does not fire for pass positions or
  partial / illegal sequences.
- `EventCallback OnSubmitRequested` (**required**, `[EditorRequired]`) —
  parameterless; fires when the user clicks the dice on a *complete* play,
  signalling submit intent. The component stays submit-oblivious: it only
  signals (the consumer already holds the `Play` from `OnPlayCompleted`), and
  the consumer binds this to its own submit action. Marked `[EditorRequired]`
  so a consumer that forgets to bind it surfaces an `RZ2012` warning rather
  than silently dropping the submit affordance.

**Imperative methods** (call via `@ref`):

- `void UndoLast()` — undoes the last committed move (no-op if none).
- `void UndoAll()` — restores the initial position, discarding all committed
  moves; allowed even after completion (consumer can expose this as
  "redo from start").

### `BackgammonCubeEntry`

**Parameters:**

- `DiagramRequest? Request` (required) — the cube decision to render and
  accept an answer against. Null renders nothing. Play decisions
  (`Decision.IsCube == false`) throw `NotImplementedException`.
- `DiagramOptions Options` — forwarded to the inner diagram.
- `Dictionary<string, object>? AdditionalAttributes` — splatted onto the
  outer wrapper `div` (`bg-cube-entry`). Not forwarded to the inner diagram.

**EventCallbacks:**

- `EventCallback<CubeDecisionPair> OnCubeDecisionCompleted` (**required**,
  `[EditorRequired]`) — fires when both halves of the cube decision have
  been selected, carrying the user's answer as a `CubeDecisionPair`. Re-fires
  whenever a selection changes after completion (no one-shot lock); does not
  fire while only one half is selected.

**Imperative methods:** none. Cube decisions have no sub-state worth rolling
back, so the play-entry-style `UndoLast` / `UndoAll` does not apply.

## BackgammonPlayEntry — pitfalls

- **Reset key is value-equality on `(Mop, Dice)`, not reference identity.**
  Tests and consumers must rebuild a `DiagramRequest` with a *different*
  starting position or dice to force a reset. Re-passing the same logical
  problem — even a freshly built request instance — does not reset state.
- **`UndoLast` / `UndoAll` invoke `StateHasChanged`** which requires the
  Blazor Dispatcher. Real consumers (button click handlers) are already on
  the Dispatcher; bUnit tests must wrap the call in `cut.InvokeAsync(...)`.
- **Pass positions do not auto-fire `OnPlayCompleted`.** When no legal play
  exists, `MoveEntryState.IsComplete` is true at construction but the
  component does not emit a synthetic `OnPlayCompleted`. Consumers handle
  pass positions via their own skip-to-next-problem flow.
- **Cube decisions are rejected at the contract boundary.** A `DiagramRequest`
  with `Decision.IsCube == true` throws `NotImplementedException` from
  `OnParametersSet`. Route cube decisions to `BackgammonCubeEntry`, which
  carries the symmetric guard on play decisions.
- **The inner diagram's cube hit region still renders** (because
  `BackgammonDiagram` always wires it) but `BackgammonPlayEntry` does not
  subscribe; cube-area clicks during play entry are no-ops by design.

## BackgammonCubeEntry — pitfalls

- **Play decisions are rejected at the contract boundary.** A
  `DiagramRequest` with `Decision.IsCube == false` throws
  `NotImplementedException` from `OnParametersSet`. Route play decisions to
  `BackgammonPlayEntry` (which carries the symmetric guard). Consumers
  branch on `Request.Decision.IsCube`; do not pass a play request to this
  component "to render the position without buttons" — the guard is
  deliberate.
- **The inner diagram's cube hit region still renders** (because
  `BackgammonDiagram` always wires it) but `BackgammonCubeEntry` does not
  subscribe; cube-region clicks on the diagram are no-ops by design. The
  decision is entered via the two button groups (`bg-cube-doubler`,
  `bg-cube-taker`), not via the diagram's hit-regions.
- **`OnCubeDecisionCompleted` is `[EditorRequired]`.** Unlike the sibling
  `BackgammonPlayEntry.OnPlayCompleted`, this callback is marked required so
  a Razor consumer that omits it — or, after a future rename, leaves a stale
  attribute on the tag — surfaces the missing binding as an `RZ2012` compile
  warning rather than splatting silently (Razor does not error on
  unrecognized component attributes). Build with warnings-as-errors to make
  that a hard gate. The sibling could adopt the same guard later.
- **Fires when both halves are set, and re-fires on later changes.** There
  is no one-shot lock: once both groups are selected, every subsequent
  selection change re-fires with the updated pair. A consumer wanting
  one-shot semantics advances to the next problem on the first callback.
  Tests pin both the gating (single half → no fire) and the re-fire contract.
- **Provisional state resets on `Mop` change, not reference identity.** The
  reset key is value-equality on `Position.Mop` (cube dice are always
  `[0, 0]`, so they are not part of the key). Re-passing the same logical
  position — even a freshly built `DiagramRequest` — preserves any
  in-progress half-selection; a different starting position resets both
  halves.
- **No imperative `UndoLast` / `UndoAll`.** A cube decision has no sub-state
  to roll back beyond the two provisional halves, which a consumer clears by
  advancing to a new position. There is no play-entry-style undo surface.

## BackgammonDiagram — pitfalls

- **Overlay viewBox must match the lib's SVG.** The overlay is sized from
  `BoardHitRegions.ViewBox` so hit rects align with the rendered diagram. If
  the lib's viewBox ever diverges from what `GetHitRegions` reports, clicks
  will land on the wrong elements. Keep both coming from the same source.
- **Inner diagram needs `pointer-events: none`.** The rendered lib SVG sits
  underneath the overlay in a `<div style="pointer-events: none">`. Removing
  that style makes the diagram swallow clicks before they reach the overlay
  rects.
- **Overlay element order is load-bearing.** The transparent hit-region
  `<svg>` is the second child of the wrapper so it stacks above the diagram.
  Don't reorder the two children or wrap them separately — overlap and z-order
  come from DOM order, not CSS positioning alone.
- **`GetHitRegions` needs `Request`, not just `Options`.** Orientation and
  on-roll tray positioning depend on match state, not just the option set.
  `OnParametersSet` passes both; don't "optimize" by caching hit regions
  keyed on options alone.
- **`MarkupString` is trusted-output-only.** The SVG injected via
  `(MarkupString)_svgMarkup` is produced by `BackgammonDiagram_Lib` and is
  trusted. Never pass externally supplied HTML through `MarkupString` in this
  component — Blazor skips encoding it, and anything coming from outside the
  lib would be an XSS vector.
- **No intrinsic size.** The component renders an SVG wrapper with no
  explicit width/height. Consumers must put it inside a sized container or
  pass size via `AdditionalAttributes`, otherwise the diagram has zero
  layout size.

## Subproject-internal next steps

- **`BackgammonDiagram` highlight parameters.** Add `HighlightedPoints`
  (set of point indices) and `SelectedPoint` (single index, optional)
  parameters to the view-only primitive, rendered as translucent overlays
  using the same `BoardHitRegions` machinery the click overlay already uses.
  `BackgammonPlayEntry` then forwards `state.LegalNextClicks` (the points you
  can advance from now) in a one-line plumbing change. Unlocks legal-hint
  hover and any future point-highlighting consumer in a single small addition. Index→rect mapping stays inside
  `BackgammonDiagram` where the rest of it lives — no consumer-side leak.
- **Migrate off `MarkupString` injection.** Once `BackgammonDiagram_Lib`
  exposes a rendering API that emits structured elements rather than a
  single SVG string, replace the `(MarkupString)` injection with a native
  Razor SVG tree. Removes the XSS footgun and makes the component
  diff-friendly.
- **Fold hit regions into the main SVG.** Once rendering is Razor-native,
  the click overlay can become additional `<rect>` elements inside the same
  `<svg>` rather than a parallel absolutely-positioned sibling, eliminating
  the pointer-events / stacking plumbing.
