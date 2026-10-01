#!/usr/bin/env python3
"""Compose the brand board — one self-contained HTML page.

Six layouts, one per reference in assets/ (see references/07-brand-board-spec.md
and scripts/lib/layouts.py). The layout comes from brand.json -> board.layout;
if it is missing, the brief picks it (pick_layout.py logic) and the reason is
printed — there is no silent default. board.mode only overrides the layout's
colour treatment. Every panel carries data-panel="<name>" so render.py exports
it on its own as well as inside the board.

    python3 scripts/build_board.py                    # board.layout, or picked from the brief
    python3 scripts/build_board.py --layout gallery   # preview one layout without saving
    python3 scripts/build_board.py --compare          # every eligible layout → pages/compare/
"""
from __future__ import annotations

import argparse
import copy
import datetime
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from lib import studio  # noqa: E402
from lib.brandlib import (  # noqa: E402
    contrast, ensure, family_name, find_project, font_face_css, load_brand, luminance, mix,
    palette_of, read_svg, set_svg_size, svg_recolor, theme,
)
from lib.layouts import LAYOUTS, brand_hue, choose, rank  # noqa: E402

W = 1600  # board width @1x

LABELS = {
    "en": dict(story="Brand Story", promise="Brand Promise", icons="Iconography",
               usage="Logo Usage", variations="Logo Variations", palette="Color Palette",
               type="Typography", concept="Concept", symbol="Symbol", logomark="Logo Mark",
               clear="Clear Space", primary="Primary", reverse="Reverse", mono="Monochrome",
               on_color="On colour", clear_note="clear space = 1 × {u} on all sides",
               app_icon="App icon", cta="Get Started", keywords="Keywords", contact="Contact",
               stands_in="stands in for {x}", identity="Brand Identity",
               primary_font="Primary typeface", weights="Weights"),
    "pt": dict(story="História da marca", promise="Promessa da marca", icons="Iconografia",
               usage="Uso do logo", variations="Variações do logo", palette="Paleta de cores",
               type="Tipografia", concept="Conceito", symbol="Símbolo", logomark="Logotipo",
               clear="Área de proteção", primary="Primária", reverse="Reversa",
               mono="Monocromática", on_color="Sobre a cor",
               clear_note="área de proteção = 1 × {u} em todos os lados",
               app_icon="Ícone de app", cta="Começar", keywords="Palavras-chave",
               contact="Contato", stands_in="substitui {x}", identity="Identidade de marca",
               primary_font="Fonte principal", weights="Pesos"),
}


# --------------------------------------------------------------------------
# context and surfaces
# --------------------------------------------------------------------------


class Ctx:
    def __init__(self, project: Path, brand: dict, t: dict, layout: str):
        self.project, self.brand, self.t, self.layout = project, brand, t, layout
        self.gen = project / "identity" / "generated"
        self.core = brand["brand"]
        self.board = brand.get("board", {})
        roles = {c.get("role"): c["hex"].upper() for c in brand.get("colors", []) if c.get("role")}
        self.ink = roles.get("ink", "#0D0D0D")
        self.paper = roles.get("surface", "#FFFFFF")
        self.hue = brand_hue(brand)
        self.hue_way = None
        for role in ("brand", "accent"):
            if roles.get(role) == self.hue and (self.gen / "variations" / f"stacked-{role}.svg").exists():
                self.hue_way = role
        if self.hue and contrast(self.hue, "#FFFFFF") < 3:
            self.hue_way = None      # a light hue cannot carry the mark on white
        lang = self.board.get("lang", "en")
        self.L = dict(LABELS.get(lang, LABELS["en"]))
        self.L.update(self.board.get("labels", {}))
        self.cta = self.board.get("cta") or self.L["cta"]
        self.disp = family_name(brand["typography"]["display"]["fontsource"])
        self.text = family_name((brand["typography"].get("text")
                                 or brand["typography"]["display"])["fontsource"])

    def svg(self, name: str, width=None, height=None) -> str:
        m = read_svg(self.gen / "variations" / f"{name}.svg")
        return set_svg_size(m, width=width, height=height) if (width or height) else m

    def icons(self, n: int = 12) -> list[str]:
        order = [i["name"] for i in (self.brand.get("icons") or [])]
        if not order:
            default = Path(__file__).parent.parent / "templates" / "icons-default.json"
            order = [i["name"] for i in json.loads(default.read_text())["icons"]]
        files = [self.gen / "icons" / f"{x}.svg" for x in order]
        return [read_svg(f) for f in files if f.exists()][:n]

    def icon(self, name: str) -> str | None:
        f = self.gen / "icons" / f"{name}.svg"
        return read_svg(f) if f.exists() else None

    def tagline(self, sep="<br>") -> str:
        return (self.core.get("tagline") or "").replace("\n", sep)

    def words(self) -> list[str]:
        k = self.core.get("keywords") or (self.core.get("voice") or {}).get("adjectives") \
            or self.brand.get("brief", {}).get("character") or []
        return list(k)[:4]

    def attributes(self) -> list[tuple[str, str]]:
        """(icon svg, label) — concept.attributes, else the brief's adjectives."""
        attrs = self.brand.get("concept", {}).get("attributes") or []
        icons = self.icons(12)
        out = []
        if attrs:
            for i, a in enumerate(attrs[:3]):
                svg = self.icon(a.get("icon", "")) or (icons[i] if i < len(icons) else "")
                out.append((svg, a.get("label", "")))
        else:
            for i, w in enumerate(self.words()[:3]):
                out.append((icons[(i * 3) % max(1, len(icons))] if icons else "", w.capitalize()))
        return out


def surf(ctx: Ctx, kind: str) -> dict:
    """Colours for a panel surface: bg, border, head, text, muted, way (which
    cut of the mark sits on it), mark (hex for recolouring diagrams)."""
    t = ctx.t
    if kind == "panel":
        bg, border = t["panel"], f"1px solid {t['line']}"
    elif kind == "panel2":
        bg, border = t["panel2"], f"1px solid {t['line']}"
    elif kind == "outline":
        dark_page = luminance(t["page"]) < 0.4
        bg = "transparent"
        border = f"1px solid {'rgba(255,255,255,.55)' if dark_page else 'rgba(0,0,0,.30)'}"
        return _colors(bg, border, light=not dark_page, ctx=ctx, page=t["page"])
    elif kind == "light":
        bg, border = ctx.paper if luminance(ctx.paper) > .8 else "#FFFFFF", "none"
    elif kind == "grey":
        bg, border = "#F1F2F4", "none"
    elif kind == "brand":
        bg, border = (ctx.hue or t["panel2"]), "none"
    elif kind == "ink":
        bg, border = ctx.ink if luminance(ctx.ink) < .1 else "#0A0A0A", "none"
        if bg.upper() == str(t["page"]).upper():     # would vanish into the page
            bg = mix(bg, "#FFFFFF", 0.06)
    else:
        raise ValueError(kind)
    return _colors(bg, border, light=luminance(bg) > 0.45, ctx=ctx, page=bg)


