"""Convert text into outlined SVG paths with fontTools.

Wordmarks ship as paths, never as live <text>: the downstream printer,
the client's slide deck, and the browser on a machine without the font all
need the geometry, not a font reference.

    from lib.svgtext import outline
    r = outline("fonts/poppins-latin-700-normal.woff", "zcio",
                size=100, tracking=-0.02)
    r["d"], r["width"], r["height"], r["baseline"]

Coordinates are in a y-down SVG space with the baseline at y=`baseline`.
"""
from __future__ import annotations

from pathlib import Path

from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont

_cache: dict[str, TTFont] = {}


def _font(path: str | Path) -> TTFont:
    key = str(path)
    if key not in _cache:
        _cache[key] = TTFont(str(path), lazy=True)
    return _cache[key]


def metrics(path: str | Path) -> dict:
    f = _font(path)
    upm = f["head"].unitsPerEm
    os2 = f["OS/2"] if "OS/2" in f else None
    return {
        "upm": upm,
        "ascender": f["hhea"].ascender,
        "descender": f["hhea"].descender,
        "cap_height": getattr(os2, "sCapHeight", None) or int(upm * 0.72),
        "x_height": getattr(os2, "sxHeight", None) or int(upm * 0.52),
    }


def outline(font_path: str | Path, text: str, size: float = 100.0,
            tracking: float = 0.0, kerning: bool = True) -> dict:
    """Return {"d", "width", "height", "baseline", "cap_height"}.

    `tracking` is in em units (-0.02 == -2%), the way a designer specifies it.
    """
    f = _font(font_path)
    upm = f["head"].unitsPerEm
    scale = size / upm
    glyphset = f.getGlyphSet()
    cmap = f.getBestCmap()
    hmtx = f["hmtx"]

    kern_pairs = {}
    if kerning and "kern" in f:
        for st in f["kern"].kernTables:
            kern_pairs.update(st.kernTable)

    names = []
    for ch in text:
        gname = cmap.get(ord(ch))
        if gname is None:
            gname = ".notdef" if ch != " " else None
        names.append(gname)

    parts = []
    x = 0.0
    track_units = tracking * upm
    for i, gname in enumerate(names):
        if gname is None:                       # space
            x += hmtx.metrics.get("space", (upm * 0.28, 0))[0] + track_units
            continue
        pen = SVGPathPen(glyphset, ntos=lambda v: f"{v:.2f}")
        tpen = TransformPen(pen, (scale, 0, 0, -scale, x * scale, 0))
        glyphset[gname].draw(tpen)
        d = pen.getCommands()
        if d:
            parts.append(d)
        adv = hmtx.metrics[gname][0]
        if i + 1 < len(names) and names[i + 1]:
            adv += kern_pairs.get((gname, names[i + 1]), 0)
        x += adv + track_units

    m = metrics(font_path)
    return {
        "d": " ".join(parts),
        "width": round((x - track_units) * scale, 3),
        "height": round((m["ascender"] - m["descender"]) * scale, 3),
        "baseline": 0.0,
        "cap_height": round(m["cap_height"] * scale, 3),
        "x_height": round(m["x_height"] * scale, 3),
    }


def wordmark_svg(font_path: str | Path, text: str, size: float = 100.0,
                 tracking: float = 0.0, color: str = "#000000",
                 padding: float = 0.0) -> str:
    """A complete, tight-cropped SVG document for a single line of type."""
    r = outline(font_path, text, size=size, tracking=tracking)
    w = r["width"] + padding * 2
    h = r["cap_height"] + padding * 2
    body = (
        f'<g transform="translate({padding},{r["cap_height"] + padding})">'
        f'<path d="{r["d"]}" fill="{color}"/></g>'
    )
    return (
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w:.2f} {h:.2f}" '
        f'width="{w:.2f}" height="{h:.2f}">\n{body}\n</svg>\n'
    )


if __name__ == "__main__":
    import sys

    if len(sys.argv) < 3:
        print("usage: svgtext.py <font-file> <text> [size] [tracking] > out.svg")
        raise SystemExit(1)
    size = float(sys.argv[3]) if len(sys.argv) > 3 else 100.0
    tr = float(sys.argv[4]) if len(sys.argv) > 4 else 0.0
    print(wordmark_svg(sys.argv[1], sys.argv[2], size, tr))
