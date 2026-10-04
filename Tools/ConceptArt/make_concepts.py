#!/usr/bin/env python3
"""Generates the schematic top-down concept plans in Docs/concepts/*.svg for Hole Design Sprint 001, pass 2.
Design illustrations only (not to scale, not game assets). Run: python3 Tools/ConceptArt/make_concepts.py"""
import math, os
from xml.sax.saxutils import escape
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Docs", "concepts")
W, H = 900, 560
C = dict(bg="#f3efe4", grass="#86c46f", grass2="#6fae5c", sand="#ecd9a6", water="#62b5dc", deep="#3f8fbf", lava="#ec5a2a", lavad="#b83a1a",
         stone="#a49d8e", stone2="#7f796c", wood="#92602f", wood2="#6d4521", ink="#2b2b2b", safe="#1f9d55", bold="#f08a00", wild="#c2189b",
         rock="#6f6a60", gold="#f2c14e", glass="#bfe9f5", dark="#3a3330")

class S:
    def __init__(s, title, sub):
        s.e = []; s.title = title; s.sub = sub
    def add(s, x): s.e.append(x); return s
    def poly(s, pts, fill, stroke="none", sw=2, op=1, dash=None):
        d = " ".join(f"{x:.1f},{y:.1f}" for x, y in pts)
        return s.add(f'<polygon points="{d}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}" opacity="{op}"' + (f' stroke-dasharray="{dash}"' if dash else '') + '/>')
    def rect(s, x, y, w, h, fill, stroke="none", sw=2, rx=0, op=1):
        return s.add(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{rx}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}" opacity="{op}"/>')
    def circ(s, x, y, r, fill, stroke="none", sw=2, op=1):
        return s.add(f'<circle cx="{x}" cy="{y}" r="{r}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}" opacity="{op}"/>')
    def ell(s, x, y, rx, ry, fill, stroke="none", sw=2, op=1):
        return s.add(f'<ellipse cx="{x}" cy="{y}" rx="{rx}" ry="{ry}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}" opacity="{op}"/>')
    def path(s, d, fill="none", stroke=C["ink"], sw=3, dash=None, op=1, marker=None):
        return s.add(f'<path d="{d}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}" opacity="{op}"' + (f' stroke-dasharray="{dash}"' if dash else '') + (f' marker-end="url(#{marker})"' if marker else '') + ' stroke-linecap="round" stroke-linejoin="round"/>')
    def text(s, x, y, t, size=15, fill=C["ink"], anchor="middle", weight="normal", italic=False):
        return s.add(f'<text x="{x}" y="{y}" font-family="Helvetica,Arial,sans-serif" font-size="{size}" fill="{fill}" text-anchor="{anchor}" font-weight="{weight}"' + (' font-style="italic"' if italic else '') + f'>{escape(t)}</text>')
    def tee(s, x, y): s.circ(x, y, 13, "#fff", C["ink"], 2); s.text(x, y + 5, "T", 15, weight="bold")
    def cup(s, x, y):
        s.circ(x, y, 9, "#111"); s.add(f'<line x1="{x}" y1="{y}" x2="{x}" y2="{y-34}" stroke="#444" stroke-width="2.5"/>'); s.poly([(x, y - 34), (x + 24, y - 27), (x, y - 20)], "#e0262d")
    def route(s, pts, kind, label=None, lx=None, ly=None, curve=False):
        col = {"safe": C["safe"], "bold": C["bold"], "wild": C["wild"]}[kind]; dash = {"safe": None, "bold": "10 7", "wild": "3 7"}[kind]
        d = f"M{pts[0][0]},{pts[0][1]} " + (" ".join(f"L{x},{y}" for x, y in pts[1:]) if not curve else f"Q{pts[1][0]},{pts[1][1]} {pts[2][0]},{pts[2][1]}")
        s.path(d, "none", col, 5, dash, 0.95, "arr" + kind)
        if label: s.text(lx if lx else pts[-1][0], ly if ly else pts[-1][1] - 12, label, 14, col, weight="bold")
    def svg(s, extra=""):
        defs = "".join(f'<marker id="arr{k}" markerUnits="userSpaceOnUse" markerWidth="16" markerHeight="16" refX="12" refY="8" orient="auto"><path d="M0,1 L16,8 L0,15 z" fill="{c}"/></marker>' for k, c in (("safe", C["safe"]), ("bold", C["bold"]), ("wild", C["wild"])))
        head = f'<text x="24" y="34" font-family="Helvetica,Arial,sans-serif" font-size="24" font-weight="bold" fill="{C["ink"]}">{s.title}</text><text x="24" y="56" font-family="Helvetica,Arial,sans-serif" font-size="14" fill="#555">{s.sub}</text>'
        leg = (f'<g font-family="Helvetica,Arial,sans-serif" font-size="13"><line x1="560" y1="538" x2="600" y2="538" stroke="{C["safe"]}" stroke-width="5"/><text x="606" y="543" fill="{C["ink"]}">safe line</text>'
               f'<line x1="672" y1="538" x2="712" y2="538" stroke="{C["bold"]}" stroke-width="5" stroke-dasharray="10 7"/><text x="718" y="543" fill="{C["ink"]}">bold line</text>'
               f'<line x1="786" y1="538" x2="826" y2="538" stroke="{C["wild"]}" stroke-width="5" stroke-dasharray="3 7"/><text x="832" y="543" fill="{C["ink"]}">wild shot</text></g>')
        return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" width="{W}" height="{H}"><defs>{defs}</defs>'
                f'<rect width="{W}" height="{H}" fill="{C["bg"]}"/><rect x="8" y="8" width="{W-16}" height="{H-16}" fill="none" stroke="#cfc8b4" stroke-width="2" rx="10"/>'
                + "".join(s.e) + head + leg + extra + '<text x="24" y="543" font-family="Helvetica,Arial,sans-serif" font-size="11" fill="#888">Schematic concept plan, not to scale. Design illustration only.</text></svg>')

