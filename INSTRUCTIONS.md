# BgDiag_Razor — Instructions

## Purpose
Thin Razor class library wrapper around **BackgammonDiagram_Lib**.
Exposes a `BackgammonDiagram.razor` component that calls `DiagramRenderer.RenderSvg()`
and injects the result as `MarkupString`.
Kept separate so the core library has no Blazor/Razor dependency.

## Repo
- GitHub: `halheinrich/BgDiag_Razor`
- Umbrella submodule path: `BgDiag_Razor`
- Local path: `D:\Users\Hal\Documents\Visual Studio 2026\Projects\backgammon\BgDiag_Razor`

## Stack
- C# / .NET 10
- Razor Class Library (`Microsoft.NET.Sdk.Razor`)
- Visual Studio 2026

  ## Dependencies
- **BackgammonDiagram_Lib** — referenced as a project dependency
  - Commit: `01432a7`
  - Key types consumed: `DiagramRequest`, `DiagramOptions`, `DiagramRenderer`, `BoardHitRegions`, `SvgViewBox`, `HitRect`
  - API entry points:
    - `string DiagramRenderer.RenderSvg(DiagramRequest request, DiagramOptions options)`
    - `BoardHitRegions DiagramRenderer.GetHitRegions(DiagramOptions options)`

## Umbrella
- Repo: `halheinrich/backgammon`
- Commit: `2e4fece`
- AGENTS.md lives at umbrella root — this project references it, does not keep its own copy.

## Component API
- `BackgammonDiagram.razor` in `BgDiag_Razor.Components`
- Parameters: `DiagramRequest? Request`, `DiagramOptions Options`
- EventCallbacks (contract stubbed, not yet wired): `OnPointClicked(int)`, `OnCubeActionClicked`
- Renders SVG via `MarkupString`; future migration path to Razor-native SVG once lib rendering matures

## Session start
1. Fetch AGENTS.md from umbrella root.
2. Fetch this file (INSTRUCTIONS.md).
3. Fetch key source files as needed (update hashes below after commits).

### Source file URLs
```
https://raw.githack.com/halheinrich/BgDiag_Razor/15e8d93/BgDiag_Razor/Components/BackgammonDiagram.razor
https://raw.githack.com/halheinrich/BgDiag_Razor/15e8d93/BgDiag_Razor/Components/BackgammonDiagram.razor.cs
https://raw.githack.com/halheinrich/BgDiag_Razor/15e8d93/BgDiag_Razor.Tests/BackgammonDiagramTests.cs
```

## Commit log
| Date | Hash | Summary |
|------|------|---------|
| 2026-03-29 | `6b0a447` | Initial scaffold: BackgammonDiagram component, bunit tests |
| 2026-03-30 | `6b0a447` | Wire click overlay: hit regions, EventCallbacks, bUnit tests |
| 2026-03-31 | '3c521c9  | Fix GetHitRegions call site to pass Request; add orientation regression test
| 2026-03-31 | `15e8d93` | Fix DiagramRequest construction: replace object initializer and with-expressions with Builder pattern