def _colors(bg, border, light, ctx, page):
    if light:
        return dict(bg=bg, border=border, head=ctx.ink, text="rgba(0,0,0,.64)",
                    muted="rgba(0,0,0,.42)", line="rgba(0,0,0,.10)", way="primary",
                    mark=ctx.ink, icon="rgba(0,0,0,.78)", light=True, page=page)
    return dict(bg=bg, border=border, head="#FFFFFF", text="rgba(255,255,255,.72)",
                muted="rgba(255,255,255,.48)", line="rgba(255,255,255,.14)", way="white",
                mark="#FFFFFF", icon="rgba(255,255,255,.82)", light=False, page=page)


def sec(name, area, inner, s, style="", pad=True):
    padding = "" if pad else "padding:0;"
    return (f'<section class="p" data-panel="{name}" style="{area};background:{s["bg"]};'
            f'border:{s["border"]};color:{s["text"]};{padding}{style}">{inner}</section>')


def head(title, s, rule=True, slash=False):
    lab = (f'<span style="color:var(--hue-accent,{s["head"]})">/</span>&nbsp; {title}'
           if slash else title)
    cls = "h rule" if rule else "h"
    return f'<h2 class="{cls}" style="color:{s["head"]};border-color:{s["line"]}">{lab}</h2>'


def stage(kind: str, ctx: Ctx) -> tuple[str, str]:
    """(class, inline style) for a studio stage: dark | lt | hue."""
    if kind == "hue" and ctx.hue:
        return "stage hue", f"--hue:{ctx.hue}"
    if kind == "lt":
        return "stage lt", ""
    return "stage", ""


def stage_sec(name, area, inner, kind, ctx, style=""):
    cls, st = stage(kind, ctx)
    return (f'<section class="p" data-panel="{name}" style="{area};padding:0;border:none;{style}">'
            f'<div class="{cls}" style="position:absolute;inset:0;border-radius:inherit;{st}">'
            f'<div class="floor"></div>{inner}</div></section>')


def ghost(ctx, size, right=None, top=None, left=None, bottom=None, opacity=.08, way="white"):
    pos = ";".join(f"{k}:{v}px" for k, v in
                   (("right", right), ("top", top), ("left", left), ("bottom", bottom)) if v is not None)
    return (f'<div style="position:absolute;{pos};width:{size}px;opacity:{opacity};'
            f'pointer-events:none">{ctx.svg(f"symbol-{way}", width="100%")}</div>')


# --------------------------------------------------------------------------
# panel content (shared by every layout)
# --------------------------------------------------------------------------


def card_back(ctx, light=False):
    c = ctx.core.get("contact", {})
    main, sub = ("#111", "rgba(0,0,0,.50)") if light else ("#fff", "rgba(255,255,255,.50)")
    return (
        f'<div style="font-family:\'{ctx.disp}\';color:{main};font-size:11px;'
        f'letter-spacing:.02em">{c.get("person","")}<div style="color:{sub};'
        f'font-size:9px;margin-top:3px">{c.get("role","")}</div></div>'
        f'<div style="color:{sub};font-size:9px;line-height:1.7">'
        f'{ctx.tagline(" ")}<br>{c.get("email","")} · {c.get("site","")}</div>'
    )


def card(ctx, size=430, finish="matte"):
    light = finish == "light"
    front = ctx.svg("logomark-primary" if light else "logomark-white")
    return studio.business_card(front, card_back(ctx, light), None, size, finish=finish)


def phone(ctx, size=250):
    screen = (
        f'<div style="font-family:\'{ctx.disp}\';color:rgba(255,255,255,.70);font-size:{size*.05:.0f}px;'
        f'text-align:center;line-height:1.6">{(ctx.core.get("tagline") or "").split(chr(10))[0]}</div>'
        f'<div style="margin-top:6px;background:{ctx.hue or "#fff"};'
        f'color:{"#fff" if ctx.hue and luminance(ctx.hue) < .45 else "#111"};border-radius:99px;'
        f'padding:{size*.035:.0f}px {size*.09:.0f}px;font-size:{size*.045:.0f}px;'
        f'font-family:\'{ctx.disp}\';font-weight:500">{ctx.cta}</div>'
    )
    return studio.phone(ctx.svg("symbol-white"), None, screen, size)


def letterhead(ctx, size=260, tilt=-4):
    c = ctx.core.get("contact", {})
    lines = [ctx.core.get("story", "")] + list(ctx.core.get("promise", []))[:3]
    meta = "<br>".join(x for x in (c.get("address"), c.get("site"), c.get("email")) if x)
    return studio.letterhead(ctx.svg("horizontal-primary"), [x for x in lines if x], meta,
                             ctx.core.get("legal", ""), size, tilt, font=ctx.text)


def email_sig(ctx, size=440):
    c = ctx.core.get("contact", {})
    rows = [x for x in (c.get("email"), c.get("phone"), c.get("site")) if x]
    return studio.email_signature(ctx.svg("logomark-primary"), c.get("person", ""),
                                  c.get("role", ""), rows, ctx.ink, size, font=ctx.text)


def app_icon(ctx, size=140, label=True):
    bg = ctx.hue if ctx.hue and luminance(ctx.hue) < .45 else "linear-gradient(160deg,#1E1E1E,#0B0B0B)"
    return studio.app_icon(ctx.svg("symbol-white"), bg, size,
                           ctx.core["name"] if label else "")


def favicons(ctx, s):
    cells = ""
    for px in (48, 32, 16):
        cells += (f'<div style="text-align:center"><div style="width:{px}px;height:{px}px;'
                  f'margin:0 auto">{ctx.svg("symbol-" + s["way"], width=str(px))}</div>'
                  f'<div style="font-size:9px;color:{s["muted"]};margin-top:8px">{px}px</div></div>')
    return f'<div style="display:flex;align-items:flex-end;gap:22px">{cells}</div>'


def swatches(ctx, s, height=66, show_name=False):
    pal = palette_of(ctx.brand)
    out = ""
    for c in pal:
        border = "border:1px solid rgba(0,0,0,.18);" if c["lstar"] > 88 else f"border:1px solid {s['line']};"
        name = (f'<div style="font-size:10px;color:{s["head"]};margin-top:8px">{c["name"]}</div>'
                if show_name else "")
        out += (f'<div style="text-align:center"><div style="height:{height}px;border-radius:10px;'
                f'background:{c["hex"]};{border}"></div>{name}<div style="font-size:9px;'
                f'color:{s["muted"]};margin-top:{4 if show_name else 9}px;letter-spacing:.03em">'
                f'{c["hex"]}</div></div>')
    return f'<div style="display:grid;grid-template-columns:repeat({len(pal)},1fr);gap:12px">{out}</div>'


