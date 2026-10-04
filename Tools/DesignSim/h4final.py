import sys; sys.path.insert(0,'.')
from h4v2 import *
for mouth, cone in ((0.42, (1.8, 0.16)), (0.45, (1.8, 0.16)), (0.42, (2.2, 0.22))):
    run(mouth, cone)
