"""Summarise WalkaboutProbe recordings into tuning numbers for GolfTuning.

Usage:  uv run --python 3.12 Tools/WalkaboutProbe/analyze.py <bodies_*.csv> [xforms_*.csv]

Prints (no raw game data is copied anywhere):
  * physics step rate
  * rolling deceleration fit  a = a0 + k*v  on flat, wall-free segments
  * wall rebound ratios (speed after / before a sharp direction change)
  * strike energy transfer (ball launch speed / putter speed just before launch)
"""
import csv
import math
import sys
from collections import defaultdict


def load(path):
    with open(path, newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return
    rows = load(sys.argv[1])
    xf = load(sys.argv[2]) if len(sys.argv) > 2 else []

    by_body = defaultdict(list)
    for r in rows:
        by_body[r["name"]].append(
            (float(r["time"]), float(r["fixedDt"]), float(r["vx"]), float(r["vy"]), float(r["vz"]), float(r["py"]))
        )

    for name, s in by_body.items():
        print(f"== body {name}: {len(s)} samples, physics rate {1 / s[0][1]:.0f} Hz")
        decel_pts, rebounds, launches = [], [], []
        for (t0, dt0, *v0, y0), (t1, dt1, *v1, y1) in zip(s, s[1:]):
            if t1 - t0 > dt0 * 1.5:
                continue  # gap: ball reset or slept
            h0, h1 = math.hypot(v0[0], v0[2]), math.hypot(v1[0], v1[2])
            if h0 < 1e-3:
                if h1 > 0.2:
                    launches.append((t1, h1))
                continue
            cosang = (v0[0] * v1[0] + v0[2] * v1[2]) / (h0 * max(h1, 1e-6))
            if h1 > h0 + 0.15:
                launches.append((t1, h1))
            elif cosang < 0.7 and h0 > 0.2:
                rebounds.append((h0, h1, math.degrees(math.acos(max(-1, min(1, cosang))))))
            elif cosang > 0.999 and abs(y1 - y0) < 1e-4 and abs(v1[1]) < 0.01:
                decel_pts.append((h0, (h0 - h1) / (t1 - t0)))

        if len(decel_pts) > 20:
            # least squares a = a0 + k v
            n = len(decel_pts)
            sv = sum(v for v, _ in decel_pts); sa = sum(a for _, a in decel_pts)
            svv = sum(v * v for v, _ in decel_pts); sva = sum(v * a for v, a in decel_pts)
            k = (n * sva - sv * sa) / max(n * svv - sv * sv, 1e-9)
            a0 = (sa - k * sv) / n
            print(f"   rolling decel fit over {n} flat samples: a0 = {a0:.3f} m/s^2, k = {k:.3f} 1/s"
                  f"  (GolfTuning.rollingDeceleration, speedDrag)")
        else:
            print("   not enough flat rolling samples for a decel fit")

        if rebounds:
            ratios = sorted(h1 / h0 for h0, h1, _ in rebounds)
            print(f"   {len(rebounds)} rebounds, speed ratio median {ratios[len(ratios) // 2]:.2f} "
                  f"(range {ratios[0]:.2f}-{ratios[-1]:.2f}); note this mixes restitution and tangential loss")

        if launches and xf:
            putter = [(float(r["time"]), float(r["speed"]), r["name"]) for r in xf]
            for t, v in launches[:30]:
                before = [p for p in putter if t - 0.05 <= p[0] <= t]
                if not before:
                    continue
                ps = max(p[1] for p in before)
                print(f"   launch t={t:.2f}: ball {v:.2f} m/s, putter max {ps:.2f} m/s -> ratio {v / max(ps, 1e-3):.2f}")
        elif launches:
            print(f"   {len(launches)} launches; pass xforms_*.csv to compute strike transfer")


if __name__ == "__main__":
    main()
