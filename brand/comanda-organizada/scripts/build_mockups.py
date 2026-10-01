#!/usr/bin/env python3
"""Phase 2 — collateral. One self-contained HTML page per item.

Run only after the identity is approved (references/08-collateral-mockups.md).
Four items: business card, phone/app, email signature, contract/letterhead.
Merchandise (tee, mug, tote, signage, pen, packaging) and the combined sheet
were removed on purpose.

    python3 scripts/build_mockups.py --all
    python3 scripts/build_mockups.py --only card,phone
    python3 scripts/render.py --mockups

Also writes two real, usable templates (not renders):
    out/assets/09-templates/email-signature.html
    out/assets/09-templates/letterhead.html
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from lib import studio  # noqa: E402
from lib.brandlib import (  # noqa: E402
    ensure, family_name, find_project, font_face_css, load_brand, read_svg,
    set_svg_size, theme,
)

ITEMS = ["card", "phone", "email", "contract"]

A4_W, A4_H = 794, 1123          # A4 at 96dpi


def page(project, brand, t, title, inner, css_extra="", width=1600, height=1200):
    disp = family_name(brand["typography"]["display"]["fontsource"])
    text = family_name(
        (brand["typography"].get("text") or brand["typography"]["display"])["fontsource"])
    return f"""<!doctype html><html lang="en"><head><meta charset="utf-8">
<title>{brand['brand']['name']} — {title}</title><style>
{font_face_css(project)}
*,*::before,*::after{{box-sizing:border-box}}
html,body{{margin:0;padding:0;background:{t['page']};
  font-family:'{text}',system-ui,sans-serif;color:{t['text']}}}
.display{{font-family:'{disp}',system-ui,sans-serif}}
.scene{{width:{width}px;height:{height}px;display:flex;align-items:center;
  justify-content:center;position:relative}}
.label{{position:absolute;left:34px;bottom:26px;font-size:11px;letter-spacing:.10em;
  text-transform:uppercase;color:rgba(255,255,255,.34)}}
{studio.STUDIO_CSS}
{css_extra}
</style></head><body>
<div class="scene stage" data-panel="{title.lower().replace(' ', '-')}">
  <div class="floor"></div>
  {inner}
  <div class="label">{brand['brand']['name']} — {title}</div>
</div></body></html>"""


# --------------------------------------------------------------------------
# item scenes
# --------------------------------------------------------------------------


def scene_card(brand, gen, mark, sym, t):
    c = brand["brand"].get("contact", {})
    tag = (brand["brand"].get("tagline") or "").replace("\n", " ")
    back = (
        f'<div class="display" style="color:#fff;font-size:15px">{c.get("person","")}'
        f'<div style="color:rgba(255,255,255,.45);font-size:11px;margin-top:5px">'
        f'{c.get("role","")}</div></div>'
        f'<div style="color:rgba(255,255,255,.55);font-size:11px;line-height:1.8">{tag}<br>'
        f'{c.get("email","")}<br>{c.get("phone","")} · {c.get("site","")}</div>'
    )
    return studio.business_card(mark, back, None, 520)


def scene_phone(brand, gen, mark, sym, t):
    tag = (brand["brand"].get("tagline") or "").split("\n")[0]
    screen = (
        f'<div class="display" style="color:rgba(255,255,255,.70);font-size:15px;'
        f'text-align:center;line-height:1.6">{tag}</div>'
        f'<div class="display" style="margin-top:6px;background:#fff;color:#111;'
        f'border-radius:99px;padding:12px 30px;font-size:13px;font-weight:500">Get Started</div>'
    )
    icon = (
        f'<div style="width:150px;height:150px;border-radius:34px;'
        f'background:linear-gradient(160deg,#1E1E1E,#0B0B0B);display:flex;'
        f'align-items:center;justify-content:center;'
        f'box-shadow:0 20px 40px rgba(0,0,0,.6)">'
        f'<div style="width:74px">{set_svg_size(sym, width="100%")}</div></div>'
        f'<div style="text-align:center;font-size:11px;color:rgba(255,255,255,.4);'
        f'margin-top:14px;letter-spacing:.04em">app icon 1024</div>'
    )
    return (f'<div style="display:flex;align-items:center;gap:90px">'
            f'{studio.phone(sym, None, screen, 320)}<div>{icon}</div></div>')


# --------------------------------------------------------------------------
# real templates
# --------------------------------------------------------------------------


def email_signature_html(project, brand, logo_src: str) -> str:
    c = brand["brand"].get("contact", {})
    disp = family_name(brand["typography"]["display"]["fontsource"])
    ink = next((x["hex"] for x in brand["colors"] if x.get("role") == "ink"), "#0D0D0D")
    return f"""<!doctype html><html><head><meta charset="utf-8">