def specimen(ctx, s, aa=62):
    disp = ctx.brand["typography"]["display"]
    fam = family_name(disp["fontsource"])
    sub = disp.get("substituted_for")
    note = (f'<div class="cap" style="margin-top:12px;color:{s["muted"]}">'
            f'{ctx.L["stands_in"].format(x=sub)}</div>') if sub else ""
    return (f'<div style="font-family:\'{fam}\';color:{s["head"]};font-size:{aa}px;font-weight:700;'
            f'line-height:1;letter-spacing:-.02em">Aa</div>'
            f'<div style="font-family:\'{fam}\';color:{s["head"]};font-size:15px;margin:14px 0 16px;'
            f'font-weight:500">{fam}</div>'
            f'<div style="font-family:\'{fam}\';font-size:11.5px;letter-spacing:.055em;line-height:2.0;'
            f'color:{s["text"]}">ABCDEFGHIJKLMNOPQRSTUVWXYZ<br>abcdefghijklmnopqrstuvwxyz<br>'
            f'0123456789</div>{note}')


def icon_grid(ctx, s, cols=4, px=30, row_h=56, n=12):
    cells = "".join(
        f'<div style="display:flex;align-items:center;justify-content:center;height:{row_h}px;'
        f'color:{s["icon"]}">{set_svg_size(i, width=str(px), height=str(px))}</div>'
        for i in ctx.icons(n))
    return f'<div style="display:grid;grid-template-columns:repeat({cols},1fr);gap:14px 8px">{cells}</div>'


def edge(ctx, hex_):
    """Border for a colour tile: visible whenever the tile is close to the page."""
    try:
        close = contrast(hex_, ctx.t["page"]) < 1.25
    except Exception:
        close = False
    if not close:
        return "none"
    return "1px solid rgba(255,255,255,.16)" if luminance(hex_) < .4 else "1px solid rgba(0,0,0,.14)"


def fam_display(ctx):
    return family_name(ctx.brand["typography"]["display"]["fontsource"])


# --------------------------------------------------------------------------
# layout 1 — studio (reference-01): the canonical 12-column system board
# --------------------------------------------------------------------------


def layout_studio(ctx: Ctx) -> tuple[str, str]:
    t, L = ctx.t, ctx.L
    P = surf(ctx, "panel")
    way = P["way"]
    glow = "rgba(0,0,0,.05)" if P["light"] else "rgba(255,255,255,.05)"
    title = ctx.board.get("title", "Brand Identity System")
    out = []

    out.append(f"""
<section class="p" data-panel="01-hero" style="grid-column:1/9;grid-row:1/3;padding:56px;min-height:470px;
  background:radial-gradient(120% 90% at 8% 0%,{glow},transparent 58%),{t['panel']};border:{P['border']}">
  <div style="position:absolute;right:-56px;top:50%;transform:translateY(-50%)">
    {studio.puck(ctx.svg('logomark-white'), None, 300)}</div>
  <div style="position:relative;width:56%;height:100%;display:flex;flex-direction:column;justify-content:space-between">
    <div style="width:340px">{ctx.svg(f'stacked-{way}', width='340')}</div>
    <div>
      <div style="font-family:'{ctx.disp}';color:{t['head']};font-size:22px;margin-top:34px;line-height:1.55">{ctx.tagline()}</div>
      <div style="height:1px;background:{t['line']};margin:30px 0 16px;width:78%"></div>
      <div class="cap">{title}</div>
    </div>
  </div>
</section>""")

    out.append(sec("02-story", "grid-column:9/13;grid-row:1/2",
                   f'<div style="position:absolute;right:30px;top:26px;opacity:.85">'
                   f'{ctx.svg(f"logomark-{way}", width="76")}</div>'
                   f'<h2 class="h rule" style="padding-right:96px;color:{P["head"]}">{L["story"]}</h2>'
                   f'<p>{ctx.core.get("story","")}</p>', P))
    out.append(sec("03-promise", "grid-column:9/13;grid-row:2/3",
                   head(L["promise"], P) + "".join(f"<p style='margin-bottom:6px'>{x}</p>"
                                                    for x in ctx.core.get("promise", [])), P))
    out.append(sec("04-iconography", "grid-column:9/13;grid-row:3/4",
                   head(L["icons"], P) + icon_grid(ctx, P), P))

    cs = svg_recolor(read_svg(ctx.gen / "clearspace.svg"), P["mark"])
    unit = (ctx.brand["logo"].get("clearspace") or {}).get("unit_label", "a")
    out.append(sec("05-logo-usage", "grid-column:1/6;grid-row:3/4",
                   head(L["usage"], P) + f'<div style="padding:6px 4px">{set_svg_size(cs, width="100%")}</div>'
                   f'<div class="cap" style="margin-top:16px">{L["clear_note"].format(u=unit)}</div>', P))

    disc_bg, disc_way = ("#FFFFFF", "primary") if way == "white" else (t["head"], "white")
    disc = (f'<div style="width:92px;height:92px;border-radius:50%;background:{disc_bg};display:flex;'
            f'align-items:center;justify-content:center">{ctx.svg(f"symbol-{disc_way}", width="46")}</div>')

    def tile(inner, cap, h=140):
        return (f'<div class="tile" style="height:{h}px;padding:20px 16px 34px">{inner}'
                f'<div class="tilecap">{cap}</div></div>')
    out.append(sec("06-logo-tiles", "grid-column:6/9;grid-row:3/4",
                   f'<div style="display:grid;grid-template-columns:1fr 1fr;gap:16px">'
                   f'{tile(disc, L["symbol"])}{tile(ctx.svg(f"logomark-{way}", width="118"), L["logomark"])}</div>'
                   f'<div style="margin-top:16px">{tile(set_svg_size(cs, width="190"), L["clear"], 168)}</div>',
                   P, "padding:28px"))

    rows = [(L["primary"], "white", ctx.ink), (L["reverse"], "primary", ctx.paper), (L["mono"], "mono", ctx.ink)]
    tiles = "".join(
        f'<div><div style="background:{bg};{"border:1px solid " + t["line"] + ";" if bg == ctx.ink else ""}'
        f'border-radius:12px;height:150px;display:flex;align-items:center;justify-content:center">'
        f'{ctx.svg(f"stacked-{w}", width="128")}</div><div class="tilecap" style="position:static;'
        f'margin-top:10px">{cap}</div></div>' for cap, w, bg in rows)
    out.append(sec("07-variations", "grid-column:1/8;grid-row:4/5",
                   head(L["variations"], P) + f'<div style="display:grid;grid-template-columns:repeat(3,1fr);'
                   f'gap:20px">{tiles}</div>', P))

    out.append(stage_sec("08-business-card", "grid-column:8/13;grid-row:4/5;min-height:330px",
                         f'<div style="transform:scale(.74) translateY(-10px)">{card(ctx, 430)}</div>', "dark", ctx))

    out.append(sec("09-color", "grid-column:1/5;grid-row:5/6", head(L["palette"], P) + swatches(ctx, P), P))
    out.append(sec("10-app-icon", "grid-column:5/9;grid-row:5/6",
                   head(L["app_icon"], P) + f'<div style="display:flex;align-items:center;gap:40px;'
                   f'padding-top:6px">{app_icon(ctx, 120, label=False)}{favicons(ctx, P)}</div>', P))
    out.append(sec("11-typography", "grid-column:9/13;grid-row:5/6", head(L["type"], P) + specimen(ctx, P), P))
    return "".join(out), "grid-auto-rows:auto"