def save(s, name):
    with open(os.path.join(OUT, name), "w") as f: f.write(s.svg())

def arc_pts(cx, cy, r, a0, a1, n=24):
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]

def ring_band(cx, cy, r0, r1, a0, a1, n=28):
    return arc_pts(cx, cy, r1, a0, a1, n) + arc_pts(cx, cy, r0, a1, a0, n)

# ---------------- Hole 1: Castaway's Cove
def h1():
    s = S("Hole 1 · Castaway's Cove (par 2)", "Starting Island · a beached shipwreck curves the lane into a hidden cove")
    s.rect(30, 70, 840, 440, "#cfe9f2", rx=12)
    s.poly([(40, 330), (300, 310), (560, 330), (640, 250), (700, 120), (820, 130), (840, 330), (800, 500), (40, 500)], C["sand"], "#c2ad78", 2)
    s.poly([(70, 372), (560, 372), (640, 300), (672, 150), (770, 150), (760, 330), (620, 440), (70, 440)], C["grass"], "#3f7f3a", 3)
    # shipwreck hull as curved bank on the outside of the bend
    s.path("M560,372 Q690,372 700,260 Q706,190 700,150", "none", C["wood2"], 14)
    s.path("M560,360 Q676,360 686,260 Q692,196 688,150", "none", C["wood"], 6)
    for t in (0.2, 0.4, 0.6, 0.8): s.add(f'<line x1="{560+140*t}" y1="{372-0*t}" x2="{565+135*t}" y2="{410-30*t}" stroke="{C["wood2"]}" stroke-width="5"/>')
    s.text(755, 300, "wrecked hull", 13, C["wood2"], italic=True); s.text(755, 316, "(curved bank)", 13, C["wood2"], italic=True)
    s.ell(120, 400, 28, 18, C["sand"], "#c2ad78", 1); s.text(120, 405, "dune", 12, "#8a7a4a")  # dune bump
    for x, y in ((250, 468), (430, 470)): s.circ(x, y, 7, "#6e8f4e")
    s.tee(100, 405); s.cup(720, 180)
    s.route([(112, 410), (560, 408), (700, 330), (722, 188)], "safe", "two easy putts: aim at the bend", 330, 472)
    s.route([(112, 400), (330, 378), (690, 350), (720, 188)], "bold", None)
    s.route([(112, 394), (600, 372), (696, 262), (721, 190)], "wild", "swoop: ride the hull round the corner", 560, 205)
    s.text(300, 108, "palm cluster & lagoon beyond", 13, "#4f7f93", italic=True)
    save(s, "hole1.svg")

