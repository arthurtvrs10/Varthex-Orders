#!/usr/bin/env python3
"""Rasterize the project's HTML pages and SVGs with headless Chromium.

    python3 scripts/render.py --board          # brand board @1x and @2x
    python3 scripts/render.py --pieces         # every board panel as its own PNG
    python3 scripts/render.py --compare        # every layout from build_board.py --compare
    python3 scripts/render.py --mockups        # every collateral page
    python3 scripts/render.py --tests          # the critique render tests
    python3 scripts/render.py --svg identity/generated/lockup-horizontal.svg --widths 512,1024
    python3 scripts/render.py --all
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from lib.brandlib import ensure, find_project, load_brand, read_svg  # noqa: E402

try:
    from playwright.sync_api import sync_playwright
except ImportError:  # pragma: no cover
    raise SystemExit("playwright is required:  pip install playwright --break-system-packages")


class Renderer:
    def __init__(self, scale_default: float = 1.0):
        self._pw = None
        self._browser = None
        self.scale_default = scale_default

    def __enter__(self):
        self._pw = sync_playwright().start()
        self._browser = self._pw.chromium.launch(channel="msedge", args=["--force-color-profile=srgb"])
        return self

    def __exit__(self, *_):
        if self._browser:
            self._browser.close()
        if self._pw:
            self._pw.stop()

    # ----------------------------------------------------------------- pages
    def page_png(self, html_path: Path, out: Path, scale: float = 1.0,
                 width: int = 1600, transparent: bool = False) -> Path:
        ensure(out.parent)
        ctx = self._browser.new_context(
            viewport={"width": width, "height": 1000}, device_scale_factor=scale
        )
        pg = ctx.new_page()
        pg.goto(html_path.resolve().as_uri(), wait_until="load")
        pg.wait_for_timeout(350)          # let webfonts settle
        try:
            pg.evaluate("document.fonts && document.fonts.ready")
        except Exception:
            pass
        pg.screenshot(path=str(out), full_page=True, omit_background=transparent)
        ctx.close()
        return out

    def panels_png(self, html_path: Path, out_dir: Path, scale: float = 2.0,
                   width: int = 1600, selector: str = "[data-panel]") -> list[Path]:
        """One PNG per element carrying `data-panel="name"`."""
        ensure(out_dir)
        ctx = self._browser.new_context(
            viewport={"width": width, "height": 1200}, device_scale_factor=scale
        )
        pg = ctx.new_page()
        pg.goto(html_path.resolve().as_uri(), wait_until="load")
        pg.wait_for_timeout(350)
        made = []
        for el in pg.query_selector_all(selector):
            name = el.get_attribute("data-panel") or "panel"
            dest = out_dir / f"{name}.png"
            el.screenshot(path=str(dest))
            made.append(dest)
        ctx.close()
        return made

    # ------------------------------------------------------------------ svg
    def svg_png(self, svg_path: Path, out: Path, width: int,
                background: str | None = None) -> Path:
        markup = read_svg(svg_path)
        bg = background or "transparent"
        html = (
            f'<!doctype html><meta charset="utf-8">'
            f'<style>html,body{{margin:0;background:{bg}}}'
            f'svg{{display:block;width:{width}px;height:auto}}</style>{markup}'
        )
        ensure(out.parent)
        ctx = self._browser.new_context(
            viewport={"width": width, "height": 100}, device_scale_factor=1
        )
        pg = ctx.new_page()
        pg.set_content(html, wait_until="load")
        el = pg.query_selector("svg")
        el.screenshot(path=str(out), omit_background=background is None)
        ctx.close()
        return out

    def pdf(self, html_path: Path, out: Path, fmt: str = "A4",
            landscape: bool = False, margin: str = "0") -> Path:
        ensure(out.parent)
        ctx = self._browser.new_context()
        pg = ctx.new_page()
        pg.goto(html_path.resolve().as_uri(), wait_until="load")
        pg.wait_for_timeout(350)
        pg.pdf(path=str(out), format=fmt, landscape=landscape, print_background=True,
               margin={"top": margin, "right": margin, "bottom": margin, "left": margin})
        ctx.close()
        return out


# --------------------------------------------------------------------------
# critique render tests (references/10)
# --------------------------------------------------------------------------

TEST_CSS = """
body{margin:0;background:#fff;font:12px/1.4 system-ui,sans-serif;color:#111}
svg{display:block;width:100%;height:auto}
section{padding:28px 32px;border-bottom:1px solid #e6e6e6}
h2{font-size:12px;letter-spacing:.08em;text-transform:uppercase;color:#888;margin:0 0 16px}
.row{display:flex;align-items:flex-end;gap:32px;flex-wrap:wrap}
.cell{text-align:center}
.cell small{display:block;margin-top:8px;color:#999;font-size:10px}
.pix{image-rendering:pixelated}
.dark{background:#0D0D0D;padding:24px;border-radius:8px}
.blur{filter:blur(6px)}
.squint{filter:grayscale(1) blur(8px);opacity:.9}
.thresh{filter:grayscale(1) contrast(1000%)}
"""


def build_tests_page(project: Path, brand: dict) -> Path:
    gen = project / "identity" / "generated"
    sym = read_svg(gen / "variations" / "symbol-primary.svg")
    sym_w = read_svg(gen / "variations" / "symbol-white.svg")
    lock = read_svg(gen / "variations" / "horizontal-primary.svg")
    lock_w = read_svg(gen / "variations" / "horizontal-white.svg")

    def box(markup, px, label, cls="", dark=False):
        wrap = f'<div class="{"dark" if dark else ""}" style="display:inline-block">' \
               f'<div class="{cls}" style="width:{px}px">{markup}</div></div>'
        return f'<div class="cell">{wrap}<small>{label}</small></div>'

    def scaled(markup, px, factor, label):
        return (
            f'<div class="cell"><div style="width:{px}px;transform:scale({factor});'
            f'transform-origin:top left;height:{px * factor}px" class="pix">{markup}</div>'
            f'<small>{label}</small></div>'
        )

    s = []
    s.append("<section><h2>16px test — counters must stay open</h2><div class='row'>"
             + "".join(scaled(sym, px, 6, f"{px}px ×6") for px in (16, 24, 32))
             + "</div></section>")
    s.append("<section><h2>1-inch test — 25mm at 300dpi</h2><div class='row'>"
             + box(lock, 295, "25mm wide (295px @300dpi)") + "</div></section>")
    s.append("<section><h2>Single color</h2><div class='row'>"
             + box(sym, 120, "100% K on white")
             + box(sym_w, 120, "white on ink", dark=True) + "</div></section>")
    s.append("<section><h2>Reverse — check for stroke gain</h2><div class='row'>"
             + box(lock_w, 380, "reverse lockup", dark=True) + "</div></section>")
    s.append("<section><h2>Blur — silhouette recognition</h2><div class='row'>"
             + box(sym, 120, "6px gaussian", cls="blur") + "</div></section>")
    s.append("<section><h2>Mono / fax threshold</h2><div class='row'>"
             + box(sym, 120, "1-bit", cls="thresh") + "</div></section>")
    s.append("<section><h2>Rotation — look for a second reading</h2><div class='row'>"
             + "".join(
                 f'<div class="cell"><div style="width:110px;transform:rotate({a}deg)">{sym}</div>'
                 f'<small>{a}°</small></div>' for a in (90, 180, 270))
             + "</div></section>")
    s.append("<section><h2>Squint — value pattern</h2><div class='row'>"
             + box(lock, 200, "grayscale + blur", cls="squint") + "</div></section>")

    html = (f'<!doctype html><meta charset="utf-8"><title>Render tests — '
            f'{brand["brand"]["name"]}</title><style>{TEST_CSS}</style>' + "".join(s))
    out = project / "pages" / "tests.html"
    ensure(out.parent)
    out.write_text(html, encoding="utf-8")
    return out


# --------------------------------------------------------------------------


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--project", default=None)
    ap.add_argument("--board", action="store_true")
    ap.add_argument("--pieces", action="store_true")
    ap.add_argument("--mockups", action="store_true")
    ap.add_argument("--compare", action="store_true",
                    help="render pages/compare/board-*.html (build_board.py --compare)")
    ap.add_argument("--tests", action="store_true")
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--svg", help="render a single SVG to PNG")
    ap.add_argument("--widths", default="512,1024,2048")
    ap.add_argument("--out", default=None)
    a = ap.parse_args()

    project = Path(a.project).resolve() if a.project else find_project()
    brand = load_brand(project)
    out = project / "out"

    if not any([a.board, a.pieces, a.mockups, a.tests, a.svg, a.all, a.compare]):
        a.all = True

    with Renderer() as r:
        if a.svg:
            src = Path(a.svg)
            if not src.is_absolute():
                src = project / src
            for w in [int(x) for x in a.widths.split(",")]:
                dest = Path(a.out) if a.out else out / "svg" / f"{src.stem}-{w}.png"
                if a.out and len(a.widths.split(",")) > 1:
                    dest = Path(a.out) / f"{src.stem}-{w}.png"
                print("→", r.svg_png(src, dest, w))
            return 0

        board = project / "pages" / "board.html"

        if a.board or a.all:
            if not board.exists():
                print("! pages/board.html missing — run build_board.py first")
            else:
                for scale in (1, 2):
                    p = r.page_png(board, out / "board" / f"brand-board@{scale}x.png",
                                   scale=scale, width=1600)
                    print("→", p)

        if a.pieces or a.all:
            if board.exists():
                made = r.panels_png(board, out / "pieces", scale=2)
                print(f"→ {len(made)} panel(s) → {out / 'pieces'}")

        if a.compare:
            pages = sorted((project / "pages" / "compare").glob("board-*.html"))
            if not pages:
                print("  (nothing to compare — run build_board.py --compare)")
            for p in pages:
                dest = out / "board" / "compare" / f"{p.stem}.png"
                r.page_png(p, dest, scale=1, width=1600)
                print("→", dest)

        if a.mockups or a.all:
            pages = sorted((project / "pages" / "mockups").glob("*.html"))
            if not pages:
                print("  (no mockup pages yet — Phase 2 runs build_mockups.py)")
            for p in pages:
                for scale in (1, 2):
                    dest = out / "mockups" / f"{p.stem}@{scale}x.png"
                    r.page_png(p, dest, scale=scale, width=1600)
                print("→", out / "mockups" / f"{p.stem}@1x.png")

        if a.tests or a.all:
            page = build_tests_page(project, brand)
            p = r.page_png(page, out / "tests" / "render-tests.png", scale=2, width=1200)
            print("→", p)
            print("  Inspect it. Every failure is a redraw, not a resize.")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
