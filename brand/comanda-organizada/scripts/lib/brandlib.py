"""Shared helpers for the brand-identity-designer skill.

Everything reads from one project folder that contains `brand.json`.
No network access is required at build time: fonts are fetched once by
`fetch_fonts.py` and then inlined as base64.
"""
from __future__ import annotations

import base64
import json
import math
import os
import re
from pathlib import Path

# --------------------------------------------------------------------------
# project
# --------------------------------------------------------------------------


def find_project(start: str | os.PathLike | None = None) -> Path:
    """Walk up from `start` (default: cwd) looking for brand.json."""
    p = Path(start or os.getcwd()).resolve()
    for cand in [p, *p.parents]:
        if (cand / "brand.json").exists():
            return cand
    raise SystemExit(
        "brand.json not found. Run from inside the project folder, or pass --project."
    )


def load_brand(project: Path) -> dict:
    data = json.loads((project / "brand.json").read_text(encoding="utf-8"))
    data.setdefault("_project", str(project))
    return data


def ensure(*dirs: Path) -> None:
    for d in dirs:
        d.mkdir(parents=True, exist_ok=True)


def slug(text: str) -> str:
    return re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-") or "untitled"


# --------------------------------------------------------------------------
# files / data uris
# --------------------------------------------------------------------------


def b64(path: Path) -> str:
    return base64.b64encode(path.read_bytes()).decode("ascii")


def data_uri(path: Path, mime: str) -> str:
    return f"data:{mime};base64,{b64(path)}"


def read_svg(path: Path) -> str:
    """Return an SVG's markup, stripped of XML prolog and comments."""
    if not path.exists():
        return ""
    s = path.read_text(encoding="utf-8")
    s = re.sub(r"<\?xml.*?\?>", "", s, flags=re.S)
    s = re.sub(r"<!--.*?-->", "", s, flags=re.S)
    return s.strip()


def svg_recolor(markup: str, color: str) -> str:
    """Force every fill/stroke in an SVG fragment to `color`.

    Used to derive the mono / white / reverse variations from one source file.
    Elements explicitly marked fill="none" keep their transparency.
    """
    def sub_attr(m):
        attr, val = m.group(1), m.group(2)
        if val.strip().lower() in ("none", "transparent"):
            return m.group(0)
        return f'{attr}="{color}"'

    out = re.sub(r'\b(fill|stroke)="([^"]*)"', sub_attr, markup)
    out = re.sub(r'\b(fill|stroke):\s*(?!none)[^;"\']+', lambda m: f"{m.group(1)}:{color}", out)
    return out


def set_svg_size(markup: str, width=None, height=None) -> str:
    markup = re.sub(r'\s(width|height)="[^"]*"', "", markup, count=2)
    attrs = ""
    if width:
        attrs += f' width="{width}"'
    if height:
        attrs += f' height="{height}"'
    return markup.replace("<svg", f"<svg{attrs}", 1)


def svg_document(inner: str, view_box: str, width=None, height=None) -> str:
    w = f' width="{width}"' if width else ""
    h = f' height="{height}"' if height else ""
    return (
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{view_box}"{w}{h}>\n'
        f"{inner}\n</svg>\n"
    )


# --------------------------------------------------------------------------
# color
# --------------------------------------------------------------------------


def hex_to_rgb(h: str) -> tuple[int, int, int]:
    h = h.strip().lstrip("#")
    if len(h) == 3:
        h = "".join(c * 2 for c in h)
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))  # type: ignore[return-value]


def rgb_to_hex(rgb) -> str:
    return "#" + "".join(f"{max(0, min(255, int(round(c)))):02X}" for c in rgb)


def _lin(c: float) -> float:
    c /= 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def luminance(h: str) -> float:
    r, g, b = hex_to_rgb(h)
    return 0.2126 * _lin(r) + 0.7152 * _lin(g) + 0.0722 * _lin(b)


def contrast(a: str, b: str) -> float:
    la, lb = luminance(a), luminance(b)
    hi, lo = max(la, lb), min(la, lb)
    return round((hi + 0.05) / (lo + 0.05), 2)


def contrast_verdict(ratio: float) -> str:
    if ratio >= 7:
        return "AAA"
    if ratio >= 4.5:
        return "AA"
    if ratio >= 3:
        return "AA-large"
    return "FAIL"


def hex_to_cmyk(h: str) -> tuple[int, int, int, int]:
    r, g, b = [c / 255 for c in hex_to_rgb(h)]
    k = 1 - max(r, g, b)
    if k >= 1:
        return (0, 0, 0, 100)
    c = (1 - r - k) / (1 - k)
    m = (1 - g - k) / (1 - k)
    y = (1 - b - k) / (1 - k)
    return tuple(int(round(v * 100)) for v in (c, m, y, k))  # type: ignore[return-value]


def _srgb_to_xyz(h: str):
    r, g, b = [_lin(c) for c in hex_to_rgb(h)]
    x = r * 0.4124 + g * 0.3576 + b * 0.1805
    y = r * 0.2126 + g * 0.7152 + b * 0.0722
    z = r * 0.0193 + g * 0.1192 + b * 0.9505
    return x, y, z


def lightness(h: str) -> float:
    """CIE L* (0-100)."""
    y = _srgb_to_xyz(h)[1]
    return 116 * (y ** (1 / 3)) - 16 if y > 0.008856 else 903.3 * y


def mix(a: str, b: str, t: float) -> str:
    ra, ga, ba = hex_to_rgb(a)
    rb, gb, bb = hex_to_rgb(b)
    return rgb_to_hex((ra + (rb - ra) * t, ga + (gb - ga) * t, ba + (bb - ba) * t))