<title>{brand['brand']['name']} — email signature</title></head>
<body style="margin:0;padding:40px;background:#fff">
<!-- Paste this block into Gmail / Outlook. Inline styles and a table layout are
     deliberate: email clients strip <style> and flexbox. -->
<table cellpadding="0" cellspacing="0" border="0" style="font-family:Helvetica,Arial,sans-serif">
  <tr>
    <td style="padding-right:18px;border-right:1px solid #E3E3E3;vertical-align:top">
      <img src="{logo_src}" alt="{brand['brand']['name']}" width="108" style="display:block;border:0">
    </td>
    <td style="padding-left:18px;vertical-align:top">
      <div style="font-size:15px;font-weight:700;color:{ink};letter-spacing:-.01em">
        {c.get('person','')}</div>
      <div style="font-size:12px;color:#6B6B6B;margin-top:3px">{c.get('role','')}</div>
      <div style="height:10px"></div>
      <div style="font-size:12px;color:#3A3A3A;line-height:1.7">
        <a href="mailto:{c.get('email','')}" style="color:{ink};text-decoration:none">
          {c.get('email','')}</a><br>
        {c.get('phone','')}<br>
        <a href="https://{c.get('site','')}" style="color:{ink};text-decoration:none">
          {c.get('site','')}</a>
      </div>
    </td>
  </tr>
</table>
<div style="margin-top:14px;font-family:Helvetica,Arial,sans-serif;font-size:10px;
  color:#9A9A9A;max-width:520px;line-height:1.6">
  {brand['brand'].get('legal','')}
</div>
</body></html>"""


def letterhead_html(project, brand, logo_svg: str, body_html: str | None = None) -> str:
    disp = family_name(brand["typography"]["display"]["fontsource"])
    text = family_name(
        (brand["typography"].get("text") or brand["typography"]["display"])["fontsource"])
    ink = next((x["hex"] for x in brand["colors"] if x.get("role") == "ink"), "#0D0D0D")
    c = brand["brand"].get("contact", {})
    body = body_html or f"""
      <h1 class="display">Service Agreement</h1>
      <p class="meta">Between {brand['brand']['name']} and [Client] · [Date]</p>
      <h2 class="display">1. Scope</h2>
      <p>[Describe the work, the deliverables, and what is explicitly out of scope.]</p>
      <h2 class="display">2. Fees and schedule</h2>
      <p>[Amount, currency, milestones, payment terms.]</p>
      <h2 class="display">3. Ownership</h2>
      <p>[When rights transfer, and what the supplier retains.]</p>
      <h2 class="display">4. Term and termination</h2>
      <p>[Notice period and what happens to work in progress.]</p>
      <div class="sign">
        <div><div class="line"></div><span>{brand['brand']['name']}</span></div>
        <div><div class="line"></div><span>Client</span></div>
      </div>"""
    return f"""<!doctype html><html><head><meta charset="utf-8">
