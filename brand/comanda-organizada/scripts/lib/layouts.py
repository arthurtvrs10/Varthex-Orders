"""The six board layouts — one per reference in assets/ — and the logic that
picks one from the brief.

Why this exists: with a single default, every identity came out on the same
board. Each reference now has its own composition in build_board.py, and the
choice is scored from brand.json instead of defaulting. See
references/07-brand-board-spec.md.

    from lib.layouts import LAYOUTS, rank
    for row in rank(brand): print(row["layout"], row["score"], row["reasons"])
"""
from __future__ import annotations

import colorsys
import hashlib
import re
import unicodedata

# --------------------------------------------------------------------------
# registry
# --------------------------------------------------------------------------
# signals: word stems (accent-free, lowercase) matched at a word start in the
# brief text, English and Portuguese. Weight = how strongly the word argues
# for that layout. Keep stems >= 4 chars so they don't hit inside other words.

LAYOUTS: dict[str, dict] = {
    "studio": {
        "ref": "reference-01-dark-studio-zcio.png",
        "label": "Dark studio — grade canônica",
        "mode": "dark",
        "needs_hue": False,
        "teaches": "Grade de 12 colunas com todos os painéis do sistema. Sóbrio, técnico.",
        "signals": {
            "quiet": 2, "silenc": 2, "discret": 2, "sobri": 2, "engineer": 2, "engenh": 2,
            "precis": 2, "premium": 2, "minimal": 2, "technic": 2, "tecnic": 2,
            "serious": 1, "serio": 1, "cool": 1, "fria": 1, "frio": 1,
            "develop": 1, "desenvolv": 1, "infra": 2, "b2b": 1, "corporat": 1,
            "consult": 1, "seguranc": 1, "security": 1, "observab": 1,
        },
    },
    "mosaic": {
        "ref": "reference-02-dark-studio-nexvora.jpg",
        "label": "Dark mosaic — grade irregular",
        "mode": "dark",
        "needs_hue": False,
        "teaches": "Painéis contornados e preenchidos alternados; wordmark vertical; 'Aa' gigante.",
        "signals": {
            "contempor": 2, "modern": 2, "digital": 2, "tech": 1, "tecnolog": 1,
            "startup": 2, "product": 1, "produto": 1, "software": 1, "saas": 2,
            "bold": 1, "ousad": 1, "young": 1, "jovem": 1, "futur": 2,
            "innov": 1, "inova": 1, "aplicativ": 1, "mobile": 1,
        },
    },
    "flood": {
        "ref": "reference-03-color-flood-forge.jpg",
        "label": "Color flood — a cor é a página",
        "mode": "flood",
        "needs_hue": True,
        "teaches": "Painéis na cor da marca, alternando com branco; paleta em blocos gigantes.",
        "signals": {
            "confident": 2, "confian": 2, "loud": 2, "vibrant": 2, "vibra": 2,
            "playful": 2, "divert": 2, "ludic": 2, "accessib": 1, "acessiv": 1,
            "consumer": 1, "consumid": 1, "varejo": 1, "retail": 1, "friendly": 1,
            "amigav": 1, "colorid": 2, "colorful": 2, "alegr": 2, "optimis": 1, "otimis": 1,
        },
    },
    "flood-wide": {
        "ref": "reference-04-color-flood-bagdoom.jpg",
        "label": "Color flood horizontal — com painéis de estratégia",
        "mode": "flood",
        "needs_hue": True,
        "teaches": "Painéis largos, palavras-chave, contato com ícones, frase de efeito.",
        "signals": {
            "energ": 2, "strong": 2, "forte": 2, "forca": 2, "sport": 2, "esport": 2,
            "fitness": 2, "academ": 2, "athlet": 2, "atlet": 2, "motiv": 2,
            "community": 1, "comunidad": 1, "local": 1, "barbear": 2, "barber": 2,
            "servic": 1, "service": 1, "dinamic": 1, "dynamic": 1, "power": 1, "potenc": 1,
        },
    },
    "editorial": {
        "ref": "reference-05-editorial-bands-kyron.jpg",
        "label": "Editorial — faixas de largura total",
        "mode": "dark",
        "needs_hue": False,
        "teaches": "Sem cards; faixas com rótulos '/ Conceito'; o conceito escrito no próprio board.",
        "signals": {
            "story": 2, "histori": 2, "manifest": 2, "statement": 2, "heritage": 2,
            "tradic": 2, "tradition": 2, "classic": 1, "classico": 1, "editorial": 2,
            "fashion": 2, "moda": 2, "cultur": 2, "arte": 1, "music": 2, "musica": 2,
            "rebel": 2, "rebeld": 2, "impact": 1, "attitud": 2, "atitud": 2,
            "autoral": 2, "authentic": 1, "autentic": 1,
        },
    },
    "gallery": {
        "ref": "reference-06-light-gallery-novamind.jpg",
        "label": "Light gallery — aplicações em primeiro plano",
        "mode": "light",
        "needs_hue": False,
        "teaches": "Página clara, só aplicações; a documentação fica no manual.",
        "signals": {
            "calm": 2, "calma": 2, "calmo": 2, "human": 2, "acolhed": 2, "welcom": 2,
            "warm": 2, "quente": 1, "suave": 2, "gentle": 2, "leve": 1, "light": 1,
            "care": 2, "cuidad": 2, "saude": 2, "health": 2, "wellness": 2, "bem-est": 2,
            "educa": 1, "famil": 1, "natural": 1, "organic": 1, "organ": 1,
            "hospital": 1, "clinic": 2, "juridic": 1, "legal": 1, "advoca": 1,
        },
    },
}