# --------------------------------------------------------------------------
# layout 2 — mosaic (reference-02): outlined and filled cells, irregular sizes
# --------------------------------------------------------------------------


def layout_mosaic(ctx: Ctx) -> tuple[str, str]:
    L = ctx.L
    O, B = surf(ctx, "outline"), surf(ctx, "brand" if ctx.hue else "light")
    LT, P2 = surf(ctx, "light"), surf(ctx, "brand" if ctx.hue else "panel2")
    fam = fam_display(ctx)
    out = []
    out.append(sec("01-wordmark", "grid-column:1/9;grid-row:1/2",
                   f'<div style="display:flex;align-items:center;justify-content:center;height:100%">'
                   f'{ctx.svg("logomark-" + O["way"], width="420")}</div>', O))
    out.append(sec("02-wordmark-vertical", "grid-column:9/13;grid-row:1/3",
                   f'<div style="position:absolute;left:50%;top:50%;width:360px;'
                   f'transform:translate(-50%,-50%) rotate(90deg)">{ctx.svg("logomark-" + B["way"], width="100%")}</div>', B))
    lt_way = ctx.hue_way or LT["way"]
    out.append(sec("03-symbol-light", "grid-column:1/5;grid-row:2/3",
                   f'<div style="display:flex;align-items:center;justify-content:center;height:100%">'
                   f'{ctx.svg("symbol-" + lt_way, width="96")}</div>', LT))
    out.append(sec("04-symbol-color", "grid-column:5/9;grid-row:2/3",
                   f'<div style="display:flex;align-items:center;justify-content:center;height:100%">'
                   f'{ctx.svg("symbol-" + P2["way"], width="96")}</div>', P2))
    out.append(sec("05-concept", "grid-column:1/5;grid-row:3/4",
                   '<div style="font-size:16px;line-height:1.7"><p>COMANDA ORGANIZADA</p>'
                   '<h2 style="font-size:32px;line-height:1.15">Cada pedido<br>em seu lugar.</h2>'
                   '<p>Três linhas vazadas numa ficha compacta. Um sinal claro para organizar o atendimento.</p></div>', LT, "padding:40px"))
    grid = svg_recolor(read_svg(ctx.gen / "grid.svg"), "#FFFFFF" if not O["light"] else ctx.ink)
    out.append(sec("06-hero", "grid-column:5/13;grid-row:3/4",
                   f'<div style="position:absolute;right:-120px;top:-60px;width:620px;opacity:.10">'
                   f'{set_svg_size(grid, width="100%")}</div>'
                   f'<div style="position:relative;height:100%;display:flex;flex-direction:column;justify-content:space-between">'
                   f'<div style="width:230px">{ctx.svg("stacked-" + O["way"], width="100%")}</div>'
                   f'<div style="font-family:\'{ctx.disp}\';color:{O["head"]};font-size:22px;line-height:1.45">'
                   f'{ctx.tagline()}</div></div>', O, "padding:52px"))
    out.append(sec("07-typography", "grid-column:1/9;grid-row:4/5",
                   f'<div style="position:relative;height:100%;display:flex;align-items:center">'
                   f'<div style="font-family:\'{fam}\';color:{O["head"]};font-size:34px;font-weight:600;z-index:1">{fam}</div>'
                   f'<div style="position:absolute;right:-10px;bottom:-78px;font-family:\'{fam}\';font-weight:700;'
                   f'font-size:300px;line-height:1;color:{O["head"]};letter-spacing:-.03em">Aa</div></div>',
                   O, "padding:0 40px"))
    out.append(sec("08-lockup", "grid-column:9/13;grid-row:4/5",
                   ghost(ctx, 200, right=90, top=-5, opacity=.07, way=O["way"]) +
                   f'<div style="position:relative;display:flex;align-items:center;justify-content:center;height:100%">'
                   f'{ctx.svg("horizontal-" + O["way"], width="250")}</div>', O))
    out.append(sec("09-clearspace", "grid-column:1/5;grid-row:5/6",
                   '<div style="height:100%;display:flex;align-items:center">' +
                   set_svg_size(read_svg(ctx.gen / "clearspace.svg"), width="100%") + '</div>', LT, "padding:28px"))
    out.append(sec("10-monochrome", "grid-column:5/9;grid-row:5/6",
                   '<div style="height:100%;display:flex;align-items:center;justify-content:center">' +
                   ctx.svg("horizontal-white", width="300") + '</div>', O, "padding:30px"))
    pal = palette_of(ctx.brand)
    bars = "".join(f'<div style="flex:1;border-radius:10px;background:{c["hex"]};display:flex;align-items:center;'
                   f'padding:0 18px;font-size:11px;letter-spacing:.04em;border:1px solid rgba(255,255,255,.08);'
                   f'color:{"#111" if c["lstar"] > 60 else "rgba(255,255,255,.8)"}">{c["hex"]}</div>' for c in pal)
    out.append(sec("11-color", "grid-column:9/13;grid-row:5/6",
                   f'<div style="display:flex;flex-direction:column;gap:10px;height:100%">{bars}</div>', O, "padding:24px"))
    out.append(sec("12-iconography", "grid-column:1/9;grid-row:6/7",
                   icon_grid(ctx, O, cols=6, px=30, row_h=50), O, "padding:32px 40px"))
    out.append(stage_sec("13-app-icon", "grid-column:9/13;grid-row:6/7", app_icon(ctx, 104), "dark", ctx))
    return "".join(out), "grid-template-rows:250px 190px 440px 210px 360px 190px"