# ---------------- Hole 2: Crabwalk
def h2():
    s = S("Hole 2 · Crabwalk (par 3)", "Starting Island · a giant driftwood crab sits in the lane: go round its claws or through its belly")
    s.rect(30, 70, 840, 440, "#cfe9f2", rx=12)
    s.poly([(40, 120), (860, 120), (860, 500), (40, 500)], C["sand"], "#c2ad78", 2)
    s.poly([(360, 470), (360, 400), (150, 360), (150, 150), (230, 150), (240, 290), (400, 330), (400, 470)], C["grass"], "#3f7f3a", 3)   # wide left lane
    s.poly([(440, 470), (440, 330), (600, 300), (620, 150), (690, 150), (680, 340), (500, 395), (500, 470)], C["grass2"], "#3f7f3a", 3)  # narrow right lane
    s.poly([(230, 150), (690, 150), (690, 195), (230, 195)], C["grass"], "#3f7f3a", 3)   # cup shelf across the top
    # crab body
    s.ell(430, 285, 110, 62, C["wood"], C["wood2"], 4)
    s.path("M335,262 Q250,205 250,262 Q250,305 322,300", C["wood"], C["wood2"], 4); s.path("M525,262 Q610,205 610,262 Q610,305 538,300", C["wood"], C["wood2"], 4)
    for x in (360, 400, 460, 500): s.rect(x - 8, 335, 16, 30, C["wood2"], rx=4)
    s.rect(398, 232, 64, 28, C["bg"], C["wood2"], 3, 6); s.text(430, 251, "belly arch", 11)  # tunnel entrance label
    s.circ(400, 245, 7, "#fff", C["ink"], 2); s.circ(460, 245, 7, "#fff", C["ink"], 2)
    s.rect(398, 360, 64, 70, C["grass"], C["wood2"], 3, 8)   # tunnel lane between legs
    s.rect(398, 195, 64, 40, C["grass"], C["wood2"], 3, 8)
    s.tee(430, 455); s.cup(560, 175)
    s.route([(425, 450), (270, 400), (190, 250), (210, 175), (540, 172)], "safe", "wide route round the claw (3 putts)", 260, 490)
    s.route([(430, 445), (430, 300), (430, 215), (540, 178)], "bold", "through the belly: par 2 chance", 735, 440)
    s.route([(470, 445), (560, 340), (650, 250), (640, 180)], "wild", "narrow right lane: bank off the claw", 700, 330)
    s.text(430, 100, "driftwood crab · shell clatter · a claw that snaps (decor)", 13, "#7a6a3a", italic=True)
    save(s, "hole2.svg")

# ---------------- Hole 3: Gorge Leap
def h3():
    s = S("Hole 3 · Gorge Leap (par 3)", "Jungle Island · a waterfall gorge: cross the rope bridge, or leap it off a fallen-log ramp")
    s.rect(30, 70, 840, 440, "#2f6f3f", rx=12, op=0.35)
    s.poly([(470, 80), (560, 80), (580, 500), (440, 500)], C["water"], "#3f8fbf", 3)  # ravine / river
    for i in range(6): s.path(f"M{480+i*14},{90+i*0} L{470+i*20},{140}", "none", "#fff", 3, op=0.8)
    s.text(515, 110, "waterfall", 13, "#fff", italic=True)
    s.poly([(80, 470), (80, 380), (260, 380), (260, 250), (460, 250), (460, 280), (260, 280), (260, 440), (330, 470)], C["grass"], "#3f7f3a", 3)
    s.rect(440, 255, 150, 28, C["wood"], C["wood2"], 3)   # bridge
    for x in range(446, 590, 18): s.add(f'<line x1="{x}" y1="255" x2="{x}" y2="283" stroke="{C["wood2"]}" stroke-width="2"/>')
    s.poly([(590, 260), (690, 260), (690, 140), (790, 140), (790, 230), (760, 300), (590, 300)], C["grass"], "#3f7f3a", 3)   # far side + cup
    s.poly([(560, 150), (610, 150), (610, 200), (560, 200)], C["wood"], C["wood2"], 3)  # fallen log ramp (launch)
    s.text(585, 140, "log ramp", 12, C["wood2"], italic=True)
    s.poly([(440, 460), (590, 460), (590, 500), (440, 500)], C["sand"], "#c2ad78", 2); s.text(515, 485, "riverbank (recovery)", 12)
    s.path("M470,400 Q515,430 515,462", "none", "#fff", 3, "5 5", marker=None); s.text(380, 425, "flume carries a fallen ball down", 12, "#fff", italic=True)
    s.poly([(260, 280), (310, 280), (330, 330), (260, 340)], C["grass2"], "#3f7f3a", 3); s.text(285, 313, "guide", 11)
    s.tee(110, 440); s.cup(740, 185)
    s.route([(120, 435), (230, 400), (300, 300), (515, 270), (640, 280), (730, 200)], "safe", "bridge with banked guide", 330, 232)
    s.route([(140, 420), (230, 190), (585, 175), (680, 170), (730, 188)], "wild", "log-ramp leap over the lip", 420, 180)
    s.text(150, 120, "hanging vines · mist · glow-moths", 13, "#fff", italic=True)
    save(s, "hole3.svg")