TIE_WINDOW = 1.5      # candidates this close to the top count as a tie
HUE_SATURATION = 0.30  # HSV saturation above which a colour counts as a real hue


# --------------------------------------------------------------------------
# helpers
# --------------------------------------------------------------------------


def _norm(text: str) -> str:
    text = unicodedata.normalize("NFKD", text or "")
    return "".join(ch for ch in text if not unicodedata.combining(ch)).lower()


def _hits(text: str, signals: dict[str, int]) -> list[tuple[str, int]]:
    found = []
    for stem, w in signals.items():
        if re.search(r"\b" + re.escape(stem), text):
            found.append((stem, w))
    return found


def brand_hue(brand: dict) -> str | None:
    """The saturated colour the flood layouts paint with, or None."""
    roles = {c.get("role"): c.get("hex") for c in brand.get("colors", []) if c.get("hex")}
    for role in ("brand", "accent"):
        h = roles.get(role)
        if h and saturation(h) >= HUE_SATURATION:
            return h
    return None


def saturation(hex_: str) -> float:
    h = hex_.lstrip("#")
    if len(h) == 3:
        h = "".join(c * 2 for c in h)
    r, g, b = (int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))
    _, s, v = colorsys.rgb_to_hsv(r, g, b)
    return s if v > 0.18 else 0.0


def brief_text(brand: dict) -> str:
    b = brand.get("brief", {})
    parts = [
        " ".join(b.get("character", []) or []),
        b.get("owns_the_word", ""), b.get("audience", ""), b.get("business", ""),
        b.get("lives_mostly_on", ""),
        " ".join(((brand.get("brand", {}).get("voice") or {}).get("adjectives") or [])),
    ]
    return _norm(" ".join(p for p in parts if p))


# --------------------------------------------------------------------------
# scoring
# --------------------------------------------------------------------------


