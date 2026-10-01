#!/usr/bin/env python3
"""Build the organized delivery folder — the actual product.

    python3 scripts/export_assets.py [--project PATH] [--skip-png]

Produces out/assets/ exactly as described in references/09-deliverables-and-export.md:
logo and symbol in every variation as SVG + PNG @1x/2x/4x, the favicon and app-icon
set, the fonts with their licenses, palette specs (md / json / css / ase), the icon
set, the board, the pieces, the mockups, and a README a non-designer can follow.
"""
from __future__ import annotations

import argparse
import json
import shutil
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from lib.brandlib import (  # noqa: E402
    ensure, family_name, find_project, load_brand, palette_of, read_svg, slug,
)
from render import Renderer  # noqa: E402

PNG_SCALES = {"1x": 512, "2x": 1024, "4x": 2048}
FAVICONS = [16, 32, 48, 180, 512, 1024]


def ase_bytes(colors: list[dict]) -> bytes:
    """Minimal Adobe Swatch Exchange writer (RGB, global swatches)."""
    out = b"ASEF" + struct.pack(">HHI", 1, 0, len(colors))
    for c in colors:
        name = c["name"] + "\x00"
        nb = name.encode("utf-16-be")
        r, g, b = [v / 255 for v in c["rgb"]]
        block = struct.pack(">H", len(name)) + nb + b"RGB " + struct.pack(">fff", r, g, b) \
            + struct.pack(">H", 0)
        out += struct.pack(">HI", 0x0001, len(block)) + block
    return out


def palette_md(brand: dict) -> str:
    rows = ["# Color palette", "",
            "CMYK values are conversions and PMS values are nearest matches — "
            "both must be confirmed on a physical proof.", ""]
    for c in palette_of(brand):
        rows += [
            f"## {c['name']}  ({c['role'] or 'unassigned'})", "",
            f"- HEX  `{c['hex']}`",
            f"- RGB  `{c['rgb'][0]} {c['rgb'][1]} {c['rgb'][2]}`",
            f"- CMYK `{' '.join(str(v) for v in c['cmyk'])}`",
            f"- PMS  `{c['pms'] or 'to be specified'}`", "",
        ]
    return "\n".join(rows)


def tokens(brand: dict) -> tuple[str, str]:
    pal = palette_of(brand)
    js, cs = {}, [":root{"]
    for c in pal:
        key = slug(c["name"])
        js[key] = c["hex"]
        cs.append(f"  --brand-{key}: {c['hex']};")
    cs.append("}")
    return json.dumps(js, indent=2), "\n".join(cs)


