"""Summit Sanctuary hero architecture (Graphics Pass 3B). Original work.

  blender -b --factory-startup --python Tools/Blender/hero_summit.py -- [piece ...]

  HeroSanctuary   the sanctuary behind the Altar: a stepped platform with a grand stair, a colonnade, wing walls with merlons, a sun-crowned
                  gate-temple with a real voussoir arch doorway, flanking carved pillars with braziers and banner poles. Front faces -Y; origin = centre of
                  the front base at the Altar's surface level (nothing of it stands over the playable Altar: its front is placed beyond the Altar rail).
  HeroSummitGate  a ceremonial gate straddling the Landing: two massive piers and a voussoir arch, a sun crest, braziers; the clear opening is 1.7 m wide and the
                  arch crown 2.85 m (above a standing player's head). Front faces -Y (the approach), origin = centre of the footprint at the Landing's level.
"""
import math
import os
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import archlib as al  # noqa: E402
import gblib as gb  # noqa: E402
from archlib import Piece, stone  # noqa: E402
import hero_temple as ht  # noqa: E402
from hero_temple import L, P, W, F, GOLD, GOLD_DARK, RED, DARK, BRONZE, FLAME, TIMBER, brazier, carved_pier  # noqa: E402

OUT = "Assets/_Game/Art/Generated/Models"


def banner(p, x, y, z_top, h=1.5, w=0.5):
    """A cloth banner hanging from a wooden crossbar: red cloth, gold sun disc and fringe."""
    p.box(W, (x, y, z_top), (w + 0.16, 0.05, 0.05), color=TIMBER, bevel=0.012)
    p.box(P, (x, y, z_top - h / 2 - 0.03), (w, 0.025, h), color=RED, bevel=0.006, ao=False)
    p.prism(P, (x, y - 0.012, z_top - h * 0.38), 0.12, 0.12, 0.012, 14, GOLD, 0.0, rot=(math.pi / 2, 0, 0))
    for i in range(6):
        p.box(P, (x - w / 2 + 0.05 + i * (w - 0.1) / 5, y, z_top - h - 0.07), (0.03, 0.02, 0.1), color=GOLD, bevel=0.004, ao=False)


def sanctuary():
    p = Piece("HeroSanctuary", {L: 1.8, P: 1.0, W: 1.2, F: 1.0}, seed=51)
    # ground course and the first platform
    p.mass(L, -4.9, 4.9, -0.2, 4.6, -1.2, 0.0, ch=0.4, bw=0.8, base=0.78, bevel=0.04)
    p.mass(L, -4.5, 4.5, 0.2, 4.2, 0.0, 1.35, ch=0.28, bw=0.62, exclude_front=[(-3.9, 3.9, 0.7, 0.95)])
    p.box(L, (0, 2.25, 1.41), (9.3, 4.2, 0.13), color=stone(1.0), bevel=0.04)
    # carved band
    p.box(P, (0, 0.22, 0.82), (8.8, 0.05, 0.22), color=DARK, bevel=0.0)
    for i in range(29):
        x = -4.2 + i * 0.3
        p.box(L, (x, 0.14, 0.82 + (0.035 if i % 2 else -0.035)), (0.2, 0.07, 0.13), color=stone(1.0, 0.02), bevel=0.012)
    # grand stair (decorative) rising to the colonnade level, with cheeks
    p.stairs(L, -1.4, 1.4, -1.6, 0.2, 1.4, 8, color=stone(0.97), bevel=0.02)
    for sx in (-1, 1):
        x0, x1 = (sx * 1.45, sx * 1.8) if sx > 0 else (sx * 1.8, sx * 1.45)
        p.wedge(L, x0, x1, -1.6, 0.2, 0.3, 1.75, color=stone(0.95))
    # second platform
    p.mass(L, -3.5, 3.5, 1.2, 3.9, 1.41, 2.75, ch=0.27, bw=0.6, exclude_front=[(-1.4, 1.4, 1.43, 2.73)])
    p.box(L, (0, 2.6, 2.81), (7.4, 2.9, 0.13), color=stone(1.0), bevel=0.04)
    # colonnade on the first platform's front: four round columns each side of the stair
    for sx in (-1, 1):
        for k in range(2):
            p.column(L, sx * (2.2 + k * 0.95), 0.62, 1.41, 1.3, 0.17, color=stone(0.98))
    p.box(L, (0, 0.62, 2.74), (6.6, 0.4, 0.1), color=stone(1.0), bevel=0.03)             # architrave over the columns
    # the sun-gate temple: third platform with a voussoir arch doorway
    p.mass(L, -2.3, 2.3, 2.0, 3.7, 2.81, 5.1, ch=0.26, bw=0.58, exclude_front=[(-0.95, 0.95, 2.83, 4.0)])
    p.box(P, (0, 2.12, 3.1), (1.9, 0.05, 0.6), color=DARK, bevel=0.0)
    p.box(P, (0, 2.12, 3.75), (1.5, 0.05, 0.7), color=DARK, bevel=0.0)
    p.arch(L, 0, 1.96, 3.35, 0.8, 0.26, 0.22, n=11)
    for sx in (-1, 1):
        p.box(L, (sx * 1.0, 2.0, 3.1), (0.24, 0.25, 0.6), color=stone(1.0), bevel=0.03)
        p.box(L, (sx * 1.18, 2.02, 4.05), (0.5, 0.28, 0.5), color=stone(1.0), bevel=0.03)
    p.box(P, (0, 2.08, 3.55), (1.45, 0.05, 1.5), color=DARK, bevel=0.0)
    p.sun(P, 0, 2.0, 4.55, 0.62, rays=20, ring_grp=L)
    p.box(L, (0, 2.85, 5.17), (5.0, 1.9, 0.14), color=stone(1.0), bevel=0.04)
    # roof crest: stepped finial with a gold sun spike
    for k, (w, h) in enumerate(((1.3, 0.2), (0.9, 0.2), (0.5, 0.2))):
        zc = 5.24 + k * 0.2 + h / 2
        p.box(L, (0, 2.85, zc), (w, w * 0.9, h), color=stone(1.0), bevel=0.03)
    p.hull(P, [(-0.2, 2.65, 5.84), (0.2, 2.65, 5.84), (0.2, 3.05, 5.84), (-0.2, 3.05, 5.84), (0, 2.85, 6.4)], GOLD)
    # flanking carved pillars with braziers, wing walls and merlons
    for sx in (-1, 1):
        carved_pier(p, sx * 3.0, 1.35, 0.9, 1.41, 4.6, 0.8)
        brazier(p, sx * 3.0, 1.8, 4.6, 0.32)
        carved_pier(p, sx * 4.85, 0.3, 0.9, -1.2, 2.5, 0.8)
        brazier(p, sx * 4.85, 0.75, 2.5, 0.3)
        # wing wall along the first platform's edge with merlons
        for k in range(7):
            p.box(L, (sx * (3.3 + k * 0.3), 0.18, 1.62), (0.2, 0.3, 0.28), color=stone(0.97), bevel=0.02)
        banner(p, sx * 1.7, 2.03, 3.7)
        banner(p, sx * 3.7, 0.5, 2.3, h=1.1)
    return p