# ---------------- Hole 4: Sinkhole Spiral
def h4():
    s = S("Hole 4 · Sinkhole Spiral (par 4 provisional; reimagined Hollow Drop)", "Jungle Island · a glowing cenote: spiral down the rim, cross the basin, climb to the altar")
    s.rect(30, 70, 840, 440, "#2f6f3f", rx=12, op=0.35)
    cx, cy = 400, 300
    s.circ(cx, cy, 215, C["stone"], C["stone2"], 4); s.circ(cx, cy, 160, C["deep"], "#2b6f99", 3); s.circ(cx, cy, 115, C["water"], "none")
    s.poly(ring_band(cx, cy, 160, 205, 200, 470), C["grass"], "#3f7f3a", 3)   # spiral ledge (3/4 turn)
    s.poly(ring_band(cx, cy, 120, 160, 70, 200), C["sand"], "#c2ad78", 3)   # basin floor ring section
    s.circ(cx, cy, 32, C["stone2"], "#555", 3); s.text(cx, cy + 4, "pillar", 11, "#fff")
    s.rect(560, 380, 130, 60, C["gold"], "#a07a1a", 3, 6); s.text(625, 415, "sun altar (cup)", 12)
    for a in (240, 300, 360): x, y = cx + 215 * math.cos(math.radians(a)), cy + 215 * math.sin(math.radians(a)); s.path(f"M{x},{y} L{x+(x-cx)*0.12},{y+(y-cy)*0.12}", "none", "#fff", 6, op=0.8)
    s.text(cx, 95, "three rim waterfalls feed the glowing pool", 13, "#fff", italic=True)
    s.tee(222, 262); s.cup(640, 395)
    s.route([(240, 270), (230, 350), (290, 470), (480, 505), (590, 420), (630, 400)], "safe", "follow the spiral ledge", 150, 500)
    s.route([(250, 270), (235, 360), (300, 460), (470, 495), (610, 410), (640, 398)], "bold", None)
    s.route([(244, 262), (180, 330), (260, 480), (460, 508), (640, 440), (642, 402)], "wild", "ride the curved wall all the way down", 740, 470)
    save(s, "hole4.svg")

# ---------------- Hole 5: Sun Stair
def h5():
    s = S("Hole 5 · The Sun Stair (par 3)", "Temple Island · a ziggurat of three terraces; a canal runs down the middle; one daring jump")
    s.rect(30, 70, 840, 440, "#d9c58e", rx=12, op=0.5)
    s.poly([(120, 500), (120, 400), (780, 400), (780, 500)], C["stone"], C["stone2"], 3); s.text(450, 480, "terrace 1 (tee)", 12, "#fff")
    s.poly([(190, 400), (190, 300), (710, 300), (710, 400)], "#b8b0a0", C["stone2"], 3); s.text(450, 385, "terrace 2", 12)
    s.poly([(320, 300), (320, 200), (580, 200), (580, 300)], C["gold"], "#a07a1a", 3); s.text(450, 282, "terrace 3", 12)
    s.circ(450, 215, 60, "#f7e08a", "#a07a1a", 3); s.cup(450, 218); s.text(450, 168, "sun disc", 12)
    s.rect(425, 215, 50, 285, C["water"], "#3f8fbf", 2, op=0.85); s.text(450, 340, "canal", 12, "#fff")   # canal through the stair
    s.poly([(130, 390), (190, 390), (190, 310), (130, 310)], C["grass"], "#3f7f3a", 3); s.text(160, 355, "ramp", 12)
    s.poly([(710, 390), (770, 390), (770, 310), (710, 310)], C["grass"], "#3f7f3a", 3); s.text(740, 355, "ramp", 12)
    s.poly([(250, 470), (320, 470), (330, 412), (255, 412)], C["wood"], C["wood2"], 3); s.text(290, 445, "launch", 12, "#fff")
    s.poly([(320, 300), (340, 300), (340, 320), (320, 320)], C["wood2"])
    s.tee(150, 470)
    s.route([(160, 462), (160, 330), (250, 330), (330, 255), (440, 232)], "safe", "side ramps and terraces", 120, 290)
    s.route([(165, 466), (290, 455), (320, 330), (400, 245), (448, 225)], "wild", "sun leap: terrace 1 to terrace 3", 470, 120)
    s.text(450, 100, "torches · carved serpents · water pours over the steps", 13, "#7a6a3a", italic=True)
    s.path("M450,500 L450,520", "none", C["water"], 8)
    s.text(450, 520, "fallen balls float back to the start pool", 12, "#3f8fbf", italic=True)
    save(s, "hole5.svg")