def score_layout(name: str, brand: dict) -> dict:
    meta = LAYOUTS[name]
    board = brand.get("board", {})
    b, concept, core = brand.get("brief", {}), brand.get("concept", {}), brand.get("brand", {})
    text = brief_text(brand)
    hue = brand_hue(brand)
    reasons: list[str] = []
    score = 0.0

    # hard constraints ------------------------------------------------------
    if meta["needs_hue"] and not hue:
        return {"layout": name, "score": None, "eligible": False,
                "reasons": ["precisa de uma cor 'brand' ou 'accent' saturada na paleta"]}
    if name in (board.get("avoid") or []):
        return {"layout": name, "score": None, "eligible": False,
                "reasons": ["listado em board.avoid"]}

    # the client pointed at this reference ------------------------------------
    if name in (board.get("prefer") or []):
        score += 4
        reasons.append("+4 cliente prefere este estilo (board.prefer)")

    # words in the brief -------------------------------------------------------
    for stem, w in _hits(text, meta["signals"]):
        score += w
        reasons.append(f"+{w} briefing menciona '{stem}…'")

    # palette -----------------------------------------------------------------
    if hue:
        bonus = {"flood": 3, "flood-wide": 2, "editorial": 2, "mosaic": 1,
                 "gallery": 1, "studio": -1}[name]
        if bonus:
            score += bonus
            reasons.append(f"{bonus:+d} paleta tem cor forte ({hue})")
    else:
        bonus = {"studio": 3, "mosaic": 1, "editorial": 0, "gallery": 1}.get(name, 0)
        if bonus:
            score += bonus
            reasons.append(f"+{bonus} paleta monocromática")

    # what the brand has to say ------------------------------------------------
    if concept.get("sentence") and name == "editorial":
        add = 2 if concept.get("attributes") else 1
        score += add
        reasons.append(f"+{add} tem frase de conceito" + (" e atributos" if add == 2 else ""))
    if core.get("keywords") and name == "flood-wide":
        score += 2
        reasons.append("+2 tem palavras-chave (painel de keywords)")
    contact = core.get("contact") or {}
    if sum(1 for k in ("email", "site", "phone", "handle") if contact.get(k)) >= 3 \
            and name == "flood-wide":
        score += 1
        reasons.append("+1 contato completo (painel de contato)")
    if len(core.get("story", "")) > 280 and name == "editorial":
        score += 1
        reasons.append("+1 história longa — pede faixa editorial")

    # the mark ----------------------------------------------------------------
    relation = concept.get("name_relation") or b.get("name_relation")
    if relation == "wordmark-only" and name in ("mosaic", "flood"):
        score += 2
        reasons.append("+2 marca só com wordmark — painéis grandes de nome")

    # where it lives ------------------------------------------------------------
    lives = _norm(b.get("lives_mostly_on", ""))
    if re.search(r"\b(app|aplicativ|site|web|digital|mobile|dashboard|saas)", lives):
        if name in ("mosaic", "flood"):
            score += 1
            reasons.append("+1 vive no digital")
    if re.search(r"\b(papel|print|impress|contrat|document|cartao|card|papelar|stationer)", lives):
        if name in ("gallery", "editorial"):
            score += 2 if name == "gallery" else 1
            reasons.append(f"+{2 if name == 'gallery' else 1} vive em impressos/documentos")

    return {"layout": name, "score": round(score, 2), "eligible": True, "reasons": reasons}


def rank(brand: dict) -> list[dict]:
    """Every layout, best first. Ineligible ones last, with the reason."""
    rows = [score_layout(n, brand) for n in LAYOUTS]
    ok = sorted((r for r in rows if r["eligible"]), key=lambda r: -r["score"])
    return ok + [r for r in rows if not r["eligible"]]


def choose(brand: dict) -> tuple[str, str]:
    """Pick one layout and say why.

    Ties inside TIE_WINDOW are broken by a stable hash of the brand name, so two
    brands with similar briefs do not land on the same board — the variety is
    deliberate but reproducible (same brand → same pick).
    """
    rows = [r for r in rank(brand) if r["eligible"]]
    if not rows:
        return "studio", "nenhum layout elegível — usando studio"
    top = rows[0]["score"]
    tied = [r for r in rows if top - r["score"] <= TIE_WINDOW]
    if len(tied) == 1:
        r = tied[0]
        return r["layout"], "; ".join(r["reasons"][:4]) or "maior pontuação"
    seed = int(hashlib.md5(_norm(brand.get("brand", {}).get("name", "")).encode()).hexdigest(), 16)
    r = tied[seed % len(tied)]
    names = ", ".join(x["layout"] for x in tied)
    return r["layout"], (f"empate técnico entre {names} — desempate por variedade "
                         f"(hash do nome); " + "; ".join(r["reasons"][:3]))