# --------------------------------------------------------------------------
# layout 3 — flood (reference-03): the brand hue is the page
# --------------------------------------------------------------------------


def layout_flood(ctx: Ctx) -> tuple[str, str]:
    B, LT = surf(ctx, "brand"), surf(ctx, "light")
    fam = fam_display(ctx)
    out = []
    out.append(sec("01-hero", "grid-column:1/13;grid-row:1/2",
                   ghost(ctx, 520, right=-90, bottom=-160, opacity=.10, way=B["way"]) +
                   f'<div style="position:relative;display:flex;flex-direction:column;justify-content:center;height:100%;gap:28px">'
                   f'<div style="width:760px">{ctx.svg("horizontal-" + B["way"], width="100%")}</div>'
                   f'<div style="font-family:\'{ctx.disp}\';color:{B["text"]};font-size:20px;letter-spacing:.02em">'
                   f'{ctx.tagline(" ")}</div></div>', B, "padding:0 80px"))
    out.append(sec("02-lockup-light", "grid-column:1/7;grid-row:2/3",
                   f'<div style="display:flex;align-items:center;justify-content:center;height:100%">'
                   f'{ctx.svg("horizontal-" + (ctx.hue_way or LT["way"]), width="400")}</div>', LT))
    out.append(stage_sec("03-app", "grid-column:7/13;grid-row:2/4",
                         f'<div style="display:flex;align-items:center;gap:70px;transform:translateY(30px)">'
                         f'{phone(ctx, 220)}<div style="color:#fff">{app_icon(ctx, 120)}</div></div>', "hue", ctx))
    out.append(sec("04-typography", "grid-column:1/7;grid-row:3/4",
                   f'<div style="display:flex;justify-content:space-between;align-items:center;height:100%">'
                   f'<div><div style="font-family:\'{fam}\';color:{B["head"]};font-size:26px;font-weight:600;'
                   f'border-bottom:1px solid {B["line"]};padding-bottom:6px;display:inline-block">{fam}</div>'
                   f'<div style="font-family:\'{fam}\';font-size:10.5px;letter-spacing:.06em;line-height:1.9;margin-top:10px;'
                   f'color:{B["text"]}">ABCDEFGHIJKLMNOPQRSTUVWXYZ<br>abcdefghijklmnopqrstuvwxyz<br>0123456789</div></div>'
                   f'<div style="font-family:\'{fam}\';color:{B["head"]};font-size:150px;font-weight:600;line-height:1;'
                   f'letter-spacing:-.03em">Aa</div></div>', B, "padding:0 48px"))
    pal = palette_of(ctx.brand)
    by_role = {c["role"]: c for c in pal}
    picks = [by_role.get(r) for r in ("brand", "accent", "surface", "ink")]
    picks = [p for p in picks if p][:3] or pal[:3]
    tiles = "".join(
        f'<div class="p" style="background:{c["hex"]};border:{edge(ctx, c["hex"])};display:flex;align-items:center;justify-content:center;'
        f'font-family:\'{ctx.disp}\';font-size:24px;letter-spacing:.02em;'
        f'color:{"#111" if c["lstar"] > 60 else "#fff"}">{c["hex"]}</div>' for c in picks)
    out.append(f'<section data-panel="05-color" style="grid-column:1/13;grid-row:4/5;display:grid;'
               f'grid-template-columns:repeat({len(picks)},1fr);gap:var(--gap)">{tiles}</section>')
    out.append(stage_sec("06-letterhead", "grid-column:1/6;grid-row:5/6", letterhead(ctx, 220, -6), "lt", ctx))
    out.append(stage_sec("07-business-card", "grid-column:6/13;grid-row:5/6",
                         f'<div style="transform:scale(.80)">{card(ctx, 440, "light")}</div>', "hue", ctx))
    out.append(sec("08-iconography", "grid-column:1/7;grid-row:6/7",
                   icon_grid(ctx, LT, cols=6, px=32, row_h=66), LT, "padding:48px"))
    out.append(stage_sec("09-email", "grid-column:7/13;grid-row:6/7", email_sig(ctx, 460), "hue", ctx))
    return "".join(out), "grid-template-rows:400px 210px 210px 180px 380px 260px"


# --------------------------------------------------------------------------
# layout 4 — flood-wide (reference-04): wide cells, strategy panels
# --------------------------------------------------------------------------


