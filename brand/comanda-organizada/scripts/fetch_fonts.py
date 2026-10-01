#!/usr/bin/env python3
"""Fetch the project's typefaces from the npm registry (@fontsource/*).

Google Fonts is not reachable from the render container; the npm registry is.
Every family on the vetted list in references/04-typography.md is published
there as `@fontsource/<id>` with real .woff2 (for the browser) and .woff
(readable by fontTools without brotli, for outlining).

    python3 scripts/fetch_fonts.py                    # families from brand.json
    python3 scripts/fetch_fonts.py --family inter --weights 400,600,700
    python3 scripts/fetch_fonts.py --list             # what is already local
"""
from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
import tarfile
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from lib.brandlib import find_project, load_brand  # noqa: E402

SUBSETS = ("latin", "latin-ext")


def npm_pack(pkg: str, dest: Path) -> Path | None:
    dest.mkdir(parents=True, exist_ok=True)
    try:
        subprocess.run(
            [shutil.which("npm.cmd") or shutil.which("npm") or "npm", "pack", pkg, "--silent"],
            cwd=dest, check=True, capture_output=True, timeout=180,
        )
    except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as e:
        err = getattr(e, "stderr", b"") or b""
        print(f"  ! npm pack {pkg} failed: {err.decode(errors='replace')[:200]}")
        return None
    tgz = sorted(dest.glob("*.tgz"), key=lambda p: p.stat().st_mtime)
    return tgz[-1] if tgz else None


def fetch(family: str, weights: list[int], out: Path, italic: bool = False) -> int:
    print(f"→ {family} {weights}")
    out.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        tmpd = Path(tmp)
        tgz = npm_pack(f"@fontsource/{family}", tmpd)
        if not tgz:
            return 0
        wanted = set()
        for w in weights:
            for sub in SUBSETS:
                wanted.add(f"{family}-{sub}-{w}-normal")
                if italic:
                    wanted.add(f"{family}-{sub}-{w}-italic")
        n = 0
        with tarfile.open(tgz) as tf:
            for m in tf.getmembers():
                p = Path(m.name)
                if p.suffix not in (".woff", ".woff2"):
                    if p.name in ("LICENSE", "LICENSE.md"):
                        src = tf.extractfile(m)
                        if src:
                            (out / f"{family}-LICENSE.txt").write_bytes(src.read())
                    continue
                if p.stem not in wanted:
                    continue
                src = tf.extractfile(m)
                if src:
                    (out / p.name).write_bytes(src.read())
                    n += 1
        print(f"  ✓ {n} files")
        return n


def families_from_brand(brand: dict) -> list[tuple[str, list[int], bool]]:
    out = []
    for key, spec in (brand.get("typography") or {}).items():
        if not isinstance(spec, dict) or "fontsource" not in spec:
            continue
        weights = [int(w) for w in spec.get("weights", [400, 700])]
        out.append((spec["fontsource"], weights, bool(spec.get("italic"))))
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--project", default=None)
    ap.add_argument("--family", help="fontsource id, e.g. poppins")
    ap.add_argument("--weights", default="400,500,600,700")
    ap.add_argument("--italic", action="store_true")
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--clean", action="store_true")
    a = ap.parse_args()

    project = Path(a.project).resolve() if a.project else find_project()
    fonts = project / "fonts"

    if a.list:
        files = sorted(fonts.glob("*")) if fonts.exists() else []
        print(f"{len(files)} file(s) in {fonts}")
        for f in files:
            print("  ", f.name)
        return 0

    if a.clean and fonts.exists():
        shutil.rmtree(fonts)

    if not shutil.which("npm"):
        print("npm not found — cannot fetch fonts. Use a font already on the system "
              "and set typography.*.system_fallback in brand.json.")
        return 1

    if a.family:
        jobs = [(a.family, [int(w) for w in a.weights.split(",")], a.italic)]
    else:
        jobs = families_from_brand(load_brand(project))
        if not jobs:
            print("No typography.*.fontsource entries in brand.json.")
            return 1

    total = sum(fetch(f, w, fonts, it) for f, w, it in jobs)
    print(f"\n{total} font file(s) → {fonts}")
    return 0 if total else 1


if __name__ == "__main__":
    raise SystemExit(main())