# ---------------- Hole 6: Waterwheel Mill
def h6():
    s = S("Hole 6 · The Waterwheel Mill (par 3)", "Temple Island · load the ball into a bucket and let the wheel lift it to the aqueduct")
    s.rect(30, 70, 840, 440, "#d9c58e", rx=12, op=0.5)
    s.poly([(70, 480), (70, 400), (280, 400), (280, 450), (350, 450), (350, 480)], C["grass"], "#3f7f3a", 3)   # tee area
    s.poly([(70, 400), (70, 150), (140, 150), (140, 400)], C["grass"], "#3f7f3a", 3)                          # long safe ramp lane (left)
    s.poly([(70, 150), (70, 100), (500, 100), (500, 150)], C["grass"], "#3f7f3a", 3)                          # top walkway
    s.rect(500, 90, 300, 60, C["stone"], C["stone2"], 3); s.text(640, 108, "aqueduct (upper channel)", 13, "#fff")
    s.rect(780, 90, 90, 70, C["gold"], "#a07a1a", 3, 8); s.cup(825, 135)
    s.rect(280, 300, 280, 150, C["water"], "#3f8fbf", 3, 14); s.text(420, 440, "millpond", 13, "#fff", italic=True)
    s.circ(560, 275, 95, "none", C["wood2"], 10); s.circ(560, 275, 12, C["wood2"])
    for a in range(0, 360, 45): x, y = 560 + 95 * math.cos(math.radians(a)), 275 + 95 * math.sin(math.radians(a)); s.rect(x - 14, y - 10, 28, 20, C["wood"], C["wood2"], 2, 3); s.add(f'<line x1="560" y1="275" x2="{x}" y2="{y}" stroke="{C["wood2"]}" stroke-width="4"/>')
    s.text(560, 280, "WHEEL", 12, "#fff", weight="bold"); s.path("M470,190 A95,95 0 0 1 650,190", "none", C["ink"], 3, marker="arrsafe"); s.text(560, 160, "turns slowly", 12, "#555")
    s.rect(340, 330, 80, 22, C["water"], "#3f8fbf", 2); s.text(380, 346, "millrace", 11, "#fff")
    s.tee(110, 455); s.cup(825, 135)
    s.route([(110, 450), (105, 160), (130, 110), (490, 118), (700, 120), (820, 135)], "safe", "long ramp walk (3 to 4 strokes)", 300, 82)
    s.route([(120, 450), (250, 420), (330, 345), (470, 300)], "bold", None)
    s.route([(470, 300), (600, 190), (740, 125), (815, 135)], "wild", "bucket lift: time it, ride the aqueduct", 750, 240)
    s.text(300, 500, "wooden creak, splashing, mill dust", 12, "#7a6a3a", italic=True)
    save(s, "hole6.svg")

