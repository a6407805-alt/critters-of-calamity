# Scripted Firestarting Squirrels miniatures (Blender 5.x, headless bpy): the Squirrel and the Pyromaniac.
#   python3 squirrels.py <outdir> <preview.png>
import bpy, math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
bpy.ops.wm.read_factory_settings(use_empty=True)
import cc_common as cc
from cc_common import *

OUT_DIR = sys.argv[1] if len(sys.argv) > 1 else "/tmp"
OUT_PNG = sys.argv[2] if len(sys.argv) > 2 else "/tmp/squirrels.png"

make_materials({
    "Body":    "#E35225",  # Squirrels primary
    "Belly":   "#EBA27E",
    "Blanket": "#3B322C",  # secondary: soot-dark straps, scarf
    "Ink":     "#2A2320",
    "Bone":    "#E8E0CC",  # teeth, eye glints
    "Wood":    "#8A6440",  # acorn, torch handles
    "Leather": "#5A4130",
    "Flame":   "#F7C948",  # Palette.EmberYellow
    "Ember":   "#E8822E",
    "Iron":    "#B8B2A6",
})

def flame(c, s=1.0):
    """Teardrop flame: orange outer with a bright yellow core showing at the front."""
    x, y, z = c
    sphere((x, y, z + 0.05 * s), (0.065 * s, 0.065 * s, 0.07 * s), "Ember")
    cone((x, y, z + 0.15 * s), 0.058 * s, 0.17 * s, "Ember")
    sphere((x, y - 0.035 * s, z + 0.055 * s), (0.045 * s, 0.035 * s, 0.05 * s), "Flame")
    cone((x, y - 0.04 * s, z + 0.12 * s), 0.038 * s, 0.12 * s, "Flame")

def squirrel(k=1.0):
    """Upright squirrel, front -Y, ~0.95*k tall to the ear tips; returns paw positions."""
    v = lambda x, y, z: (x * k, y * k, z * k)
    sphere(v(0, 0.05, 0.20), (0.20 * k, 0.22 * k, 0.18 * k), "Body")                 # haunches
    for s in (-1, 1):
        sphere(v(s * 0.12, -0.13, 0.04), (0.06 * k, 0.13 * k, 0.035 * k), "Body")
    sphere(v(0, -0.02, 0.40), (0.17 * k, 0.15 * k, 0.21 * k), "Body", rot=(-10, 0, 0))
    sphere(v(0, -0.10, 0.38), (0.12 * k, 0.09 * k, 0.16 * k), "Belly", rot=(-10, 0, 0))
    sphere(v(0, -0.06, 0.66), (0.14 * k, 0.15 * k, 0.13 * k), "Body")                 # head
    sphere(v(0, -0.19, 0.63), (0.07 * k, 0.07 * k, 0.06 * k), "Belly")                # muzzle
    sphere(v(0, -0.255, 0.645), (0.022 * k, 0.015 * k, 0.017 * k), "Ink", subd=0)
    cube(v(0, -0.24, 0.585), (0.022 * k, 0.006 * k, 0.025 * k), "Bone", bevel=0.003, subd=0)   # teeth
    for s in (-1, 1):
        sphere(v(s * 0.065, -0.17, 0.70), (0.03 * k, 0.018 * k, 0.034 * k), "Ink", subd=0)    # eyes
        sphere(v(s * 0.058, -0.185, 0.715), (0.009 * k, 0.005 * k, 0.009 * k), "Bone", subd=0)
        cone(v(s * 0.08, -0.04, 0.82), 0.045 * k, 0.14 * k, "Body", rot=(-5, s * 18, 0))     # tufted ears
        cone(v(s * 0.085, -0.04, 0.90), 0.015 * k, 0.07 * k, "Ink", rot=(-5, s * 18, 0))
    # the big S-curved bushy tail rising behind
    for i in range(16):
        t = i / 15
        r = 0.13 + 0.05 * math.sin(t * math.pi)                                     # fattest mid-way: bushy
        sphere(v(0.02 * math.sin(t * 4), 0.24 + 0.16 * math.sin(t * math.pi * 0.85) - 0.06 * t * t, 0.14 + 0.74 * t),
               (r * k, r * 0.9 * k, 0.13 * k), "Body")
    sphere(v(0.0, 0.17, 0.90), (0.12 * k, 0.11 * k, 0.10 * k), "Body")              # tail tip curls forward
    paws = {}
    for s in (-1, 1):
        p = v(s * 0.15, -0.16, 0.44)
        limb(v(s * 0.13, -0.04, 0.55), p, 0.04 * k, 0.035 * k, "Body")
        sphere(p, (0.04 * k, 0.04 * k, 0.04 * k), "Belly")
        paws[s] = p
    return paws