def layout_flood_wide(ctx: Ctx) -> tuple[str, str]:
    B, LT, INK, GR = surf(ctx, "brand"), surf(ctx, "light"), surf(ctx, "ink"), surf(ctx, "grey")
    c = ctx.core.get("contact", {})
    out = []
    out.append(sec("01-hero", "grid-column:1/13;grid-row:1/2",
                   ghost(ctx, 560, right=-150, top=-90, opacity=.12, way=B["way"]) +
                   f'<div style="position:relative;display:flex;flex-direction:column;align-items:center;justify-content:center;'
                   f'height:100%;gap:26px"><div style="width:640px">{ctx.svg("horizontal-" + B["way"], width="100%")}</div>'
                   f'<div style="font-family:\'{ctx.disp}\';color:{B["head"]};font-size:18px;letter-spacing:.24em;'
                   f'text-transform:uppercase">{ctx.tagline("&nbsp;&nbsp;")}</div></div>', B))
    out.append(sec("02-stacked", "grid-column:1/5;grid-row:2/3",
                   f'<div style="display:flex;align-items:center;justify-content:center;height:100%">'
                   f'{ctx.svg("stacked-" + (ctx.hue_way or GR["way"]), width="230")}</div>', GR))
    words = "".join(f'<div>{w.upper()}</div>' for w in ctx.words())
    out.append(sec("03-keywords", "grid-column:5/9;grid-row:2/3",
                   ghost(ctx, 300, right=-80, top=30, opacity=.12, way=B["way"]) +
                   f'<div style="position:relative;display:flex;flex-direction:column;justify-content:center;height:100%">'
                   f'<div style="font-family:\'{ctx.disp}\';color:{B["head"]};font-size:26px;line-height:1.5;'
                   f'letter-spacing:.06em;font-weight:500">{words}</div>'
                   f'<div style="width:44px;height:2px;background:{B["head"]};margin-top:22px"></div></div>', B, "padding:0 44px"))
    out.append(sec("04-story", "grid-column:9/13;grid-row:2/3",
                   head(ctx.L["story"], INK) + f'<p>{ctx.core.get("story","")}</p>', INK))
    attrs = "".join(
        f'<div style="text-align:center;color:{INK["head"]}"><div style="width:34px;margin:0 auto">'
        f'{set_svg_size(svg, width="34", height="34") if svg else ""}</div>'
        f'<div style="font-size:10px;letter-spacing:.12em;text-transform:uppercase;margin-top:10px;'
        f'color:{INK["muted"]}">{lab}</div></div>' for svg, lab in ctx.attributes())
    promise = "<br>".join(ctx.core.get("promise", [])[:3])
    out.append(sec("05-statement", "grid-column:1/8;grid-row:3/4",
                   f'<div style="display:flex;flex-direction:column;justify-content:center;height:100%;gap:34px">'
                   f'<div style="font-family:\'{ctx.disp}\';color:{INK["head"]};font-size:30px;font-weight:600;'
                   f'line-height:1.3;text-transform:uppercase;letter-spacing:.01em">{promise or ctx.tagline()}</div>'
                   f'<div style="display:flex;gap:44px">{attrs}</div></div>', INK, "padding:0 56px"))
    out.append(stage_sec("06-business-card", "grid-column:8/13;grid-row:3/4",
                         f'<div style="transform:scale(.68)">{card(ctx, 440, "light")}</div>', "lt", ctx))
    rows = []
    for key, icon in (("handle", "message"), ("site", "globe"), ("email", "mail"), ("phone", "message")):
        if c.get(key) and len(rows) < 3:
            svg = ctx.icon(icon) or ""
            rows.append(f'<div style="display:flex;align-items:center;gap:16px;color:{LT["head"]}">'
                        f'<div style="width:22px;color:{LT["icon"]}">{set_svg_size(svg, width="22", height="22")}</div>'
                        f'<span style="font-size:14px">{c[key]}</span></div>')
    out.append(sec("07-contact", "grid-column:1/5;grid-row:4/5",
                   ghost(ctx, 210, right=-40, bottom=-50, opacity=.10,
                         way=(ctx.hue_way or "primary")) +
                   f'<div style="position:relative;display:flex;flex-direction:column;justify-content:center;gap:22px;'
                   f'height:100%">{"".join(rows)}</div>', LT, "padding:0 44px"))
    out.append(sec("08-app-icon", "grid-column:5/9;grid-row:4/5",
                   f'<div style="display:flex;align-items:center;justify-content:center;height:100%;color:{LT["head"]}">'
                   f'{app_icon(ctx, 130)}</div>', LT))
    out.append(stage_sec("09-app", "grid-column:9/13;grid-row:4/6",
                         f'<div style="transform:rotate(-6deg)">{phone(ctx, 190)}</div>', "hue", ctx))
    pal = palette_of(ctx.brand)[:4]
    bars = "".join(
        f'<div class="p" style="background:{c_["hex"]};border:{edge(ctx, c_["hex"])};'
        f'display:flex;align-items:center;padding:0 26px;font-size:14px;letter-spacing:.03em;'
        f'color:{"#111" if c_["lstar"] > 60 else "#fff"}">{c_["hex"]}</div>' for c_ in pal)
    out.append(f'<section data-panel="10-color" style="grid-column:1/9;grid-row:5/6;display:grid;'
               f'grid-template-columns:repeat({len(pal)},1fr);gap:var(--gap)">{bars}</section>')
    out.append(stage_sec("11-letterhead", "grid-column:1/7;grid-row:6/7", letterhead(ctx, 220, -4), "lt", ctx))
    out.append(stage_sec("12-email", "grid-column:7/13;grid-row:6/7", email_sig(ctx, 450), "hue", ctx))
    return "".join(out), "grid-template-rows:380px 300px 300px 280px 110px 340px"


# --------------------------------------------------------------------------
# layout 5 — editorial (reference-05): full-bleed bands, the idea written out
# --------------------------------------------------------------------------