# ---------------- Hole 7: Lava Falls
def h7():
    s = S("Hole 7 · Lava Falls (par 4)", "Volcanic Island · a three-tier lava cascade; skip a tier through the vent tube")
    s.rect(30, 70, 840, 440, "#3a3330", rx=12)
    s.poly([(40, 80), (860, 80), (860, 500), (40, 500)], C["lavad"], op=0.45)
    s.poly([(80, 150), (320, 150), (320, 215), (80, 215)], C["stone"], C["stone2"], 3); s.text(200, 188, "tier 1 (tee)", 13, "#fff")
    s.poly([(330, 280), (580, 280), (580, 345), (330, 345)], C["stone"], C["stone2"], 3); s.text(455, 318, "tier 2", 13, "#fff")
    s.poly([(590, 410), (830, 410), (830, 485), (590, 485)], C["stone"], C["stone2"], 3); s.text(710, 452, "tier 3", 13, "#fff")
    for (x0, y0, x1, y1) in ((320, 150, 330, 280), (580, 280, 590, 410)): s.rect(x0 - 5, y0 + 35, 18, 100, C["lava"], op=0.9)
    s.poly([(300, 200), (350, 215), (350, 285), (300, 270)], C["lava"], C["lavad"], 2, 0.95); s.poly([(570, 330), (610, 340), (610, 415), (570, 405)], C["lava"], C["lavad"], 2, 0.95)
    s.text(380, 230, "lavafall", 12, "#ffd9a0", italic=True)
    s.circ(150, 385, 38, C["dark"], "#111", 4); s.circ(150, 385, 20, C["lava"]); s.text(150, 440, "vent mouth", 12, "#ffd9a0")  # vent tube mouth
    s.path("M185,380 Q350,420 560,440", "none", C["glass"], 12, op=0.7); s.path("M185,380 Q350,420 560,440", "none", "#fff", 3, "4 8")
    s.text(360, 438, "glass tube under the lava", 12, "#bfe9f5", italic=True)
    s.poly([(560, 440), (610, 430), (610, 460), (560, 470)], C["dark"], "#111", 3)
    s.circ(700, 450, 20, C["gold"], "#a07a1a", 3); s.cup(700, 455)
    s.poly([(120, 225), (150, 225), (150, 345), (120, 345)], C["stone"], C["stone2"], 2)
    s.tee(110, 183)
    s.route([(120, 183), (305, 183), (420, 190), (440, 300), (570, 320), (640, 440), (700, 455)], "safe", "tiers 1, 2, 3 (4 strokes)", 330, 100)
    s.route([(120, 186), (150, 330), (150, 385)], "bold", "enter the vent: tube carries you under the lava", 300, 490)
    s.route([(300, 180), (400, 170), (500, 215), (520, 300)], "wild", "jump the lavafall (tier 1 to tier 2)", 560, 175)
    s.text(640, 112, "geysers · glowing cracks · ash drift", 13, "#ffd9a0", italic=True)
    save(s, "hole7.svg")

# ---------------- Hole 8: Caldera Run
def h8():
    s = S("Hole 8 · Caldera Run (par 4)", "Volcanic Island · an upper rim walk, a lava-tunnel dive, and the Roulette Bowl")
    s.rect(30, 70, 840, 440, "#3a3330", rx=12)
    cx, cy = 600, 300
    s.circ(cx, cy, 190, C["lavad"], "#ff9a40", 4); s.circ(cx, cy, 150, C["stone"], C["stone2"], 5); s.circ(cx, cy, 120, "#8d8576"); s.circ(cx, cy, 60, "#a49d8e", "none")
    s.circ(cx, cy, 8, "#111"); s.add(f'<line x1="{cx}" y1="{cy}" x2="{cx}" y2="{cy-34}" stroke="#444" stroke-width="2.5"/>'); s.poly([(cx, cy - 34), (cx + 24, cy - 27), (cx, cy - 20)], "#e0262d")
    s.text(cx, cy + 62, "Roulette Bowl", 14, "#fff", weight="bold")
    s.path(f"M{cx-125},{cy+40} A130,130 0 1 1 {cx+80},{cy+100}", "none", C["wild"], 4, "3 7", marker="arrwild")
    s.poly(ring_band(cx, cy, 150, 190, 105, 400), C["stone"], C["stone2"], 3, 0.0)
    s.poly([(60, 470), (60, 400), (330, 400), (330, 470)], C["stone"], C["stone2"], 3)  # tee pad
    s.poly([(60, 400), (60, 150), (200, 150), (200, 400)], "#b8b0a0", C["stone2"], 3); s.text(130, 280, "rim walk (upper)", 12)
    s.poly([(200, 150), (200, 100), (560, 100), (560, 150)], "#b8b0a0", C["stone2"], 3)
    s.poly([(470, 100), (560, 100), (560, 170), (520, 190), (470, 150)], "#b8b0a0", C["stone2"], 3); s.text(515, 135, "gate", 11)
    s.rect(285, 420, 150, 40, C["dark"], "#111", 3, 6); s.text(360, 445, "lava tunnel", 12, "#ffd9a0")
    s.path("M330,440 L440,440", "none", C["lava"], 8, "6 6")
    s.rect(380, 120, 12, 100, C["dark"], "#111", 2, 4); s.circ(386, 170, 6, "#ffd9a0"); s.text(386, 238, "sweeper", 12, "#ffd9a0")
    s.circ(525, 190, 10, C["gold"], "#a07a1a", 2); s.text(525, 215, "drop", 11, "#fff")
    s.tee(100, 440)
    s.route([(110, 435), (130, 160), (210, 125), (480, 125), (515, 175)], "safe", "rim walk, then a ramp drop into the bowl", 330, 85)
    s.route([(120, 442), (300, 442), (445, 440), (470, 380), (500, 330)], "bold", "dive through the lava tunnel", 420, 485)
    s.text(cx, 515, "ball laps the bowl wall and spirals in to the cup", 13, "#ffd9a0", italic=True)
    save(s, "hole8.svg")

