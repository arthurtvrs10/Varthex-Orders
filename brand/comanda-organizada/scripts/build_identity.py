#!/usr/bin/env python3
"""Generate every derived identity asset from brand.json + the hand-authored symbol.

Reads   identity/src/symbol.svg   (and optionally a custom wordmark svg)
Writes  identity/generated/
          wordmark.svg  descriptor.svg  logomark.svg
          lockup-horizontal.svg  lockup-stacked.svg  symbol.svg
          variations/<lockup>-<colorway>.svg
          clearspace.svg  grid.svg
          icons/<name>.svg  icons.svg (sprite)  icons-grid.svg
          type-scale.json  palette.json  contrast-report.txt

Run:  python3 scripts/build_identity.py [--project PATH]
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from lib import svgtext  # noqa: E402
from lib.brandlib import (  # noqa: E402
    contrast, contrast_verdict, ensure, find_font_file, find_project, load_brand,
    palette_of, read_svg, svg_document, svg_recolor,
)

# --------------------------------------------------------------------------


def symbol_geometry(project: Path, brand: dict) -> tuple[str, float, float]:
    """Return (inner markup, width, height) of the hand-authored symbol."""
    src = project / brand["logo"].get("symbol", "identity/src/symbol.svg")
    markup = read_svg(src)
    if not markup:
        raise SystemExit(f"Symbol not found: {src}\nAuthor it by hand first "
                         f"(see references/03-logo-construction.md).")
    vb = re.search(r'viewBox="([\d.\-\s]+)"', markup)
    if not vb:
        raise SystemExit(f"{src} has no viewBox. Add one, e.g. viewBox=\"0 0 100 100\".")
    x0, y0, w, h = [float(v) for v in vb.group(1).split()]
    inner = re.sub(r"^<svg[^>]*>|</svg>\s*$", "", markup, flags=re.S).strip()
    if x0 or y0:
        inner = f'<g transform="translate({-x0},{-y0})">{inner}</g>'
    return inner, w, h


def font_for(project: Path, brand: dict, role: str, weight: int) -> Path:
    spec = brand["typography"].get(role) or brand["typography"]["display"]
    p = find_font_file(project, spec["fontsource"], weight)
    if not p:
        raise SystemExit(
            f"No font file for {spec['fontsource']} {weight}. "
            f"Run: python3 scripts/fetch_fonts.py"
        )
    return p


# --------------------------------------------------------------------------
# element builders — each returns (markup, width, height)
# --------------------------------------------------------------------------


def build_wordmark(project: Path, brand: dict):
    wm = brand["logo"]["wordmark"]
    custom = wm.get("custom_svg")
    if custom:
        markup = read_svg(project / custom)
        vb = re.search(r'viewBox="([\d.\-\s]+)"', markup)
        _, _, w, h = [float(v) for v in vb.group(1).split()]
        inner = re.sub(r"^<svg[^>]*>|</svg>\s*$", "", markup, flags=re.S).strip()
        return inner, w, h, h
    font = font_for(project, brand, wm.get("font", "display"), wm.get("weight", 700))
    r = svgtext.outline(font, wm.get("text") or brand["brand"]["name"],
                        size=wm.get("size", 100), tracking=wm.get("tracking", 0.0))
    inner = f'<g transform="translate(0,{r["cap_height"]:.3f})"><path d="{r["d"]}"/></g>'
    return inner, r["width"], r["cap_height"], r["cap_height"]


def build_descriptor(project: Path, brand: dict, cap_h: float):
    d = brand["logo"].get("descriptor") or {}
    text = d.get("text") or brand["brand"].get("descriptor")
    if not text:
        return None
    font = font_for(project, brand, d.get("font", "display"), d.get("weight", 500))
    probe = svgtext.outline(font, "H", size=100)
    cap_ratio = probe["cap_height"] / 100.0
    target_cap = cap_h * d.get("size_ratio", 0.34)
    size = target_cap / cap_ratio
    r = svgtext.outline(font, text, size=size, tracking=d.get("tracking", 0.06))
    inner = f'<g transform="translate(0,{r["cap_height"]:.3f})"><path d="{r["d"]}"/></g>'
    return inner, r["width"], r["cap_height"]


def build_logomark(project: Path, brand: dict):
    """Wordmark + subordinate descriptor stacked — the 'logo mark' tile."""
    wi, ww, wh, cap = build_wordmark(project, brand)
    desc = build_descriptor(project, brand, cap)
    if not desc:
        return wi, ww, wh
    di, dw, dh = desc
    cfg = brand["logo"].get("descriptor") or {}
    gap = cap * cfg.get("gap_ratio", 0.22)
    align = cfg.get("align", "center")
    width = max(ww, dw)
    wx = (width - ww) / 2 if align == "center" else (width - ww if align == "right" else 0)
    dx = (width - dw) / 2 if align == "center" else (width - dw if align == "right" else 0)
    inner = (
        f'<g transform="translate({wx:.3f},0)">{wi}</g>'
        f'<g transform="translate({dx:.3f},{wh + gap:.3f})">{di}</g>'
    )
    return inner, width, wh + gap + dh


def build_lockups(project: Path, brand: dict):
    sym, sw, sh = symbol_geometry(project, brand)
    lm, lw, lh = build_logomark(project, brand)
    _, _, _, cap = build_wordmark(project, brand)
    cfg = brand["logo"].get("lockup") or {}

    s_h = cap * cfg.get("symbol_scale", 1.5)
    k = s_h / sh
    s_w = sw * k
    sym_scaled = f'<g transform="scale({k:.5f})">{sym}</g>'

    # horizontal ------------------------------------------------------------
    gap = s_w * cfg.get("gap_ratio", 0.45)
    h_h = max(s_h, lh)
    horiz = (
        f'<g transform="translate(0,{(h_h - s_h) / 2:.3f})">{sym_scaled}</g>'
        f'<g transform="translate({s_w + gap:.3f},{(h_h - lh) / 2:.3f})">{lm}</g>'
    )
    horiz_size = (s_w + gap + lw, h_h)

    # stacked ---------------------------------------------------------------
    vgap = s_h * cfg.get("stacked_gap_ratio", 0.5)
    width = max(s_w, lw)
    stack = (
        f'<g transform="translate({(width - s_w) / 2:.3f},0)">{sym_scaled}</g>'
        f'<g transform="translate({(width - lw) / 2:.3f},{s_h + vgap:.3f})">{lm}</g>'
    )
    stack_size = (width, s_h + vgap + lh)

    return {
        "symbol": (sym, sw, sh),
        "logomark": (lm, lw, lh),
        "horizontal": (horiz, *horiz_size),
        "stacked": (stack, *stack_size),
    }


# --------------------------------------------------------------------------
# colorways
# --------------------------------------------------------------------------


def colorways(brand: dict) -> dict[str, str]:
    pal = {c["role"]: c["hex"] for c in brand.get("colors", []) if c.get("role")}
    neutrals = [c["hex"] for c in brand.get("colors", []) if c.get("role") == "neutral"]
    ways = {
        "primary": pal.get("ink", "#0D0D0D"),
        "reverse": pal.get("surface", "#FFFFFF"),
        "black": "#000000",
        "white": "#FFFFFF",
        "mono": neutrals[len(neutrals) // 2] if neutrals else "#7A7A7A",
    }
    if pal.get("brand"):
        ways["brand"] = pal["brand"]
    if pal.get("accent"):
        ways["accent"] = pal["accent"]
    return ways


# --------------------------------------------------------------------------
# clear space
# --------------------------------------------------------------------------


def build_clearspace(project: Path, brand: dict, lock, ink: str) -> str:
    inner, w, h = lock
    cfg = brand["logo"].get("clearspace") or {}
    _, _, _, cap = build_wordmark(project, brand)
    factor = cfg.get("factor", 1)

    glyph_markup, unit = "", cap * 0.55
    if cfg.get("unit_source", "glyph") == "glyph":
        wm = brand["logo"]["wordmark"]
        font = font_for(project, brand, wm.get("font", "display"), wm.get("weight", 700))
        g = svgtext.outline(font, cfg.get("unit_glyph", "a"), size=wm.get("size", 100))
        unit = g["x_height"] * factor
        glyph_markup = (
            f'<g transform="translate(0,{g["x_height"]:.3f})">'
            f'<path d="{g["d"]}" fill="{ink}"/></g>'
        )
        glyph_w = g["width"]
    else:
        glyph_w = unit

    pad = unit
    W, H = w + pad * 2, h + pad * 2
    line = 'stroke="%s" stroke-width="%.2f" stroke-opacity=".35"' % (ink, unit * 0.035)
    dash = f'{line} stroke-dasharray="{unit * 0.16:.2f} {unit * 0.12:.2f}" fill="none"'

    parts = [
        f'<rect x="0" y="0" width="{W:.2f}" height="{H:.2f}" {dash}/>',
        f'<rect x="{pad:.2f}" y="{pad:.2f}" width="{w:.2f}" height="{h:.2f}" '
        f'{line} stroke-opacity=".18" fill="none"/>',
        f'<g transform="translate({pad:.2f},{pad:.2f})" fill="{ink}">{inner}</g>',
    ]
    if glyph_markup:
        gx = (pad - glyph_w) / 2
        parts.append(f'<g transform="translate({gx:.2f},{(pad - unit) / 2:.2f})">{glyph_markup}</g>')
        parts.append(
            f'<g transform="translate({gx:.2f},{H - pad + (pad - unit) / 2:.2f})">{glyph_markup}</g>'
        )
    # dimension ticks on the left margin
    tx = pad * 0.5
    parts.append(f'<path d="M{tx:.2f} 0 V{pad:.2f}" {line}/>')
    parts.append(f'<path d="M{tx:.2f} {H - pad:.2f} V{H:.2f}" {line}/>')
    return svg_document("\n".join(parts), f"0 0 {W:.2f} {H:.2f}", round(W, 2), round(H, 2))


def build_grid_diagram(project: Path, brand: dict, ink: str) -> str:
    """The construction grid drawn over the symbol."""
    sym, w, h = symbol_geometry(project, brand)
    kind = brand["logo"].get("grid", "circle")
    g = ['<g stroke="%s" stroke-opacity=".28" fill="none" stroke-width="%.2f">' % (ink, w * 0.004)]
    cx, cy = w / 2, h / 2
    if kind == "circle":
        for r in (w / 2, w / 3, w / 4, w / 6):
            g.append(f'<circle cx="{cx}" cy="{cy}" r="{r:.2f}"/>')
        g.append(f'<path d="M{cx} 0 V{h} M0 {cy} H{w}"/>')
        g.append(f'<path d="M0 0 L{w} {h} M{w} 0 L0 {h}"/>')
    elif kind == "modular":
        n = 8
        for i in range(n + 1):
            g.append(f'<path d="M{w * i / n:.2f} 0 V{h} M0 {h * i / n:.2f} H{w}"/>')
    else:
        g.append(f'<path d="M{cx} 0 V{h} M0 {cy} H{w}"/>')
        for a in (30, 60, 120, 150):
            import math
            dx, dy = math.cos(math.radians(a)) * w, math.sin(math.radians(a)) * h
            g.append(f'<path d="M{cx - dx:.2f} {cy - dy:.2f} L{cx + dx:.2f} {cy + dy:.2f}"/>')
    g.append("</g>")
    body = f'<g fill="{ink}" fill-opacity=".92">{sym}</g>' + "\n".join(g)
    return svg_document(body, f"0 0 {w} {h}", w, h)


# --------------------------------------------------------------------------
# icons / type / color
# --------------------------------------------------------------------------


def build_icons(project: Path, brand: dict, out: Path) -> list[dict]:
    icons = brand.get("icons")
    if not icons:
        default = Path(__file__).parent.parent / "templates" / "icons-default.json"
        cfg = json.loads(default.read_text())
        icons = cfg["icons"]
        stroke, cap, join = cfg["stroke"], cfg["linecap"], cfg["linejoin"]
        print("  (using the default icon set — redraw it to the mark's DNA)")
    else:
        stroke = brand.get("iconography", {}).get("stroke", 1.75)
        cap = join = "round"

    ensure(out)
    made = []
    for ic in icons:
        if ic.get("file"):
            body = read_svg(project / ic["file"])
            body = re.sub(r"^<svg[^>]*>|</svg>\s*$", "", body, flags=re.S)
        elif ic.get("style") == "filled":
            body = f'<path d="{ic["d"]}" fill="currentColor"/>'
        else:
            body = (
                f'<path d="{ic["d"]}" fill="none" stroke="currentColor" '
                f'stroke-width="{stroke}" stroke-linecap="{cap}" stroke-linejoin="{join}"/>'
            )
        (out / f"{ic['name']}.svg").write_text(
            svg_document(body, "0 0 24 24", 24, 24), encoding="utf-8"
        )
        made.append({"name": ic["name"], "body": body})

    sprite = "\n".join(
        f'<symbol id="icon-{m["name"]}" viewBox="0 0 24 24">{m["body"]}</symbol>' for m in made
    )
    (out / "icons.svg").write_text(
        svg_document(f"<defs>{sprite}</defs>", "0 0 24 24"), encoding="utf-8"
    )
    return made


def type_scale(brand: dict) -> dict:
    s = brand.get("scale", {})
    base, ratio = s.get("base", 16), s.get("ratio", 1.25)
    names = [
        ("Display", 4, 0.95, -0.02, 700), ("H1", 3, 1.10, -0.015, 700),
        ("H2", 2, 1.15, -0.010, 600), ("H3", 1, 1.20, -0.005, 600),
        ("Body L", 0.5, 1.55, 0, 400), ("Body", 0, 1.60, 0, 400),
        ("Small", -1, 1.50, 0, 400), ("Caption", -1.5, 1.40, 0.02, 500),
    ]
    steps = []
    for name, step, lh, tr, w in names:
        px = round(base * (ratio ** step))
        steps.append({"name": name, "px": px, "line_height": lh,
                      "tracking_em": tr, "weight": w})
    return {"base": base, "ratio": ratio, "steps": steps}


def contrast_report(brand: dict) -> tuple[str, int]:
    pal = palette_of(brand)
    ink = next((c["hex"] for c in pal if c["role"] == "ink"), "#0D0D0D")
    surface = next((c["hex"] for c in pal if c["role"] == "surface"), "#FFFFFF")
    lines, fails = ["Contrast report (WCAG 2.1)", "=" * 52], 0
    for c in pal:
        # neutrals are tints, not text colors: reported, never counted as failures
        informational = c["role"] == "neutral"
        for bg, label in ((surface, "on surface"), (ink, "on ink")):
            if c["hex"] == bg:
                continue
            r = contrast(c["hex"], bg)
            v = contrast_verdict(r)
            if v == "FAIL" and not informational:
                fails += 1
            note = "  (tint — not for text)" if informational and v == "FAIL" else ""
            lines.append(f"{c['name']:<12} {label:<12} {r:>6}:1  {v}{note}")
    lines += ["", f"{fails} text/mark pairing(s) below 3:1."]
    if fails:
        lines.append("Fix by adjusting lightness — never by outlining the text.")
    return "\n".join(lines), fails


# --------------------------------------------------------------------------


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--project", default=None)
    a = ap.parse_args()

    project = Path(a.project).resolve() if a.project else find_project()
    brand = load_brand(project)
    gen = project / "identity" / "generated"
    ensure(gen, gen / "variations", gen / "icons")

    ways = colorways(brand)
    ink = ways["primary"]
    locks = build_lockups(project, brand)

    for name, (inner, w, h) in locks.items():
        doc = svg_document(f'<g fill="{ink}">{inner}</g>', f"0 0 {w:.2f} {h:.2f}",
                           round(w, 2), round(h, 2))
        fname = {"horizontal": "lockup-horizontal", "stacked": "lockup-stacked"}.get(name, name)
        (gen / f"{fname}.svg").write_text(doc, encoding="utf-8")

    n = 0
    for lock_name, (inner, w, h) in locks.items():
        for way, color in ways.items():
            body = svg_recolor(f'<g fill="{ink}">{inner}</g>', color)
            (gen / "variations" / f"{lock_name}-{way}.svg").write_text(
                svg_document(body, f"0 0 {w:.2f} {h:.2f}", round(w, 2), round(h, 2)),
                encoding="utf-8",
            )
            n += 1

    (gen / "clearspace.svg").write_text(
        build_clearspace(project, brand, locks["horizontal"], ink), encoding="utf-8")
    (gen / "grid.svg").write_text(build_grid_diagram(project, brand, ink), encoding="utf-8")

    icons = build_icons(project, brand, gen / "icons")
    (gen / "type-scale.json").write_text(json.dumps(type_scale(brand), indent=2), encoding="utf-8")
    (gen / "palette.json").write_text(json.dumps(palette_of(brand), indent=2), encoding="utf-8")
    report, fails = contrast_report(brand)
    (gen / "contrast-report.txt").write_text(report, encoding="utf-8")

    print(f"✓ lockups: {', '.join(locks)}")
    print(f"✓ {n} variation files")
    print(f"✓ {len(icons)} icons + sprite")
    print("✓ clearspace.svg, grid.svg, type-scale.json, palette.json")
    print()
    print(report)
    if fails:
        print("\n!! Fix the failing pairings before presenting (references/05-color.md).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