def readme(brand: dict, families: list[str]) -> str:
    n = brand["brand"]["name"]
    return f"""# {n} — brand assets

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
- Type is set in {' and '.join(families)}. Both are in `03-typography/fonts/`
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
"""


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--project", default=None)
    ap.add_argument("--skip-png", action="store_true")
    a = ap.parse_args()

    project = Path(a.project).resolve() if a.project else find_project()
    brand = load_brand(project)
    gen = project / "identity" / "generated"
    out = project / "out" / "assets"

    dirs = {k: out / k for k in (
        "01-logo", "02-symbol", "03-typography", "04-color", "05-icons",
        "06-board", "07-pieces", "08-mockups", "09-templates")}
    ensure(out, *dirs.values())

    # ---- 01 logo / 02 symbol ------------------------------------------------
    lockups = {"01-logo": ("horizontal", "primary"), "01-logo/stacked": ("stacked", None)}
    ways = ["primary", "reverse", "black", "white", "mono", "brand", "accent"]
    jobs: list[tuple[Path, Path]] = []

    for lock, base in (("horizontal", dirs["01-logo"]), ("stacked", dirs["01-logo"] / "stacked")):
        for way in ways:
            src = gen / "variations" / f"{lock}-{way}.svg"
            if not src.exists():
                continue
            d = base / way
            ensure(d)
            dest = d / f"logo-{lock}-{way}.svg"
            shutil.copy(src, dest)
            jobs.append((dest, d))

    for way in ways:
        src = gen / "variations" / f"symbol-{way}.svg"
        if src.exists():
            dest = dirs["02-symbol"] / f"symbol-{way}.svg"
            shutil.copy(src, dest)
            jobs.append((dest, dirs["02-symbol"]))
    for name in ("logomark", "horizontal", "stacked"):
        s = gen / f"lockup-{name}.svg" if name != "logomark" else gen / "logomark.svg"
        if s.exists():
            shutil.copy(s, dirs["01-logo"] / s.name)

    # ---- 03 typography ------------------------------------------------------
    fdir = dirs["03-typography"] / "fonts"
    ensure(fdir)
    for f in (project / "fonts").glob("*"):
        shutil.copy(f, fdir / f.name)
    scale = json.loads((gen / "type-scale.json").read_text())
    md = ["# Type scale", "",
          f"Modular scale — base {scale['base']}px, ratio {scale['ratio']}.", "",
          "| Step | Size | Line height | Tracking | Weight |", "|---|---|---|---|---|"]
    for s in scale["steps"]:
        md.append(f"| {s['name']} | {s['px']}px | {s['line_height']} | "
                  f"{s['tracking_em'] * 100:+.1f}% | {s['weight']} |")
    families = [family_name(v["fontsource"]) for v in brand["typography"].values()
                if isinstance(v, dict) and "fontsource" in v]
    md += ["", "Families: " + ", ".join(families) + "."]
    for k, v in brand["typography"].items():
        if isinstance(v, dict) and v.get("substituted_for"):
            md.append(f"\n> `{family_name(v['fontsource'])}` stands in for "
                      f"**{v['substituted_for']}** — swap it in production.")
    (dirs["03-typography"] / "type-scale.md").write_text("\n".join(md), encoding="utf-8")

    # ---- 04 color -----------------------------------------------------------
    (dirs["04-color"] / "palette.md").write_text(palette_md(brand), encoding="utf-8")
    tj, tc = tokens(brand)
    (dirs["04-color"] / "tokens.json").write_text(tj, encoding="utf-8")
    (dirs["04-color"] / "tokens.css").write_text(tc, encoding="utf-8")
    (dirs["04-color"] / "palette.ase").write_bytes(ase_bytes(palette_of(brand)))
    cr = gen / "contrast-report.txt"
    if cr.exists():
        shutil.copy(cr, dirs["04-color"] / "contrast-report.txt")

    # ---- 05 icons -----------------------------------------------------------
    isvg = dirs["05-icons"] / "svg"
    ensure(isvg)
    icon_files = []
    for f in sorted((gen / "icons").glob("*.svg")):
        shutil.copy(f, isvg / f.name)
        if f.name != "icons.svg":
            icon_files.append(isvg / f.name)
    shutil.copy(gen / "icons" / "icons.svg", dirs["05-icons"] / "icons.svg")

    # ---- 06/07/08 renders ---------------------------------------------------
    for src, dst in ((project / "out" / "board", dirs["06-board"]),
                     (project / "out" / "pieces", dirs["07-pieces"]),
                     (project / "out" / "mockups", dirs["08-mockups"])):
        if src.exists():
            for f in src.glob("*.png"):
                shutil.copy(f, dst / f.name)
    if (project / "pages" / "board.html").exists():
        shutil.copy(project / "pages" / "board.html", dirs["06-board"] / "board.html")

    # ---- rasterize ----------------------------------------------------------
    if not a.skip_png:
        with Renderer() as r:
            for src, d in jobs:
                for label, px in PNG_SCALES.items():
                    r.svg_png(src, d / f"{src.stem}-{label}.png", px)
            sym = gen / "variations" / "symbol-primary.svg"
            sym_w = gen / "variations" / "symbol-white.svg"
            if sym.exists():
                for px in FAVICONS:
                    r.svg_png(sym, dirs["02-symbol"] / f"favicon-{px}.png", px,
                              background="#FFFFFF" if px <= 180 else None)
                r.svg_png(sym_w, dirs["02-symbol"] / "app-icon-1024.png", 1024,
                          background="#0D0D0D")
                r.svg_png(sym_w, dirs["02-symbol"] / "social-avatar-1000.png", 1000,
                          background="#0D0D0D")
            ipng = dirs["05-icons"] / "png"
            ensure(ipng)
            for f in icon_files:
                for px in (24, 48, 96):
                    r.svg_png(f, ipng / f"{f.stem}-{px}.png", px)
            lh = out / "09-templates" / "letterhead.html"
            if lh.exists():
                r.pdf(lh, out / "09-templates" / "letterhead.pdf")

    (out / "README.md").write_text(readme(brand, families), encoding="utf-8")

    total = sum(1 for _ in out.rglob("*") if _.is_file())
    print(f"✓ {total} file(s) → {out}")
    print("  next: python3 scripts/build_manual.py, then write RATIONALE.md")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
