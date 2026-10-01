# Scripts

Everything derives from `brand.json` plus the hand-authored SVGs in
`identity/src/`. Nothing is edited by hand downstream — change the source and
re-run.

## The pipeline

```bash
# once per project
python3 scripts/fetch_fonts.py            # families named in brand.json, from npm

# Phase 1 — identity
python3 scripts/build_identity.py         # lockups, variations, clearspace, grid, icons
python3 scripts/pick_layout.py --write    # choose the board layout from the brief
python3 scripts/build_board.py            # pages/board.html in that layout
python3 scripts/render.py --board --pieces
python3 scripts/render.py --tests         # the critique renders — look at them

# Phase 2 — collateral (after approval)
python3 scripts/build_mockups.py --all
python3 scripts/render.py --mockups

# delivery
python3 scripts/export_assets.py          # out/assets/ — the organized folder
python3 scripts/build_manual.py           # BRAND-MANUAL.pdf
```

Every script accepts `--project PATH`; without it they walk up from the current
directory looking for `brand.json`.

## What each one does

| Script | Reads | Writes |
|---|---|---|
| `fetch_fonts.py` | `brand.json → typography.*.fontsource` | `fonts/*.woff2`, `*.woff`, licenses |
| `build_identity.py` | `identity/src/symbol.svg`, fonts | `identity/generated/**` |
| `pick_layout.py` | `brand.json` (brief, palette, concept) | `brand.json → board.layout`, `board.layout_reason` |
| `build_board.py` | generated SVGs, `board.layout` | `pages/board.html` · `pages/compare/board-*.html` |
| `build_mockups.py` | generated SVGs | `pages/mockups/*.html`, `out/assets/09-templates/*` |
| `render.py` | the HTML pages, any SVG | `out/board`, `out/board/compare`, `out/pieces`, `out/mockups`, `out/tests` |
| `export_assets.py` | everything above | `out/assets/**` |
| `build_manual.py` | everything above | `pages/manual.html`, `BRAND-MANUAL.pdf` |

## Flags worth knowing

```
fetch_fonts.py   --family inter --weights 400,600,700 [--italic] [--list] [--clean]
pick_layout.py   [--write] [--layout studio|mosaic|flood|flood-wide|editorial|gallery]
                 [--keep-mode] [--list] [--json]
build_board.py   [--layout NAME]     # preview one layout without saving
build_board.py   --compare           # every eligible layout → pages/compare/
build_mockups.py --all | --only card,phone,email,contract
render.py        --board --pieces --mockups --tests --compare --all
render.py        --svg path/to.svg --widths 512,1024,2048 [--out DIR]
export_assets.py --skip-png          # SVG-only pass, much faster while iterating
build_manual.py  --version v1.2
```

## Library

- `lib/brandlib.py` — project discovery, SVG utilities (`read_svg`,
  `svg_recolor`, `set_svg_size`), color math (contrast, CIE L\*, CMYK, perceptual
  neutral ramps), font inlining as base64.
- `lib/svgtext.py` — fontTools text → outlined SVG paths, with kerning and
  em-unit tracking. This is what makes the wordmark shippable as geometry
  instead of live text.
- `lib/studio.py` — the CSS/SVG studio objects (business card, phone, puck,
  letterhead, email signature, app icon) and the shared lighting language.
- `lib/layouts.py` — the six board layouts (reference file, colour mode,
  signal words) and the scoring/tie-break logic `pick_layout.py` and
  `build_board.py` share.

## Notes

- **Offline by design.** Google Fonts is unreachable from the render container;
  `@fontsource` on the npm registry is not. Every generated page inlines its
  fonts as base64, so the HTML is self-contained and renders identically in
  Chromium, in a browser, and as a published Artifact.
- **Rendering** goes through Playwright's bundled Chromium. Do not run
  `playwright install` — the browser is already present.
- **PNG scales** default to 512 / 1024 / 2048 px wide for `@1x/2x/4x`. Change
  `PNG_SCALES` in `export_assets.py` if a client needs different sizes.
- The mockups are **stylized renders**, not photographs. Say so when presenting.
