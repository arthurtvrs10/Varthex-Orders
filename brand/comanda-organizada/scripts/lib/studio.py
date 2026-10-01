"""Stylized CSS/SVG studio objects for placing a mark in context.

These are renders, not photographs — say so when presenting. Every object takes
the real vector mark, so nothing is warped and everything updates the moment the
logo changes.

Shared language (references/08): one light source top-left, soft ambient shadow
plus a tight contact shadow, a specular sweep on the lit edge.

Objects kept: business card, phone, puck (board hero), plus three flat-lay
pieces the board layouts use — letterhead, email signature and app icon.
Merchandise (tee, mug, tote, signage, pen, packaging) was removed on purpose;
do not re-add it unless the user asks for that item by name.
"""
from __future__ import annotations

STUDIO_CSS = """
.stage{position:relative;display:flex;align-items:center;justify-content:center;
  background:radial-gradient(120% 100% at 30% 10%,#1E1E1E 0%,#121212 45%,#0B0B0B 100%);
  overflow:hidden;border-radius:16px;}
.stage::after{content:"";position:absolute;inset:0;pointer-events:none;
  background:radial-gradient(80% 60% at 50% 120%,rgba(0,0,0,.55),transparent 70%);}
.floor{position:absolute;left:0;right:0;bottom:0;height:38%;
  background:linear-gradient(180deg,rgba(255,255,255,.035),transparent);}
.obj{position:relative;filter:drop-shadow(0 40px 60px rgba(0,0,0,.6));}
.mark svg{display:block;width:100%;height:auto;}
.spec{position:absolute;inset:0;pointer-events:none;
  background:linear-gradient(135deg,rgba(255,255,255,.10),rgba(255,255,255,0) 42%,
             rgba(0,0,0,.22) 100%);}
.matte{background:linear-gradient(150deg,#1D1D1D 0%,#161616 45%,#0F0F0F 100%);}
.light{background:linear-gradient(150deg,#FCFCFC 0%,#F1F1F1 50%,#DFDFDF 100%);}
.stage.lt{background:radial-gradient(120% 100% at 30% 10%,#FFFFFF 0%,#EEF0F2 55%,#E2E4E8 100%);}
.stage.lt::after{background:radial-gradient(80% 60% at 50% 120%,rgba(0,0,0,.10),transparent 70%);}
.stage.lt .floor{background:linear-gradient(180deg,rgba(0,0,0,.025),transparent);}
.stage.lt .obj{filter:drop-shadow(0 30px 44px rgba(20,24,30,.22));}
.stage.hue{background:radial-gradient(120% 100% at 30% 10%,rgba(255,255,255,.14),transparent 60%),var(--hue);}
.stage.hue::after{background:radial-gradient(80% 60% at 50% 120%,rgba(0,0,0,.25),transparent 70%);}
.cap{position:absolute;bottom:12px;left:0;right:0;text-align:center;
  font-size:10px;letter-spacing:.06em;color:rgba(255,255,255,.40);}
"""


def _mark(svg: str, width: str, color: str | None = None, extra: str = "") -> str:
    tint = f"color:{color};" if color else ""
    return f'<div class="mark" style="width:{width};{tint}{extra}">{svg}</div>'


# --------------------------------------------------------------------------
# objects — each returns a block of HTML to drop inside a .stage
# --------------------------------------------------------------------------


def business_card(front_svg: str, back_html: str, mark_color: str, size: int = 460,
                  finish: str = "matte") -> str:
    """Two faces, back behind, front on top. finish: 'matte' (dark stock) or
    'light' (white stock — pass the ink cut of the mark and dark back text)."""
    w = size
    h = int(w * 0.63)
    return f"""
<div class="obj" style="width:{int(w*1.18)}px;height:{int(h*1.30)}px;position:relative">
  <div class="{finish}" style="position:absolute;left:0;top:0;width:{w}px;height:{h}px;
    border-radius:8px;transform:rotate(-6deg);box-shadow:0 30px 50px rgba(0,0,0,.6)">
    <div class="spec" style="border-radius:8px"></div>
    <div style="position:absolute;inset:0;padding:{int(h*0.13)}px;display:flex;
      flex-direction:column;justify-content:space-between">{back_html}</div>
  </div>
  <div class="{finish}" style="position:absolute;left:{int(w*0.17)}px;top:{int(h*0.30)}px;
    width:{w}px;height:{h}px;border-radius:8px;transform:rotate(4deg);
    box-shadow:0 36px 60px rgba(0,0,0,.68)">
    <div class="spec" style="border-radius:8px"></div>
    <div style="position:absolute;inset:0;display:flex;align-items:center;justify-content:center">
      {_mark(front_svg, f"{int(w*0.42)}px", mark_color, "opacity:.94;")}
    </div>
  </div>
</div>"""


def phone(svg: str, mark_color: str, screen_html: str = "", size: int = 300) -> str:
    w = size
    h = int(w * 2.03)
    return f"""
<div class="obj" style="width:{w}px;height:{h}px;border-radius:{int(w*0.13)}px;
  padding:{max(7,int(w*0.028))}px;background:linear-gradient(160deg,#3A3A3A,#151515 40%,#2A2A2A);
  box-shadow:0 40px 70px rgba(0,0,0,.7)">
  <div style="position:relative;width:100%;height:100%;border-radius:{int(w*0.108)}px;
    overflow:hidden;background:linear-gradient(180deg,#111,#0A0A0A)">
    <div style="position:absolute;left:50%;top:{int(w*0.035)}px;transform:translateX(-50%);
      width:{int(w*0.30)}px;height:{int(w*0.075)}px;background:#000;border-radius:99px"></div>
    <div style="position:absolute;inset:0;display:flex;flex-direction:column;
      align-items:center;justify-content:center;gap:{int(w*0.07)}px;padding:{int(w*0.12)}px">
      {_mark(svg, f"{int(w*0.46)}px", mark_color, "opacity:.95;")}
      {screen_html}
    </div>
  </div>
</div>"""


