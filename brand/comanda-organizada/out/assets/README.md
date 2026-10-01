# Varthex — brand assets

Everything here is generated from `brand.json`. If something needs to change,
change it there and re-run the scripts; do not edit these files by hand.

## Which file do I use?

| Situation | File |
|---|---|
| On a white or light background | `01-logo/primary/` |
| On a dark background or a photo | `01-logo/white/` |
| One-color print, stamp, engraving | `01-logo/black/` |
| Avatar, favicon, app icon | `02-symbol/` |
| Square-ish space (merch, signage) | `01-logo/stacked/` |

**Use the SVG whenever the tool accepts one.** PNGs are a fallback for software
that cannot place vector art; they do not scale up.

## Rules that are not negotiable

- Keep the clear space. Nothing enters the exclusion zone.
- Never stretch, rotate, recolor, or add effects to the mark.
- Below the minimum size, switch to a smaller lockup — do not shrink further.
- Type is set in Manrope and Ibm Plex Mono. Both are in `03-typography/fonts/`
  with their licenses.

## What is in here

```
01-logo/      the full lockup in every colorway (SVG + PNG)
02-symbol/    the symbol alone, favicons, app icon, social avatar
03-typography/fonts, the type scale, the specimen
04-color/     palette specs, design tokens, .ase swatches
05-icons/     the icon set, individually and as a sprite
06-board/     the brand board
07-pieces/    every board panel on its own
08-mockups/   the collateral renders
09-templates/ email signature and letterhead you can actually use
BRAND-MANUAL.pdf · RATIONALE.md
```

Questions about an application not covered here go to whoever owns the brand
before the file is used — that is what the manual is for.
