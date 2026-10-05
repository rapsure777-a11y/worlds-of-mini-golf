# DesignSim

Offline paper model of the ball physics (`GolfBall.cs`) used for Hole Design Sprint 001. Not part of the game; nothing here is compiled by Unity.

- Build (automatic on first import, or by hand): `gcc -O2 -shared -fPIC -o libgolfsim.so golfsim.c -lm`
- Check the model against facts the repo's own tests establish: `python3 validate.py` (all lines should say PASS).
- As-built holes: `python3 asbuilt.py`, `python3 asbuilt_h4.py`. Concept studies: `h*_explore.py`, `h4_review.py`, `others.py`.
- Player scatter (`PLAYERS` in `golfsim.py`) is assumed, not measured. Airborne balls and cup lip-outs are not modelled. Use results to compare options, not as predicted scores.