def layout_editorial(ctx: Ctx) -> tuple[str, str]:
    L = ctx.L
    INK, LT, GR = surf(ctx, "ink"), surf(ctx, "light"), surf(ctx, "grey")
    BR = surf(ctx, "brand" if ctx.hue else "panel2")
    accent = ctx.hue or "rgba(255,255,255,.55)"
    fam = fam_display(ctx)
    title = ctx.board.get("title", "Brand Identity System")
    lines = (ctx.core.get("tagline") or ctx.core["name"]).split("\n")
    tag = lines[0] + (f'<br><span style="color:{accent}">{lines[1]}</span>' if len(lines) > 1 else "")
    band = "grid-column:1/13;border-radius:0"
    out = []

    out.append(sec("01-hero", f"{band};min-height:440px",
                   ghost(ctx, 560, right=40, top=-60, opacity=.07) +
                   f'<div style="position:relative;display:grid;grid-template-columns:1.1fr 1fr;height:100%;align-items:center">'
                   f'<div><div class="cap" style="letter-spacing:.42em">{title}</div>'
                   f'<div style="font-family:\'{ctx.disp}\';font-weight:700;font-style:italic;color:#fff;font-size:68px;'
                   f'line-height:1.02;letter-spacing:-.02em;margin:28px 0 26px">{tag}</div>'
                   f'<p style="max-width:44ch">{(ctx.core.get("story","").split(". ")[0]).rstrip(".")}.</p>'
                   f'<div style="width:46px;height:2px;background:{accent};margin-top:26px"></div></div>'
                   f'<div style="display:flex;justify-content:center">{ctx.svg("horizontal-white", width="400")}</div></div>',
                   INK, "padding:64px 72px"))

    attrs = "".join(
        f'<div style="text-align:center;padding:0 10px;{"border-left:1px solid " + LT["line"] + ";" if i else ""}">'
        f'<div style="width:28px;margin:0 auto;color:{accent if ctx.hue else LT["head"]}">'
        f'{set_svg_size(svg, width="28", height="28") if svg else ""}</div>'
        f'<div style="font-size:11px;color:{LT["head"]};margin-top:12px">{lab}</div></div>'
        for i, (svg, lab) in enumerate(ctx.attributes()))
    concept = ctx.brand.get("concept", {}).get("sentence") or ctx.core.get("story", "")
    out.append(f'<section data-panel="02-concept" style="{band};display:grid;grid-template-columns:5fr 7fr;min-height:420px">'
               f'<div style="background:{LT["bg"]};padding:56px 60px;color:{LT["text"]}">'
               f'{head(L["concept"], LT, rule=False, slash=True)}'
               f'<p style="font-size:15px;line-height:1.75;margin-top:18px">{concept}</p>'
               f'<div style="display:grid;grid-template-columns:repeat(3,1fr);margin-top:40px">{attrs}</div></div>'
               f'<div style="background:{BR["bg"]};display:flex;flex-direction:column;align-items:center;'
               f'justify-content:center;gap:26px;position:relative;overflow:hidden">'
               f'{ghost(ctx, 380, left=-120, bottom=-140, opacity=.10, way=BR["way"])}'
               f'<div style="width:330px;position:relative">{ctx.svg("stacked-" + BR["way"], width="100%")}</div>'
               f'<div style="font-family:\'{ctx.disp}\';font-style:italic;color:{BR["head"]};font-size:20px;position:relative">'
               f'{ctx.tagline(" ")}</div></div></section>')

    pal = palette_of(ctx.brand)
    sw = "".join(
        f'<div style="background:{c["hex"]};border-radius:6px;height:150px;padding:14px;display:flex;flex-direction:column;'
        f'justify-content:flex-end;{"border:1px solid rgba(255,255,255,.12);" if c["lstar"] < 12 else ""}'
        f'color:{"#111" if c["lstar"] > 60 else "#fff"}"><div style="font-size:11px;font-weight:600">{c["name"]}</div>'
        f'<div style="font-size:9.5px;opacity:.7;margin-top:3px">{c["hex"]}</div></div>' for c in pal)
    weights = "<br>".join(str(w) for w in ctx.brand["typography"]["display"].get("weights", [400, 700]))
    out.append(sec("03-color-type", f"{band};min-height:330px",
                   f'<div style="display:grid;grid-template-columns:1fr 1px 1fr;gap:56px;height:100%">'
                   f'<div>{head(L["palette"], INK, rule=False, slash=True)}<div style="display:grid;'
                   f'grid-template-columns:repeat({len(pal)},1fr);gap:12px;margin-top:20px">{sw}</div></div>'
                   f'<div style="background:{INK["line"]}"></div>'
                   f'<div style="position:relative">{head(L["type"], INK, rule=False, slash=True)}'
                   f'<div style="position:absolute;right:0;top:20px;font-family:\'{fam}\';font-size:130px;font-weight:700;'
                   f'color:rgba(255,255,255,.08);line-height:1">Aa</div>'
                   f'<div class="cap" style="margin-top:22px">{L["primary_font"]}</div>'
                   f'<div style="font-family:\'{fam}\';color:#fff;font-size:40px;font-weight:700;margin:6px 0 18px">{fam}</div>'
                   f'<div style="display:flex;gap:40px"><div style="font-family:\'{fam}\';font-size:12px;letter-spacing:.32em;'
                   f'line-height:2;color:{INK["text"]}">ABCDEFGHIJKLM<br>NOPQRSTUVWXYZ<br>0123456789</div>'
                   f'<div style="border-left:1px solid {INK["line"]};padding-left:24px;font-size:12px;line-height:2;'
                   f'color:{INK["muted"]}">{weights}</div></div></div></div>', INK, "padding:48px 72px"))

    var_tiles = [
        (BR["bg"], "horizontal-" + BR["way"], "220"), (ctx.ink, "horizontal-white", "220"),
        ("#FFFFFF", "horizontal-primary", "220"), ("#E4E6EA", "symbol-" + (ctx.hue_way or "primary"), "70"),
    ]
    vt = "".join(f'<div style="background:{bg};height:150px;border-radius:6px;display:flex;align-items:center;'
                 f'justify-content:center">{ctx.svg(n, width=w)}</div>' for bg, n, w in var_tiles)
    out.append(sec("04-variations", f"{band}",
                   head(L["variations"], GR, rule=False, slash=True) +
                   f'<div style="display:grid;grid-template-columns:repeat(4,1fr);gap:16px;margin-top:22px">{vt}</div>',
                   GR, "padding:44px 72px"))

    out.append(sec("05-iconography", f"{band}",
                   f'<div style="display:grid;grid-template-columns:220px 1fr;align-items:center">'
                   f'{head(L["icons"], INK, rule=False, slash=True)}{icon_grid(ctx, INK, cols=12, px=26, row_h=40)}</div>',
                   INK, "padding:34px 72px"))

    apps = (
        f'<section data-panel="06-applications" style="{band};display:grid;grid-template-columns:5fr 3.5fr 3.5fr;'
        f'grid-template-rows:320px 320px;gap:4px;background:#000">'
        f'<div class="stage" style="grid-row:1/3;border-radius:0"><div class="floor"></div>'
        f'<div style="transform:scale(.92)">{card(ctx, 440)}</div></div>'
        f'<div class="stage lt" style="border-radius:0"><div class="floor"></div>{letterhead(ctx, 190, -5)}</div>'
        f'<div class="stage" style="border-radius:0"><div class="floor"></div>'
        f'<div style="transform:translateY(34px)">{phone(ctx, 150)}</div></div>'
        f'<div class="{stage("hue", ctx)[0]}" style="border-radius:0;{stage("hue", ctx)[1]}"><div class="floor"></div>'
        f'<div style="color:#fff">{app_icon(ctx, 110)}</div></div>'
        f'<div class="stage lt" style="border-radius:0"><div class="floor"></div>{email_sig(ctx, 330)}</div>'
        f'</section>')
    out.append(apps)

    year = datetime.date.today().year
    out.append(sec("07-footer", f"{band}",
                   f'<div style="display:flex;align-items:center;justify-content:space-between">'
                   f'{ctx.svg("horizontal-white", width="120")}'
                   f'<div class="cap" style="letter-spacing:.4em">{L["identity"]} · {ctx.core["name"]}</div>'
                   f'<div class="cap">{year}</div>'
                   f'<div style="color:{accent};font-weight:700;font-size:20px;letter-spacing:-.1em;font-style:italic">///</div></div>',
                   INK, "padding:26px 72px"))
    return "".join(out), "grid-auto-rows:auto;--gap:0px;--r:0px"


# --------------------------------------------------------------------------
# layout 6 — gallery (reference-06): light page, applications first
# --------------------------------------------------------------------------