def summit_gate():
    p = Piece("HeroSummitGate", {L: 1.8, P: 1.0, F: 1.0}, seed=61)
    spring, r_in = 2.0, 0.85
    # two massive piers (ashlar on three sides) with a plinth
    for sx in (-1, 1):
        x0, x1 = (sx * 0.9, sx * 1.65) if sx > 0 else (sx * 1.65, sx * 0.9)
        p.mass(L, x0, x1, -0.45, 0.45, -3.2, spring + 0.1, ch=0.26, bw=0.5, base=0.95)
        p.box(L, (sx * 1.28, 0.0, -0.05), (0.95, 0.75, 0.2), color=stone(0.85), bevel=0.035)
        p.box(L, (sx * 1.28, 0.0, spring + 0.14), (0.95, 0.75, 0.16), color=stone(1.0), bevel=0.04)
        carved = (sx * 1.28, -0.45, 1.0)
        ht.carved_face(p, carved[0], carved[1], carved[2], 0.62)
    # the arch: a voussoir ring with a keystone, a stone cornice and spandrel above
    p.arch(L, 0, -0.45, spring + 0.1, r_in, 0.3, 0.9, n=13)
    p.box(L, (0, 0.0, spring + r_in + 0.1 + 0.3 + 0.12), (2.6, 0.95, 0.24), color=stone(1.0), bevel=0.04)
    # sun crest above the arch
    p.box(L, (0, 0.0, spring + r_in + 0.55 + 0.28), (1.0, 0.8, 0.55), color=stone(0.97), bevel=0.04)
    p.sun(P, 0, -0.42, spring + r_in + 0.12 + 0.62, 0.3, rays=14, ring_grp=L)
    p.hull(P, [(-0.18, -0.15, spring + r_in + 1.1), (0.18, -0.15, spring + r_in + 1.1), (0.18, 0.15, spring + r_in + 1.1), (-0.18, 0.15, spring + r_in + 1.1), (0, 0, spring + r_in + 1.6)], GOLD)
    for sx in (-1, 1):
        brazier(p, sx * 1.28, 0.0, spring + 0.22, 0.26)
    return p


PIECES = {"HeroSanctuary": sanctuary, "HeroSummitGate": summit_gate}


def main():
    want = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for name, fn in PIECES.items():
        if want and name not in want:
            continue
        try:
            gb.reset_scene()
            fn().export(OUT, ao_samples=20, ao_dist=0.7)
        except Exception as e:
            import traceback
            al.log(f"{name} FAILED: {e}\n{traceback.format_exc()}")
    al.log("hero_summit done")


if __name__ == "__main__":
    main()
