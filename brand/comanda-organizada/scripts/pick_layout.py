#!/usr/bin/env python3
"""Choose the brand-board layout from the brief — never by default.

Scores the six reference layouts in assets/ against brand.json (adjectives,
audience, where the brand lives, palette, concept, keywords, contact) and
prints the ranking with the reason for every point. Ties are broken by a stable
hash of the brand name, so similar briefs still get different boards.

    python3 scripts/pick_layout.py                  # ranking + recommendation
    python3 scripts/pick_layout.py --write          # save the recommendation
    python3 scripts/pick_layout.py --write --layout editorial   # the user's pick
    python3 scripts/pick_layout.py --list           # the six layouts and their references
    python3 scripts/pick_layout.py --json

Writes brand.json -> board.layout and board.layout_reason. It removes
board.mode (the colour treatment then follows the layout) unless --keep-mode.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from lib.brandlib import find_project, load_brand  # noqa: E402
from lib.layouts import LAYOUTS, brand_hue, choose, rank  # noqa: E402

ASSETS = Path(__file__).parent.parent / "assets"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--project", default=None)
    ap.add_argument("--write", action="store_true", help="save the choice into brand.json")
    ap.add_argument("--layout", choices=list(LAYOUTS), help="force this layout (the user's pick)")
    ap.add_argument("--keep-mode", action="store_true", help="keep an explicit board.mode")
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--json", action="store_true")
    a = ap.parse_args()

    if a.list:
        for name, m in LAYOUTS.items():
            need = "  (precisa de cor forte)" if m["needs_hue"] else ""
            print(f"{name:<11} {m['label']}{need}\n            assets/{m['ref']}\n"
                  f"            {m['teaches']}\n")
        return 0

    project = Path(a.project).resolve() if a.project else find_project()
    path = project / "brand.json"
    brand = load_brand(project)
    rows = rank(brand)
    pick, why = choose(brand)
    if a.layout:
        pick, why = a.layout, "escolhido pelo usuário"

    if a.json:
        print(json.dumps({"pick": pick, "why": why, "ranking": rows}, ensure_ascii=False, indent=2))
    else:
        hue = brand_hue(brand)
        print(f"Marca: {brand['brand']['name']}   cor forte: {hue or 'nenhuma'}\n")
        for i, r in enumerate(rows, 1):
            m = LAYOUTS[r["layout"]]
            sc = f"{r['score']:>5}" if r["eligible"] else "   —"
            flag = "  ← recomendado" if r["layout"] == pick else ""
            print(f"{i}. {r['layout']:<11}{sc}  {m['label']}{flag}")
            print(f"   ref: assets/{m['ref']}")
            for reason in r["reasons"][:6]:
                print(f"     {reason}")
        print(f"\n→ {pick}: {why}")

    if a.write:
        data = json.loads(path.read_text(encoding="utf-8"))
        board = data.setdefault("board", {})
        board["layout"] = pick
        board["layout_reason"] = why
        if not a.keep_mode:
            board.pop("mode", None)
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"✓ brand.json → board.layout = {pick!r}")
        print("  next: python3 scripts/build_board.py")
    elif not a.json:
        print("  (nada salvo — rode com --write, ou --write --layout <nome> para a escolha do usuário)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