def layout_gallery(ctx: Ctx) -> tuple[str, str]:
    B = surf(ctx, "brand" if ctx.hue else "ink")
    INK = surf(ctx, "ink")
    out = []
    out.append(sec("01-hero", "grid-column:1/9;grid-row:1/2",
                   ghost(ctx, 420, right=-60, bottom=-120, opacity=.14, way=B["way"]) +
                   f'<div style="position:relative;display:flex;flex-direction:column;justify-content:center;height:100%;gap:30px">'
                   f'<div style="width:120px">{ctx.svg("symbol-" + B["way"], width="100%")}</div>'
                   f'<div style="font-family:\'{ctx.disp}\';color:{B["head"]};font-size:46px;font-weight:700;'
                   f'line-height:1.12;letter-spacing:-.015em;max-width:12em">{ctx.tagline()}</div></div>',
                   B, "padding:0 64px"))
    out.append(stage_sec("02-letterhead", "grid-column:9/13;grid-row:1/2", letterhead(ctx, 240, 4), "lt", ctx))
    out.append(stage_sec("03-app-icon", "grid-column:1/5;grid-row:2/3",
                         f'<div style="color:#111">{app_icon(ctx, 130)}</div>', "lt", ctx))
    out.append(stage_sec("04-business-card", "grid-column:5/13;grid-row:2/4",
                         f'<div style="transform:scale(1.05)">{card(ctx, 500, "light")}</div>', "lt", ctx))
    out.append(stage_sec("05-app", "grid-column:1/5;grid-row:3/4",
                         f'<div style="transform:translateY(70px)">{phone(ctx, 170)}</div>', "hue", ctx))
    out.append(sec("06-statement", "grid-column:1/9;grid-row:4/5",
                   f'<div style="display:grid;grid-template-columns:1fr 1px 1fr;gap:48px;align-items:center;height:100%">'
                   f'<div style="display:flex;justify-content:center">{ctx.svg("stacked-white", width="230")}</div>'
                   f'<div style="background:{INK["line"]};height:60%"></div>'
                   f'<div style="font-family:\'{ctx.disp}\';color:#fff;font-size:34px;font-weight:700;line-height:1.2">'
                   f'{ctx.tagline()}</div></div>', INK,
                   "background:radial-gradient(90% 120% at 20% 0%,rgba(255,255,255,.07),transparent 60%)," + INK["bg"]))
    out.append(stage_sec("07-email", "grid-column:9/13;grid-row:4/5", email_sig(ctx, 330), "lt", ctx))
    return "".join(out), "grid-template-rows:440px 330px 330px 380px"


BUILDERS = {
    "studio": layout_studio, "mosaic": layout_mosaic, "flood": layout_flood,
    "flood-wide": layout_flood_wide, "editorial": layout_editorial, "gallery": layout_gallery,
}


# --------------------------------------------------------------------------


def css(t: dict, ctx: Ctx, grid: str) -> str:
    radius = {"flood": 26, "flood-wide": 22, "editorial": 0}.get(ctx.layout, 18)
    gap = {"editorial": 0}.get(ctx.layout, 24)
    pad = {"editorial": 0}.get(ctx.layout, 28)
    return f"""
{studio.STUDIO_CSS}
body{{background:{t['page']};color:{t['text']};
  font-family:'{ctx.text}',system-ui,sans-serif;-webkit-font-smoothing:antialiased;}}
.board{{--r:{radius}px;--gap:{gap}px;{f"--hue-accent:{ctx.hue};" if ctx.hue else ""}width:{W}px;padding:{pad}px;display:grid;
  gap:var(--gap);grid-template-columns:repeat(12,1fr);{grid}}}
.p{{background:{t['panel']};border:1px solid {t['line']};border-radius:var(--r);
  padding:34px 36px;position:relative;overflow:hidden;}}
.board .stage{{border-radius:var(--r);}}
.h{{font-family:'{ctx.disp}',system-ui,sans-serif;color:{t['head']};font-size:19px;
  font-weight:600;letter-spacing:.01em;margin:0 0 14px;}}
.h.rule{{padding-bottom:14px;border-bottom:1px solid {t['line']};margin-bottom:20px;}}
p{{margin:0;font-size:13.5px;line-height:1.72;}}
.board .cap{{position:static;text-align:inherit;font-size:10.5px;letter-spacing:.10em;text-transform:uppercase;color:{t['muted']};}}
.tile{{background:{t['panel2']};border:1px solid {t['line']};border-radius:12px;
  display:flex;align-items:center;justify-content:center;position:relative;}}
.tilecap{{position:absolute;bottom:9px;left:0;right:0;text-align:center;
  font-size:10px;color:{t['muted']};letter-spacing:.04em;}}
svg{{display:block;}}
"""


def resolve_layout(brand: dict, forced: str | None) -> tuple[str, str]:
    if forced:
        return forced, "forçado via --layout"
    chosen = brand.get("board", {}).get("layout")
    if chosen in LAYOUTS:
        return chosen, brand.get("board", {}).get("layout_reason", "board.layout")
    if chosen:
        print(f"  ! board.layout {chosen!r} desconhecido — escolhendo pelo briefing")
    name, why = choose(brand)
    print(f"  board.layout ausente — escolhido pelo briefing: {name} ({why})")
    print("  (salve com: python3 scripts/pick_layout.py --write)")
    return name, why


def build(project: Path, brand: dict, layout: str, out: Path, native: bool = False) -> Path:
    """native=True ignores an explicit board.mode (used by --compare)."""
    b = copy.deepcopy(brand)
    board = b.setdefault("board", {})
    if native or not board.get("mode"):
        board["mode"] = LAYOUTS[layout]["mode"]
    if LAYOUTS[layout]["needs_hue"] and not brand_hue(b):
        print(f"  ! {layout} precisa de uma cor forte na paleta — os painéis de cor ficam escuros")
    t = theme(b)
    ctx = Ctx(project, b, t, layout)
    body, grid = BUILDERS[layout](ctx)
    html = (
        f'<!doctype html><html lang="{board.get("lang", "en")}"><head><meta charset="utf-8">'
        f'<title>{b["brand"]["name"]} — Brand Identity System</title><style>'
        f'{font_face_css(project)}\n*,*::before,*::after{{box-sizing:border-box}}'
        f'html,body{{margin:0;padding:0}}\n{css(t, ctx, grid)}</style></head>'
        f'<body><div class="board" data-layout="{layout}">{body}</div></body></html>'
    )
    ensure(out.parent)
    out.write_text(html, encoding="utf-8")
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--project", default=None)
    ap.add_argument("--layout", choices=list(LAYOUTS), help="preview a layout without saving it")
    ap.add_argument("--compare", action="store_true", help="build every eligible layout into pages/compare/")
    a = ap.parse_args()

    project = Path(a.project).resolve() if a.project else find_project()
    brand = load_brand(project)
    if not (project / "identity" / "generated" / "variations").exists():
        raise SystemExit("Run scripts/build_identity.py first.")

    if a.compare:
        made = []
        for r in rank(brand):
            if r["eligible"]:
                made.append(build(project, brand, r["layout"],
                                  project / "pages" / "compare" / f"board-{r['layout']}.html", native=True))
        print(f"✓ {len(made)} layout(s) → {project / 'pages' / 'compare'}")
        print("  next: python3 scripts/render.py --compare")
        return 0

    layout, why = resolve_layout(brand, a.layout)
    out = build(project, brand, layout, project / "pages" / "board.html")
    print(f"✓ {out}  layout={layout}  ref=assets/{LAYOUTS[layout]['ref']}")
    print(f"  por quê: {why}")
    print("  next: python3 scripts/render.py --board --pieces")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