def puck(svg: str, mark_color: str, size: int = 240) -> str:
    """The dimensional object used on the board hero: the symbol on a disc."""
    return f"""
<div class="obj" style="width:{size}px;height:{size}px;border-radius:50%;
  background:radial-gradient(65% 65% at 32% 26%,#2A2A2A,#151515 55%,#080808 100%);
  box-shadow:0 30px 60px rgba(0,0,0,.72),inset 0 1px 0 rgba(255,255,255,.12);
  display:flex;align-items:center;justify-content:center">
  {_mark(svg, f"{int(size*0.58)}px", mark_color, "opacity:.95;")}
</div>"""


# --------------------------------------------------------------------------
# flat-lay stationery — real content, drawn in CSS
# --------------------------------------------------------------------------


def letterhead(logo_svg: str, lines: list[str], meta: str, footer: str,
               size: int = 300, tilt: float = -4, font: str = "__TEXT__") -> str:
    """A4 sheet (1 : 1.414) with the ink lockup top-left, a hairline, real body
    copy and a footer line. `lines` are real paragraphs (story, promise)."""
    w, h = size, int(size * 1.414)
    fs = max(5.5, w * 0.0215)
    body = "".join(f'<p style="margin:0 0 {fs*0.9:.1f}px;font-size:{fs:.1f}px;'
                   f'line-height:1.65;color:#4A4A4A">{x}</p>' for x in lines)
    return f"""
<div class="obj" style="width:{w}px;height:{h}px;transform:rotate({tilt}deg)">
  <div style="position:absolute;inset:0;background:#FFFFFF;border-radius:2px;
    padding:{int(w*0.09)}px {int(w*0.10)}px;font-family:'{font}',system-ui,sans-serif">
    <div style="display:flex;justify-content:space-between;align-items:flex-start;
      padding-bottom:{int(w*0.04)}px;border-bottom:1px solid #E4E4E4">
      <div style="width:{int(w*0.34)}px">{_mark(logo_svg, "100%")}</div>
      <div style="text-align:right;font-size:{fs*0.8:.1f}px;color:#8A8A8A;line-height:1.6">{meta}</div>
    </div>
    <div style="margin-top:{int(w*0.09)}px">{body}</div>
    <div style="position:absolute;left:{int(w*0.10)}px;right:{int(w*0.10)}px;bottom:{int(w*0.06)}px;
      padding-top:{int(w*0.025)}px;border-top:1px solid #EDEDED;font-size:{fs*0.75:.1f}px;
      color:#A2A2A2">{footer}</div>
    <div class="spec" style="opacity:.35"></div>
  </div>
</div>"""


def email_signature(logo_svg: str, name: str, role: str, contact_lines: list[str],
                    ink: str = "#0D0D0D", size: int = 460, font: str = "__TEXT__") -> str:
    """The signature as it lands in an inbox: a white message card."""
    w = size
    fs = max(9, w * 0.028)
    rows = "<br>".join(contact_lines)
    return f"""
<div class="obj" style="width:{w}px">
  <div style="background:#FFFFFF;border-radius:10px;padding:{int(w*0.06)}px {int(w*0.07)}px;
    font-family:'{font}',system-ui,sans-serif">
    <div style="height:6px;width:38%;background:#EDEDED;border-radius:3px;margin-bottom:9px"></div>
    <div style="height:6px;width:62%;background:#F2F2F2;border-radius:3px;margin-bottom:{int(w*0.06)}px"></div>
    <div style="display:flex;align-items:center;gap:{int(w*0.045)}px">
      <div style="width:{int(w*0.22)}px;padding-right:{int(w*0.045)}px;border-right:1px solid #E3E3E3">
        {_mark(logo_svg, "100%")}</div>
      <div>
        <div style="font-size:{fs*1.15:.1f}px;font-weight:700;color:{ink}">{name}</div>
        <div style="font-size:{fs*0.9:.1f}px;color:#6B6B6B;margin:2px 0 6px">{role}</div>
        <div style="font-size:{fs*0.9:.1f}px;color:#3A3A3A;line-height:1.6">{rows}</div>
      </div>
    </div>
  </div>
</div>"""


def app_icon(symbol_svg: str, bg: str, size: int = 150, label: str = "") -> str:
    """Rounded-square app icon (iOS radius ≈ 22.4%) with the symbol at ~50%."""
    lab = (f'<div style="text-align:center;font-size:{max(10, size*0.085):.0f}px;'
           f'margin-top:{int(size*0.10)}px;opacity:.6">{label}</div>') if label else ""
    return f"""
<div class="obj" style="width:{size}px">
  <div style="width:{size}px;height:{size}px;border-radius:{size*0.224:.0f}px;background:{bg};
    display:flex;align-items:center;justify-content:center;position:relative;overflow:hidden">
    <div class="spec" style="opacity:.6"></div>
    {_mark(symbol_svg, f"{int(size*0.50)}px")}
  </div>{lab}
</div>"""