# ======================= Squirrel: carries a lit Acorn Firestarter =======================
cc.parts = []
P = squirrel()
tube([(-0.14, -0.08, 0.58), (0, -0.17, 0.54), (0.14, -0.08, 0.58)], 0.02, "Blanket")        # soot-dark scarf
a = (0.0, -0.27, 0.47)                                                                     # acorn held in both paws
sphere(a, (0.08, 0.08, 0.09), "Wood")
shell(a, (0.085, 0.085, 0.09), "Leather", [((0, 0, a[2] + 0.02), (0, 0, 1))], thick=0.02)  # acorn cap
tube([(a[0], a[1], a[2] + 0.10), (a[0] + 0.03, a[1] - 0.02, a[2] + 0.15)], 0.007, "Ink")   # fuse
sphere((a[0] + 0.035, a[1] - 0.025, a[2] + 0.165), (0.02, 0.02, 0.03), "Flame", subd=0)    # spark
SQ = export_groups({"Squirrel": cc.parts}, os.path.join(OUT_DIR, "Squirrel.fbx"), decimate_over=250, ratio=0.18)[0]
SQ.location.x = 10

# ======================= Pyromaniac: goggles, two torches, a tail on fire =======================
cc.parts = []
K = 1.2
P = squirrel(K)
for s in (-1, 1):                                                                          # brass goggles
    cyl((s * 0.075 * K, -0.20 * K, 0.71 * K), 0.045 * K, 0.04 * K, "Iron", rot=(90, 0, 0), subd=0)
    cyl((s * 0.075 * K, -0.225 * K, 0.71 * K), 0.035 * K, 0.01 * K, "Flame", rot=(90, 0, 0), subd=0)
tube([(-0.17 * K, -0.10 * K, 0.72 * K), (0, -0.06 * K, 0.80 * K), (0.17 * K, -0.10 * K, 0.72 * K)], 0.01 * K, "Leather")
tube([(-0.16 * K, -0.10 * K, 0.42 * K), (0.0, -0.17 * K, 0.30 * K), (0.16 * K, -0.10 * K, 0.42 * K)], 0.02 * K, "Blanket")  # bandolier
for i in range(3):
    sphere((-0.06 + i * 0.06, -0.19 * K, 0.33 * K + abs(i - 1) * 0.02), (0.03, 0.03, 0.035), "Wood", subd=0)      # spare acorns
for s in (-1, 1):                                                                          # a torch in each paw, held high
    p = P[s]
    limb((p[0], p[1], p[2] - 0.08), (p[0] + s * 0.04, p[1] - 0.02, p[2] + 0.30), 0.02, 0.025, "Wood")
    cyl((p[0] + s * 0.045, p[1] - 0.02, p[2] + 0.31), 0.035, 0.05, "Leather", subd=0)
    flame((p[0] + s * 0.045, p[1] - 0.02, p[2] + 0.33), 1.1)
flame((0.0, 0.24 * K, 0.97 * K), 1.3)                                                      # the tail tip is alight
for i in range(5):                                                                         # soot patches
    sphere((0.12 * math.cos(i * 1.3) * K, -0.03 * K, (0.30 + i * 0.06) * K), (0.04, 0.03, 0.03), "Blanket", subd=0)
PY = export_groups({"Pyromaniac": cc.parts}, os.path.join(OUT_DIR, "Pyromaniac.fbx"), decimate_over=250, ratio=0.3)[0]

SQ.location = (-0.8, 0.3, 0)
c = SQ.copy(); c.data = SQ.data; bpy.context.scene.collection.objects.link(c); c.location = (0.9, 0.4, 0)
PY.location = (0.05, -0.05, 0)
render_preview(OUT_PNG, (0.3, -3.8, 1.8), (0.05, 0.1, 0.55))
print("done")
