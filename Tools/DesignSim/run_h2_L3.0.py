import sys; sys.path.insert(0,'.')
from h2_tune import *
evaluate("arena half-width 3.0, gap .5, funnel r.45 d3", 3.0, 0.5, 0.45, 0.03, (-1.15, 6.7), n=200, L=3.0)