# ---------------- Hole 9: Summit Sanctuary
def h9():
    s = S("Hole 9 · Summit Sanctuary (par 5 provisional)", "Summit · a mountain-top journey: cloud bridge, wind tube, moon terraces, the Sunset Bowl")
    s.rect(30, 70, 840, 440, "#cfe4f2", rx=12)
    for x, y, r in ((120, 130, 55), (300, 100, 40), (700, 110, 50), (820, 200, 35)): s.ell(x, y, r * 1.8, r * 0.6, "#fff", op=0.9)
    s.poly([(60, 480), (60, 400), (330, 400), (330, 480)], C["grass"], "#3f7f3a", 3)                      # start terrace
    s.poly([(330, 440), (330, 410), (470, 410), (470, 440)], C["wood"], C["wood2"], 3); s.text(400, 430, "cloud bridge", 11, "#fff")
    s.poly([(470, 480), (470, 380), (700, 380), (700, 480)], C["stone"], C["stone2"], 3)                  # terrace 2
    s.circ(540, 340, 28, C["dark"], "#111", 3); s.text(540, 345, "wind tube", 11, "#bfe9f5")
    s.path("M540,340 C560,260 380,260 330,230", "none", C["glass"], 12, op=0.7)
    s.poly([(220, 300), (220, 200), (460, 200), (460, 300)], "#b8b0a0", C["stone2"], 3); s.text(340, 255, "moon terraces", 12)
    s.poly([(60, 230), (60, 150), (220, 150), (220, 230)], "#c9c1b0", C["stone2"], 3)
    s.circ(660, 180, 130, C["stone2"], "#555", 4); s.circ(660, 180, 105, C["gold"], "#a07a1a", 4); s.circ(660, 180, 60, "#f7e08a"); s.cup(660, 182)
    s.text(660, 82, "Sunset Bowl", 14, weight="bold"); s.poly([(460, 220), (500, 220), (560, 200), (560, 170), (500, 190), (460, 190)], C["gold"], "#a07a1a", 2)
    s.text(650, 300, "wide cone; whole archipelago behind", 11, "#555", italic=True)
    s.tee(100, 440)
    s.route([(110, 435), (330, 430), (470, 425), (600, 410), (700, 330), (500, 270), (330, 250), (120, 190), (330, 170), (555, 182), (650, 182)], "safe", "terraces and switchbacks (5 relaxed strokes)", 400, 520)
    s.route([(120, 438), (330, 425), (480, 430), (545, 345)], "bold", None); s.route([(335, 232), (400, 215), (560, 190), (640, 184)], "bold", "wind-tube shortcut to the bowl rim", 520, 150)
    s.route([(470, 372), (500, 300), (600, 250), (650, 215)], "wild", "moon jump onto the bowl rim", 760, 285)
    save(s, "hole9.svg")

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for f in (h1, h2, h3, h4, h5, h6, h7, h8, h9): f()
    print("wrote", sorted(os.listdir(OUT)))
