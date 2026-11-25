# Scripted Trouble Making Tanukis miniatures (Blender 5.x, headless bpy): the Tanuki and A Band of Troublemakers.
#   python3 tanuki.py <outdir> <preview.png>
import bpy, math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
bpy.ops.wm.read_factory_settings(use_empty=True)
import cc_common as cc
from cc_common import *

OUT_DIR = sys.argv[1] if len(sys.argv) > 1 else "/tmp"
OUT_PNG = sys.argv[2] if len(sys.argv) > 2 else "/tmp/tanuki.png"

make_materials({
    "Body":    "#B5652A",  # Tanuki primary
    "Belly":   "#D8A77A",
    "Blanket": "#F2C94C",  # Tanuki secondary: scarf, drum cord
    "Ink":     "#2A2320",  # eye mask, paws, tail rings
    "Bone":    "#E8E0CC",  # muzzle, drum skin
    "Rope":    "#E5D5B5",  # straw hat
    "Wood":    "#8A6440",
    "Gold":    "#F2B83B",  # Anankian coin
    "Leaf":    "#5E9E4B",
    "Nose":    "#2A2320",
})

def tanuki(k=1.0, belly=1.0):
    """Upright tanuki, front faces -Y, ~1.0*k tall to the top of the head."""
    v = lambda x, y, z: (x * k, y * k, z * k)
    for s in (-1, 1):
        sphere(v(s * 0.13, -0.03, 0.12), (0.10 * k, 0.12 * k, 0.12 * k), "Ink")                  # dark legs
        sphere(v(s * 0.13, -0.12, 0.03), (0.08 * k, 0.10 * k, 0.03 * k), "Ink", subd=0)
    sphere(v(0, 0.0, 0.38), (0.27 * k * belly, 0.25 * k * belly, 0.28 * k), "Body")              # round body
    sphere(v(0, -0.12 * belly, 0.34), (0.20 * k * belly, 0.16 * k * belly, 0.21 * k), "Belly")    # big belly
    sphere(v(0, -0.04, 0.72), (0.20 * k, 0.18 * k, 0.17 * k), "Body")                             # head
    sphere(v(0, -0.17, 0.68), (0.10 * k, 0.09 * k, 0.07 * k), "Bone")                             # pale muzzle
    sphere(v(0, -0.255, 0.70), (0.035 * k, 0.025 * k, 0.025 * k), "Nose", subd=0)
    for s in (-1, 1):
        sphere(v(s * 0.10, -0.15, 0.75), (0.075 * k, 0.04 * k, 0.05 * k), "Ink", rot=(0, s * 25, 0))   # the bandit mask
        sphere(v(s * 0.10, -0.185, 0.765), (0.022 * k, 0.012 * k, 0.022 * k), "Bone", subd=0)         # eye glints
        sphere(v(s * 0.105, -0.19, 0.76), (0.013 * k, 0.008 * k, 0.013 * k), "Ink", subd=0)
        sphere(v(s * 0.15, -0.02, 0.87), (0.06 * k, 0.03 * k, 0.06 * k), "Ink")                      # round ears
        sphere(v(s * 0.26, -0.10, 0.42), (0.065 * k, 0.065 * k, 0.09 * k), "Ink")                    # arms / paws
    # big bushy striped tail
    for i in range(5):
        t = i / 4
        sphere(v(0.04 * t, 0.26 + 0.10 * t, 0.16 + 0.20 * t), (0.11 * k, 0.11 * k, 0.09 * k), "Ink" if i % 2 else "Body")

# ======================= Tanuki: straw hat, coin pouch on the back =======================
cc.parts = []
tanuki()
cyl((0, -0.03, 0.86), 0.26, 0.03, "Rope", subd=0)                                     # wide straw hat brim
cone((0, -0.03, 0.94), 0.17, 0.14, "Rope")
tube([(-0.17, -0.12, 0.62), (0, -0.21, 0.56), (0.17, -0.12, 0.62)], 0.025, "Blanket")  # scarf
cyl((0.0, 0.25, 0.42), 0.12, 0.035, "Gold", rot=(80, 0, 0), subd=0)                    # a stolen Anankian coin on the back
cube((0.0, 0.272, 0.42), (0.035, 0.01, 0.035), "Ink", rot=(80, 0, 45), bevel=0, subd=0)
tube([(-0.18, -0.10, 0.58), (-0.10, 0.20, 0.52), (0.0, 0.25, 0.50)], 0.012, "Wood")
T = export_groups({"Tanuki": cc.parts}, os.path.join(OUT_DIR, "Tanuki.fbx"), decimate_over=250, ratio=0.25)[0]
T.location.x = 10

# ======================= A Band of Troublemakers: pot-bellied drummer =======================
cc.parts = []
K = 1.3
tanuki(K, belly=1.25)
# leaf balanced on the head (the shapeshifter's trick)
bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, location=(0.03, -0.04 * K, 0.93 * K))
lf = bpy.context.object; lf.scale = (0.05, 0.14, 0.015); lf.rotation_euler = (math.radians(10), 0, math.radians(20)); finish(lf, "Leaf", 1)
# taiko drum strapped across the belly, with drumsticks
cyl((0, -0.37, 0.42), 0.23, 0.22, "Wood", rot=(90, 0, 0), subd=0)
cyl((0, -0.48, 0.42), 0.21, 0.01, "Bone", rot=(90, 0, 0), subd=0)
for y in (-0.27, -0.47):
    torus((0, y, 0.42), 0.235, 0.012, "Blanket", rot=(90, 0, 0))
for i in range(8):
    a = i / 8 * 2 * math.pi
    sphere((0.235 * math.cos(a), -0.47, 0.42 + 0.235 * math.sin(a)), (0.014, 0.014, 0.014), "Ink", subd=0)
tube([(-0.30, -0.10, 0.70), (0, -0.20, 0.72), (0.30, -0.10, 0.70)], 0.02, "Blanket")       # drum strap
for s in (-1, 1):
    limb((s * 0.33, -0.17, 0.50), (s * 0.16, -0.56, 0.74), 0.016, 0.014, "Wood")
    sphere((s * 0.16, -0.56, 0.74), (0.028, 0.028, 0.028), "Blanket", subd=0)
# sake jug hanging at the hip
cyl((0.33, 0.06, 0.28), 0.08, 0.16, "Rope", taper=0.7)
cyl((0.33, 0.06, 0.38), 0.03, 0.05, "Wood")
B = export_groups({"BandOfTroublemakers": cc.parts}, os.path.join(OUT_DIR, "BandOfTroublemakers.fbx"), decimate_over=250, ratio=0.3)[0]

T.location = (-0.85, 0.3, 0)
c = T.copy(); c.data = T.data; bpy.context.scene.collection.objects.link(c); c.location = (1.05, 0.4, 0)
B.location = (0.1, -0.1, 0)
render_preview(OUT_PNG, (0.4, -4.0, 1.9), (0.05, 0.1, 0.55))
print("done")
