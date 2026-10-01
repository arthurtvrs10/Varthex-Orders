#!/usr/bin/env python3
"""Render the PDF brand manual — A4 landscape, one idea per spread.

    python3 scripts/build_manual.py [--project PATH] [--version v1.0]

Writes pages/manual.html (self-contained) and out/assets/BRAND-MANUAL.pdf.
Section order follows references/09-deliverables-and-export.md.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from lib.brandlib import (  # noqa: E402
    contrast, contrast_verdict, ensure, family_name, find_project, font_face_css,
    load_brand, palette_of, read_svg, set_svg_size, svg_recolor,
)
from render import Renderer  # noqa: E402

PW, PH = 1122, 793      # A4 landscape @96dpi


def css(disp: str, text: str, ink: str) -> str:
    return f"""
@page{{size:A4 landscape;margin:0}}
*,*::before,*::after{{box-sizing:border-box}}
body{{margin:0;background:#fff;color:{ink};font-family:'{text}',system-ui,sans-serif}}
.page{{width:{PW}px;height:{PH}px;padding:64px 72px;position:relative;
  page-break-after:always;overflow:hidden;background:#fff}}
.page:last-child{{page-break-after:auto}}
.page.dark{{background:#0D0D0D;color:rgba(255,255,255,.72)}}
.display{{font-family:'{disp}',system-ui,sans-serif}}
h1{{font-family:'{disp}';font-size:44px;font-weight:700;letter-spacing:-.025em;
  margin:0 0 10px;color:inherit}}
h2{{font-family:'{disp}';font-size:12px;font-weight:600;letter-spacing:.14em;
  text-transform:uppercase;color:#9A9A9A;margin:0 0 26px;
  padding-bottom:12px;border-bottom:1px solid #E6E6E6}}
.dark h2{{color:rgba(255,255,255,.45);border-color:rgba(255,255,255,.12)}}
p{{font-size:13px;line-height:1.75;max-width:66ch;margin:0 0 12px;color:#3C3C3C}}
.dark p{{color:rgba(255,255,255,.66)}}
.lead{{font-size:19px;line-height:1.6;color:#1A1A1A;max-width:52ch}}
.dark .lead{{color:#fff}}
.folio{{position:absolute;left:72px;right:72px;bottom:34px;display:flex;
  justify-content:space-between;font-size:9.5px;color:#B0B0B0;letter-spacing:.06em}}
.dark .folio{{color:rgba(255,255,255,.30)}}
.grid2{{display:grid;grid-template-columns:1fr 1fr;gap:44px;align-items:start}}
.grid3{{display:grid;grid-template-columns:repeat(3,1fr);gap:26px}}
.grid4{{display:grid;grid-template-columns:repeat(4,1fr);gap:22px}}
.card{{border:1px solid #E8E8E8;border-radius:10px;padding:22px;text-align:center}}
.dark .card{{border-color:rgba(255,255,255,.12)}}
.card .cap{{font-size:10px;color:#9A9A9A;margin-top:14px;letter-spacing:.04em}}
table{{width:100%;border-collapse:collapse;font-size:11.5px}}
th,td{{text-align:left;padding:9px 10px;border-bottom:1px solid #EDEDED}}
th{{font-size:9.5px;letter-spacing:.10em;text-transform:uppercase;color:#9A9A9A}}
.swatch{{height:88px;border-radius:8px;border:1px solid rgba(0,0,0,.08)}}
.spec{{font-size:10px;line-height:1.7;color:#6E6E6E;margin-top:10px}}
.no{{position:relative}}
.no::after{{content:"";position:absolute;inset:14px;
  background:linear-gradient(to top right,transparent 48.4%,#D9403E 48.4%,#D9403E 51.6%,
  transparent 51.6%)}}
svg{{display:block;max-width:100%;height:auto}}
"""


def folio(brand, section, n, total):
    return (f'<div class="folio"><span>{brand["brand"]["name"]} — {section}</span>'
            f'<span>{n} / {total}</span></div>')


def build(project: Path, brand: dict, version: str) -> Path:
    gen = project / "identity" / "generated"
    disp = family_name(brand["typography"]["display"]["fontsource"])
    text = family_name(
        (brand["typography"].get("text") or brand["typography"]["display"])["fontsource"])
    pal = palette_of(brand)
    ink = next((c["hex"] for c in pal if c["role"] == "ink"), "#0D0D0D")
    surface = next((c["hex"] for c in pal if c["role"] == "surface"), "#FFFFFF")
    today = dt.date.today().isoformat()

    v = lambda n: read_svg(gen / "variations" / f"{n}.svg")  # noqa: E731
    pages: list[tuple[str, str]] = []

    # 1 cover ---------------------------------------------------------------
    pages.append(("Cover", f"""
      <div class="page dark" style="display:flex;flex-direction:column;
        justify-content:space-between">
        <div style="width:280px">{set_svg_size(v('stacked-white'), width='100%')}</div>
        <div>
          <h1 style="color:#fff">Brand Guidelines</h1>
          <p class="lead">{(brand['brand'].get('tagline') or '').replace(chr(10), ' ')}</p>
          <p style="margin-top:30px;font-size:11px;letter-spacing:.10em;
            color:rgba(255,255,255,.42)">{version} · {today}</p>
        </div>
      </div>"""))

    # 2 the idea ------------------------------------------------------------
    c = brand.get("concept", {})
    pages.append(("The idea", f"""
      <div class="page"><h2>The idea</h2>
        <div class="grid2">
          <div>
            <h1>{c.get('name', 'The mark')}</h1>
            <p class="lead">{c.get('sentence', '')}</p>
            <p style="margin-top:22px">Relationship to the name:
              <strong>{c.get('name_relation', '')}</strong>.
              {('Operation: ' + c['operation']) if c.get('operation') else ''}</p>
            <p>{brand['brand'].get('story', '')}</p>
          </div>
          <div class="card" style="padding:44px">
            {set_svg_size(v('symbol-primary'), width='190')}
            <div class="cap">the symbol</div>
          </div>
        </div>
      </div>"""))

    # 3 logo + construction --------------------------------------------------
    m = brand["logo"].get("metrics", {})
    rows = "".join(f"<tr><td>{k}</td><td>{val}</td></tr>" for k, val in m.items())
    pages.append(("The logo", f"""
      <div class="page"><h2>The logo · construction</h2>
        <div class="grid2">
          <div>{set_svg_size(read_svg(gen / 'grid.svg'), width='300')}
            <p class="spec" style="margin-top:16px">Grid:
              {brand['logo'].get('grid', '')} · U = {brand['logo'].get('unit', 100)}</p>
          </div>
          <div>
            <div style="width:330px;margin-bottom:34px">
              {set_svg_size(v('horizontal-primary'), width='100%')}</div>
            <table><tr><th>Element</th><th>Value</th></tr>{rows}</table>
          </div>
        </div>
      </div>"""))

    # 4 clear space + minimum sizes -----------------------------------------
    ms = brand["logo"].get("min_size", {})
    cs_unit = (brand["logo"].get("clearspace") or {}).get("unit_label", "a")
    pages.append(("Clear space", f"""
      <div class="page"><h2>Clear space and minimum size</h2>
        <div class="grid2">
          <div>{set_svg_size(read_svg(gen / 'clearspace.svg'), width='420')}
            <p class="spec">Exclusion zone = 1 × {cs_unit} on all four sides. Nothing
              enters it: no text, no image edge, no page trim, no other logo.</p>
          </div>
          <div>
            <table>
              <tr><th>Context</th><th>Minimum</th></tr>
              <tr><td>Symbol, digital</td><td>{ms.get('digital_symbol_px', 16)} px</td></tr>
              <tr><td>Lockup, digital</td><td>{ms.get('digital_lockup_px', 90)} px wide</td></tr>
              <tr><td>Symbol, print</td><td>{ms.get('print_symbol_mm', 8)} mm</td></tr>
              <tr><td>Lockup, print</td><td>{ms.get('print_lockup_mm', 25)} mm wide</td></tr>
            </table>
            <p style="margin-top:22px"><strong>Below the minimum, switch lockup —
              do not shrink.</strong> The stacked lockup holds at smaller widths than
              the horizontal; below that, use the symbol alone.</p>
          </div>
        </div>
      </div>"""))

    # 5 variations ------------------------------------------------------------
    cards = ""
    for cap, way, bg in (("Primary", "primary", surface), ("Reverse", "white", ink),
                         ("Monochrome", "black", surface), ("Tint", "mono", surface)):
        art = v(f"stacked-{way}")
        if not art:
            continue
        cards += (f'<div class="card" style="background:{bg};border-color:'
                  f'{"rgba(255,255,255,.14)" if bg == ink else "#E8E8E8"}">'
                  f'<div style="width:120px;margin:0 auto">{set_svg_size(art, width="100%")}</div>'
                  f'<div class="cap" style="color:{"rgba(255,255,255,.5)" if bg == ink else "#9A9A9A"}">'
                  f'{cap}</div></div>')
    pages.append(("Variations", f"""
      <div class="page"><h2>Variations</h2>
        <div class="grid4">{cards}</div>
        <p style="margin-top:34px">Use <strong>Primary</strong> on light backgrounds and
          <strong>Reverse</strong> on dark ones or over imagery. <strong>Monochrome</strong>
          is for single-colour processes — stamping, engraving, embroidery, fax-grade
          print. <strong>Tint</strong> is for secondary placements only, never for the
          primary signature.</p>
      </div>"""))

    # 6 misuse ---------------------------------------------------------------
    base = v("stacked-primary")
    wrongs = [
        ("Do not stretch", "transform:scaleX(1.45)", base),
        ("Do not rotate", "transform:rotate(-14deg)", base),
        ("Do not recolour", "", svg_recolor(base, "#C8452F")),
        ("Do not add effects", "filter:drop-shadow(0 6px 6px rgba(0,0,0,.55))", base),
        ("Do not distort", "transform:scaleY(1.5)", base),
        ("Do not re-space", "transform:scaleX(.7);letter-spacing:.4em", base),
    ]
    cells = "".join(
        f'<div class="card no"><div style="width:110px;margin:0 auto;{st}">'
        f'{set_svg_size(art, width="100%")}</div>'
        f'<div class="cap">{cap}</div></div>' for cap, st, art in wrongs)
    pages.append(("Misuse", f"""
      <div class="page"><h2>Misuse</h2><div class="grid3">{cells}</div>
        <p style="margin-top:28px">Also: never place the mark on a busy photograph or a
          low-contrast field, never box it when the layout does not require a box, and
          never re-type the wordmark in another typeface.</p>
      </div>"""))

    # 7 color ----------------------------------------------------------------
    sw = ""
    for col in pal:
        sw += (f'<div><div class="swatch" style="background:{col["hex"]}"></div>'
               f'<div class="spec"><strong>{col["name"]}</strong><br>{col["hex"]}<br>'
               f'RGB {" ".join(str(x) for x in col["rgb"])}<br>'
               f'CMYK {" ".join(str(x) for x in col["cmyk"])}<br>'
               f'PMS {col["pms"] or "TBC"}</div></div>')
    pairs = "".join(
        f'<tr><td>{col["name"]} on {"ink" if bg == ink else "surface"}</td>'
        f'<td>{contrast(col["hex"], bg)}:1</td>'
        f'<td>{contrast_verdict(contrast(col["hex"], bg))}</td></tr>'
        for col in pal for bg in (surface, ink) if col["hex"] != bg)
    pages.append(("Colour", f"""
      <div class="page"><h2>Colour</h2>
        <div class="grid{min(len(pal), 4) if len(pal) < 5 else 4}"
          style="grid-template-columns:repeat({len(pal)},1fr)">{sw}</div>
        <p class="spec" style="margin-top:22px">CMYK values are conversions and PMS values
          are nearest matches. Confirm both on a physical proof before any print run.</p>
        <div style="margin-top:20px;max-height:230px;overflow:hidden">
          <table><tr><th>Pairing</th><th>Ratio</th><th>WCAG</th></tr>{pairs}</table>
        </div>
      </div>"""))

    # 8 typography -----------------------------------------------------------
    scale = json.loads((gen / "type-scale.json").read_text())
    steps = "".join(
        f'<tr><td>{s["name"]}</td><td>{s["px"]} px</td><td>{s["line_height"]}</td>'
        f'<td>{s["tracking_em"] * 100:+.1f}%</td><td>{s["weight"]}</td></tr>'
        for s in scale["steps"])
    fam_cards = ""
    for role, spec in brand["typography"].items():
        if not isinstance(spec, dict) or "fontsource" not in spec:
            continue
        fam = family_name(spec["fontsource"])
        sub = (f'<br><span style="color:#C0392B">stands in for {spec["substituted_for"]}</span>'
               if spec.get("substituted_for") else "")
        fam_cards += (
            f'<div><div class="display" style="font-family:\'{fam}\';font-size:56px;'
            f'font-weight:700;line-height:1">Aa</div>'
            f'<div class="spec"><strong>{fam}</strong> — {spec.get("class", "")}<br>'
            f'{spec.get("role", role)}<br>weights {", ".join(str(w) for w in spec.get("weights", []))}'
            f'{sub}</div>'
            f'<div style="font-family:\'{fam}\';font-size:11px;letter-spacing:.05em;'
            f'line-height:1.9;margin-top:12px;color:#555">'
            f'ABCDEFGHIJKLMNOPQRSTUVWXYZ<br>abcdefghijklmnopqrstuvwxyz<br>0123456789</div></div>')
    pages.append(("Typography", f"""
      <div class="page"><h2>Typography</h2>
        <div class="grid2">
          <div class="grid2" style="gap:26px">{fam_cards}</div>
          <div><table><tr><th>Step</th><th>Size</th><th>LH</th><th>Track</th><th>Wt</th></tr>
            {steps}</table>
            <p class="spec" style="margin-top:14px">Modular scale, base
              {scale['base']}px, ratio {scale['ratio']}. Measure: 45–75 characters.</p>
          </div>
        </div>
      </div>"""))

    # 9 iconography ----------------------------------------------------------
    icons = [f for f in sorted((gen / "icons").glob("*.svg")) if f.name != "icons.svg"][:12]
    ig = "".join(
        f'<div class="card" style="padding:16px;color:{ink}">'
        f'{set_svg_size(read_svg(f), width="28", height="28")}'
        f'<div class="cap">{f.stem}</div></div>' for f in icons)
    pages.append(("Iconography", f"""
      <div class="page"><h2>Iconography</h2>
        <div class="grid2">
          <div style="display:grid;grid-template-columns:repeat(4,1fr);gap:14px">{ig}</div>
          <div><p>Icons are drawn on the mark's DNA: the same grid, the same stroke
            weight, the same terminals and corner radius. A borrowed icon set overwrites
            the brand's character on every screen.</p>
            <p>24 × 24 grid, 20px live area. Outline style throughout — never mix filled
            and outline in one set. Every icon uses <code>currentColor</code>, so one
            file works in every colourway and in dark mode.</p>
            <p>Test at 16px before shipping. An icon that turns to mud is redrawn, not
            shrunk.</p></div>
        </div>
      </div>"""))

    # 10 voice ---------------------------------------------------------------
    voice = brand["brand"].get("voice", {})
    say = "".join(f"<li>{x}</li>" for x in voice.get("we_say", []))
    dont = "".join(f"<li>{x}</li>" for x in voice.get("we_dont_say", []))
    if say or dont or voice.get("adjectives"):
        pages.append(("Voice", f"""
          <div class="page"><h2>Voice</h2>
            <h1 style="font-size:30px">{' · '.join(voice.get('adjectives', []))}</h1>
            <div class="grid2" style="margin-top:34px">
              <div><h2 style="border:none;padding:0;margin-bottom:12px">We say</h2>
                <ul style="font-size:13px;line-height:1.9;color:#3C3C3C">{say}</ul></div>
              <div><h2 style="border:none;padding:0;margin-bottom:12px">We don't say</h2>
                <ul style="font-size:13px;line-height:1.9;color:#9A9A9A">{dont}</ul></div>
            </div>
          </div>"""))

    # 11 applications --------------------------------------------------------
    shots = sorted((project / "out" / "mockups").glob("*@1x.png"))[:6]
    if shots:
        import base64
        tiles = "".join(
            f'<div><img src="data:image/png;base64,'
            f'{base64.b64encode(p.read_bytes()).decode()}" style="width:100%;'
            f'border-radius:8px;display:block"><div class="cap">{p.stem.split("@")[0]}</div></div>'
            for p in shots)
        pages.append(("Applications", f"""
          <div class="page dark"><h2>Applications</h2>
            <div class="grid3" style="gap:18px">{tiles}</div>
            <p class="spec" style="margin-top:20px;color:rgba(255,255,255,.42)">
              Stylised studio renders, not photographs.</p>
          </div>"""))

    # 12 assets --------------------------------------------------------------
    pages.append(("Assets", f"""
      <div class="page"><h2>Assets and approvals</h2>
        <p class="lead">Everything is in the delivered asset folder, organised by use.</p>
        <table style="margin-top:26px">
          <tr><th>Need</th><th>Folder</th></tr>
          <tr><td>Logo on light backgrounds</td><td>01-logo/primary/</td></tr>
          <tr><td>Logo on dark backgrounds or photos</td><td>01-logo/white/</td></tr>
          <tr><td>Single-colour processes</td><td>01-logo/black/</td></tr>
          <tr><td>Avatar, favicon, app icon</td><td>02-symbol/</td></tr>
          <tr><td>Fonts and licences</td><td>03-typography/fonts/</td></tr>
          <tr><td>Colour tokens for developers</td><td>04-color/tokens.css · tokens.json</td></tr>
          <tr><td>Icon set</td><td>05-icons/</td></tr>
          <tr><td>Email signature, letterhead</td><td>09-templates/</td></tr>
        </table>
        <p style="margin-top:26px">Use the SVG wherever the tool accepts one. Any
          application not covered here is approved by the brand owner before it ships.</p>
        <p class="spec">{version} · {today}</p>
      </div>"""))

    total = len(pages)
    body = ""
    for i, (name, html) in enumerate(pages):
        html = html.rstrip()
        # every page string ends with the .page </div>; slot the folio in front of it
        body += html[: -len("</div>")] + folio(brand, name, i + 1, total) + "</div>"

    out = project / "pages" / "manual.html"
    ensure(out.parent)
    out.write_text(
        f'<!doctype html><html lang="en"><head><meta charset="utf-8">'
        f'<title>{brand["brand"]["name"]} — Brand Guidelines</title><style>'
        f'{font_face_css(project)}\n{css(disp, text, ink)}</style></head>'
        f'<body>{body}</body></html>', encoding="utf-8")
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--project", default=None)
    ap.add_argument("--version", default="v1.0")
    a = ap.parse_args()

    project = Path(a.project).resolve() if a.project else find_project()
    brand = load_brand(project)
    page = build(project, brand, a.version)
    dest = project / "out" / "assets" / "BRAND-MANUAL.pdf"
    ensure(dest.parent)
    with Renderer() as r:
        r.pdf(page, dest, fmt="A4", landscape=True)
    print(f"✓ {page}")
    print(f"✓ {dest}")
    print("  Guidelines that are not versioned are not followed — bump --version on edits.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