<title>{brand['brand']['name']} — letterhead</title><style>
{font_face_css(project)}
@page{{size:A4;margin:0}}
*,*::before,*::after{{box-sizing:border-box}}
body{{margin:0;background:#fff;color:{ink};font-family:'{text}',Georgia,serif}}
.sheet{{width:{A4_W}px;min-height:{A4_H}px;padding:64px 72px 76px;position:relative;
  background:#fff;margin:0 auto}}
.display{{font-family:'{disp}',system-ui,sans-serif}}
header{{display:flex;justify-content:space-between;align-items:flex-start;
  padding-bottom:22px;border-bottom:1px solid #E4E4E4}}
header .logo{{width:132px}}
header .meta{{text-align:right;font-size:9.5px;color:#8A8A8A;line-height:1.7}}
h1{{font-size:25px;font-weight:700;letter-spacing:-.015em;margin:44px 0 6px}}
h2{{font-size:13px;font-weight:600;margin:26px 0 8px}}
p{{font-size:11.5px;line-height:1.75;color:#3C3C3C;margin:0 0 10px;max-width:62ch}}
p.meta{{color:#8A8A8A;font-size:10.5px;margin-bottom:24px}}
.sign{{display:flex;gap:52px;margin-top:64px}}
.sign>div{{flex:1}}
.sign .line{{height:1px;background:#CFCFCF;margin-bottom:8px}}
.sign span{{font-size:10px;color:#8A8A8A}}
footer{{position:absolute;left:72px;right:72px;bottom:36px;padding-top:14px;
  border-top:1px solid #EDEDED;display:flex;justify-content:space-between;
  font-size:9px;color:#A2A2A2}}
</style></head><body>
<div class="sheet">
  <header>
    <div class="logo">{set_svg_size(logo_svg, width="100%")}</div>
    <div class="meta">{c.get('address','')}<br>{c.get('site','')}<br>{c.get('email','')}</div>
  </header>
  {body}
  <footer><span>{brand['brand'].get('legal','')}</span><span>1 / 1</span></footer>
</div></body></html>"""


# --------------------------------------------------------------------------


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--project", default=None)
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--only", default=None, help="comma list: " + ",".join(ITEMS))
    a = ap.parse_args()

    project = Path(a.project).resolve() if a.project else find_project()
    brand = load_brand(project)
    t = theme(brand)
    gen = project / "identity" / "generated"
    pages = project / "pages" / "mockups"
    tpl = project / "out" / "assets" / "09-templates"
    ensure(pages, tpl)

    mark = read_svg(gen / "variations" / "logomark-white.svg")
    sym = read_svg(gen / "variations" / "symbol-white.svg")
    mark_ink = read_svg(gen / "variations" / "logomark-primary.svg")

    wanted = ITEMS if (a.all or not a.only) else [x.strip() for x in a.only.split(",")]
    unknown = [w for w in wanted if w not in ITEMS]
    if unknown:
        print(f"  ! ignorado (não existe mais): {', '.join(unknown)} — itens: {', '.join(ITEMS)}")
    wanted = [w for w in wanted if w in ITEMS] or ITEMS

    simple = {
        "card": ("Business Card", lambda: scene_card(brand, gen, mark, sym, t)),
        "phone": ("App", lambda: scene_phone(brand, gen, mark, sym, t)),
    }

    made = []
    for key in wanted:
        if key in simple:
            title, fn = simple[key]
            (pages / f"{key}.html").write_text(
                page(project, brand, t, title, fn()), encoding="utf-8")
            made.append(key)

    # --- real templates -----------------------------------------------------
    logo_src = ""
    if "email" in wanted:
        png = project / "out" / "assets" / "01-logo" / "primary" / "logo-primary-1024.png"
        if png.exists():
            import base64
            logo_src = "data:image/png;base64," + base64.b64encode(png.read_bytes()).decode()
        else:
            try:
                from render import Renderer
                tmp = project / "out" / "tmp-email-logo.png"
                with Renderer() as r:
                    r.svg_png(gen / "variations" / "logomark-primary.svg", tmp, 432)
                import base64
                logo_src = "data:image/png;base64," + base64.b64encode(tmp.read_bytes()).decode()
                tmp.unlink(missing_ok=True)
            except Exception as e:  # pragma: no cover
                print(f"  ! could not rasterize the signature logo ({e}); "
                      f"run export_assets.py first")
        sig = email_signature_html(project, brand, logo_src)
        (tpl / "email-signature.html").write_text(sig, encoding="utf-8")
        (pages / "email.html").write_text(
            page(project, brand, t, "Email Signature",
                 f'<div style="background:#fff;border-radius:14px;padding:20px 26px;'
                 f'box-shadow:0 40px 70px rgba(0,0,0,.6);width:760px;overflow:hidden">'
                 f'<iframe srcdoc="{sig.replace(chr(34), "&quot;")}" '
                 f'style="width:100%;height:250px;border:0"></iframe></div>',
                 height=900),
            encoding="utf-8")
        made.append("email")

    if "contract" in wanted:
        lh = letterhead_html(project, brand, mark_ink)
        (tpl / "letterhead.html").write_text(lh, encoding="utf-8")
        (pages / "contract.html").write_text(
            page(project, brand, t, "Contract",
                 f'<div style="transform:scale(.86);box-shadow:0 50px 80px rgba(0,0,0,.7)">'
                 f'<iframe srcdoc="{lh.replace(chr(34), "&quot;")}" '
                 f'style="width:{A4_W}px;height:{A4_H}px;border:0;display:block"></iframe></div>',
                 height=1180),
            encoding="utf-8")
        made.append("contract")

    print(f"✓ {len(made)} mockup page(s) → {pages}")
    print(f"  {', '.join(made)}")
    print(f"✓ usable templates → {tpl}")
    print("  next: python3 scripts/render.py --mockups")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