def neutral_ramp(ink: str, surface: str, steps: int = 5) -> list[str]:
    """Perceptually-even ramp between ink and surface (excludes both ends)."""
    lo, hi = lightness(ink), lightness(surface)
    out = []
    for i in range(1, steps + 1):
        target = lo + (hi - lo) * i / (steps + 1)
        # binary search the mix ratio that lands on the target L*
        a, b = 0.0, 1.0
        for _ in range(24):
            m = (a + b) / 2
            if lightness(mix(ink, surface, m)) < target:
                a = m
            else:
                b = m
        out.append(mix(ink, surface, (a + b) / 2))
    return out


def palette_of(brand: dict) -> list[dict]:
    """Normalize brand.json colors into a list of full specs."""
    out = []
    for c in brand.get("colors", []):
        h = c["hex"].upper()
        out.append(
            {
                "name": c.get("name", h),
                "hex": h,
                "role": c.get("role", ""),
                "rgb": hex_to_rgb(h),
                "cmyk": hex_to_cmyk(h),
                "pms": c.get("pms", ""),
                "lstar": round(lightness(h), 1),
            }
        )
    return out


# --------------------------------------------------------------------------
# fonts
# --------------------------------------------------------------------------

FONT_MIME = {".woff2": "font/woff2", ".woff": "font/woff", ".ttf": "font/ttf"}


def font_files(project: Path) -> list[Path]:
    d = project / "fonts"
    if not d.exists():
        return []
    return sorted([p for p in d.rglob("*") if p.suffix in FONT_MIME])


def font_face_css(project: Path) -> str:
    """@font-face rules with every fetched face inlined as base64.

    Filenames follow fontsource: `<family>-latin-<weight>-<style>.woff2`
    """
    rules = []
    for p in font_files(project):
        if p.suffix != ".woff2":
            continue
        m = re.match(r"(?P<fam>.+?)-[a-z-]*?(?P<w>\d{3})-(?P<style>normal|italic)$", p.stem)
        if not m:
            continue
        family = m.group("fam").replace("-", " ").title()
        rules.append(
            "@font-face{font-family:'%s';font-style:%s;font-weight:%s;font-display:block;"
            "src:url(%s) format('woff2');}"
            % (family, m.group("style"), m.group("w"), data_uri(p, "font/woff2"))
        )
    return "\n".join(rules)


def family_name(fontsource_id: str) -> str:
    return fontsource_id.replace("-", " ").title()


def find_font_file(project: Path, fontsource_id: str, weight: int = 700,
                   prefer=(".woff", ".ttf", ".woff2")) -> Path | None:
    """Locate a face for outlining. `.woff` first — fontTools reads it
    without the brotli dependency that `.woff2` needs."""
    for ext in prefer:
        for p in sorted((project / "fonts").glob(f"{fontsource_id}-*-{weight}-normal{ext}")):
            return p
    for ext in prefer:
        cands = sorted((project / "fonts").glob(f"{fontsource_id}-*normal{ext}"))
        if cands:
            return cands[0]
    return None


# --------------------------------------------------------------------------
# html shell
# --------------------------------------------------------------------------

BOARD_MODES = {
    # archetype A — dark studio (references 01, 02)
    "dark": {
        "page": "#0A0A0A", "panel": "#131313", "panel2": "#1A1A1A",
        "line": "rgba(255,255,255,.10)", "text": "rgba(255,255,255,.62)",
        "head": "#FFFFFF", "muted": "rgba(255,255,255,.42)", "mark": "#FFFFFF",
    },
    # archetype D — light gallery (reference 06)
    "light": {
        "page": "#F1F2F4", "panel": "#FFFFFF", "panel2": "#F6F7F8",
        "line": "rgba(0,0,0,.10)", "text": "rgba(0,0,0,.62)",
        "head": "#0E1012", "muted": "rgba(0,0,0,.42)", "mark": "#0E1012",
    },
    # archetype B — colour flood (references 03, 04); panel is filled below
    "flood": {
        "page": "#0A0A0A", "panel": None, "panel2": None,
        "line": "rgba(255,255,255,.22)", "text": "rgba(255,255,255,.78)",
        "head": "#FFFFFF", "muted": "rgba(255,255,255,.55)", "mark": "#FFFFFF",
    },
}


def theme(brand: dict) -> dict:
    """Board colour tokens for the archetype named in board.mode.

    See references/07-brand-board-spec.md. `flood` paints the panels in the
    brand hue and needs a `brand` or `accent` colour; without one it falls back
    to `dark` rather than guessing.
    """
    board = brand.get("board", {})
    mode = board.get("mode", "dark")
    if mode not in BOARD_MODES:
        print(f"  ! unknown board.mode {mode!r} — using 'dark'")
        mode = "dark"

    if mode == "flood":
        roles = {c.get("role"): c["hex"] for c in brand.get("colors", [])}
        hue = roles.get("brand") or roles.get("accent")
        if not hue:
            print("  ! board.mode 'flood' needs a colour with role 'brand' or "
                  "'accent' — using 'dark'")
            mode = "dark"

    t = dict(BOARD_MODES[mode])
    if mode == "flood":
        t["panel"] = hue
        t["panel2"] = mix(hue, "#000000", 0.22)
        t["page"] = roles.get("ink", "#0A0A0A")

    t["mode"] = mode
    t.update(board.get("theme_overrides", {}))
    return t


def html_shell(title: str, css: str, body: str, project: Path,
               width: int | None = None) -> str:
    """Self-contained HTML page: fonts inlined, no external requests."""
    size = f"body{{width:{width}px;}}" if width else ""
    return f"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>{title}</title>
<style>
{font_face_css(project)}
*,*::before,*::after{{box-sizing:border-box;}}
html,body{{margin:0;padding:0;}}
{size}
{css}
</style></head><body>
{body}
</body></html>
"""
