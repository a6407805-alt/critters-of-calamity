# Scripted token toppers for Critters of Calamity (Blender 5.x, headless bpy): Supply Line, Gluttony, Catapult,
# Holy Relic, Coin and Tar Pit. They sit on the game's token disc; each fits a ~1.0 footprint, front faces -Y.
# The Catapult's two stones are "Count_1"/"Count_2" meshes: the game shows one per use left.
#   python3 tokens.py <outdir> <preview.png>
import bpy, math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
bpy.ops.wm.read_factory_settings(use_empty=True)
import cc_common as cc
from cc_common import *

OUT_DIR = sys.argv[1] if len(sys.argv) > 1 else "/tmp"
OUT_PNG = sys.argv[2] if len(sys.argv) > 2 else "/tmp/tokens.png"

make_materials({
    "Body":    "#C86428",
    "Blanket": "#F5C445",
    "Ink":     "#2A2320",
    "Wood":    "#8A6440",
    "Leather": "#5A4130",
    "Rope":    "#E5D5B5",
    "Stone":   "#B0B2B8",
    "Robe":    "#F4EEDC",
    "Gold":    "#F2B83B",
    "Carrot":  "#E8822E",
    "Leaf":    "#4F8F4A",
    "Tar":     "#1A1714",
})

OUT = {}
def done(name, extra=None, yaw=0):
    groups = {name: cc.parts}
    if extra: groups.update(extra)
    if yaw:   # turn the whole piece (e.g. so the catapult shows its arm side-on to the camera)
        from mathutils import Matrix
        R = Matrix.Rotation(math.radians(yaw), 4, 'Z')
        for objs in groups.values():
            for o in objs: o.matrix_world = R @ o.matrix_world
    ms = export_groups(groups, os.path.join(OUT_DIR, name + ".fbx"), decimate_over=300, ratio=0.4)
    for m in ms: m.location.x = 50 + len(OUT) * 5
    OUT[name] = ms

# ======================= Supply Line: crates, a sack and a pennant =======================
cc.parts = []
cube((-0.12, 0.05, 0.16), (0.20, 0.18, 0.16), "Wood", bevel=0.02, subd=0)
cube((-0.12, 0.05, 0.16), (0.205, 0.04, 0.165), "Leather", bevel=0, subd=0)         # strap
cube((0.20, 0.10, 0.12), (0.14, 0.14, 0.12), "Wood", rot=(0, 0, 20), bevel=0.02, subd=0)
cube((-0.08, 0.05, 0.40), (0.13, 0.12, 0.08), "Wood", rot=(0, 0, -12), bevel=0.02, subd=0)
sphere((0.20, -0.22, 0.12), (0.13, 0.11, 0.13), "Rope")                               # grain sack
cone((0.20, -0.22, 0.27), 0.05, 0.07, "Rope")
torus((0.20, -0.22, 0.23), 0.045, 0.012, "Leather")
limb((-0.32, -0.12, 0.0), (-0.32, -0.12, 0.78), 0.014, 0.012, "Wood")                # pennant pole
cube((-0.22, -0.12, 0.70), (0.10, 0.006, 0.06), "Body", bevel=0, subd=0)              # owner pennant
cone((-0.12, -0.12, 0.70), 0.06, 0.06, "Body", rot=(0, -90, 0))
done("SupplyLine")

# ======================= Gluttony: a heap of carrots =======================
cc.parts = []
for i, (x, y, yaw, pitch) in enumerate(((-0.15, 0.05, 20, 80), (0.12, -0.05, -35, 85), (0.0, 0.14, 95, 75), (0.02, -0.15, 160, 60))):
    a = math.radians(yaw)
    cx, cy = x, y
    cone((cx, cy, 0.10 + 0.06 * (i == 3)), 0.07, 0.40, "Carrot", rot=(pitch, 0, yaw))
    tx, ty = cx - 0.20 * math.sin(a) * -1, cy - 0.20 * math.cos(a)
    for k in (-1, 0, 1):
        cone((cx + 0.22 * math.sin(a) * -1 + k * 0.02, cy + 0.22 * math.cos(a), 0.14 + 0.06 * (i == 3)), 0.018, 0.14, "Leaf", rot=(-pitch + 20 * k, 0, yaw))
done("Gluttony")

# ======================= Catapult: wooden frame, throwing arm, stones for uses left =======================
cc.parts = []
for x in (-0.20, 0.20):
    cube((x, 0.0, 0.05), (0.03, 0.34, 0.03), "Wood", bevel=0.005, subd=0)             # base rails
    limb((x, -0.12, 0.05), (x, 0.02, 0.42), 0.022, 0.02, "Wood")                     # A-frame
    limb((x, 0.16, 0.05), (x, 0.02, 0.42), 0.022, 0.02, "Wood")
cube((0, -0.30, 0.05), (0.23, 0.03, 0.03), "Wood", bevel=0.005, subd=0)
cube((0, 0.30, 0.05), (0.23, 0.03, 0.03), "Wood", bevel=0.005, subd=0)
cyl((0, 0.02, 0.40), 0.03, 0.46, "Wood", rot=(0, 90, 0), subd=0)                     # axle
limb((0, -0.24, 0.10), (0, 0.24, 0.62), 0.025, 0.02, "Wood")                          # throwing arm
cyl((0, 0.26, 0.64), 0.08, 0.05, "Wood", rot=(-48, 0, 0), subd=0)                     # cup
cube((0, -0.24, 0.10), (0.08, 0.06, 0.06), "Stone", bevel=0.01, subd=0)               # counterweight
for x in (-0.24, 0.24):
    cyl((x, -0.22, 0.06), 0.06, 0.03, "Wood", rot=(0, 90, 0), subd=0)                 # wheels
    cyl((x, 0.22, 0.06), 0.06, 0.03, "Wood", rot=(0, 90, 0), subd=0)
frame = cc.parts
extra = {}
for i, (x, y, z) in enumerate(((0.0, 0.27, 0.70), (0.30, -0.36, 0.06))):            # one stone loaded, one waiting
    cc.parts = []
    sphere((x, y, z), (0.07, 0.07, 0.065), "Stone", subd=1)
    extra["Count_%d" % (i + 1)] = cc.parts
cc.parts = frame
done("Catapult", extra)   # the game turns it side-on

# ======================= Holy Relic: a gold reliquary topped by a cross =======================
cc.parts = []
cube((0, 0, 0.06), (0.26, 0.18, 0.06), "Robe", bevel=0.02, subd=0)                    # white plinth
cube((0, 0, 0.22), (0.20, 0.13, 0.10), "Gold", bevel=0.02, subd=0)                    # reliquary chest
prism_pts = [(-0.20, -0.13, 0.32), (0.20, -0.13, 0.32), (0.0, -0.13, 0.44), (-0.20, 0.13, 0.32), (0.20, 0.13, 0.32), (0.0, 0.13, 0.44)]
me = bpy.data.meshes.new("roof"); me.from_pydata(prism_pts, [], [(0, 2, 5, 3), (1, 4, 5, 2), (3, 5, 4), (0, 1, 2), (0, 3, 4, 1)]); me.update()
ro = bpy.data.objects.new("roof", me); bpy.context.scene.collection.objects.link(ro); finish(ro, "Gold", 0, bevel=0.01)
for x in (-0.12, 0.0, 0.12):
    sphere((x, -0.135, 0.22), (0.025, 0.01, 0.025), "Body", subd=0)                   # gems (owner color)
cube((0, 0, 0.60), (0.022, 0.022, 0.15), "Gold", bevel=0.004, subd=0)                 # cross
cube((0, 0, 0.66), (0.08, 0.022, 0.022), "Gold", bevel=0.004, subd=0)
done("HolyRelic")

# ======================= Coin: a small stack of Anankian coins with square holes =======================
cc.parts = []
for i, (x, y) in enumerate(((0, 0), (0.02, -0.01), (-0.015, 0.015), (0.01, 0.0))):
    cyl((x, y, 0.03 + i * 0.06), 0.30, 0.05, "Gold", subd=0)
    cube((x, y, 0.03 + i * 0.06), (0.07, 0.07, 0.03), "Ink", rot=(0, 0, 45), bevel=0, subd=0)
cyl((0.30, -0.30, 0.20), 0.22, 0.045, "Gold", rot=(70, 0, 30), subd=0)               # one leaning on the stack
cube((0.30, -0.30, 0.20), (0.05, 0.03, 0.05), "Ink", rot=(70, 0, 30), bevel=0, subd=0)
done("Coin")

# ======================= Tar Pit: a glossy black puddle with bubbles and a stuck stick =======================
cc.parts = []
sphere((0, 0, 0.0), (0.42, 0.38, 0.06), "Tar")
for x, y, r in ((-0.14, 0.08, 0.08), (0.12, -0.10, 0.06), (0.20, 0.14, 0.045), (-0.05, -0.18, 0.04)):
    sphere((x, y, 0.04), (r, r, r * 0.8), "Tar")                                      # bubbles
torus((-0.14, 0.08, 0.07), 0.05, 0.012, "Tar")                                         # a popped bubble ring
limb((0.18, 0.02, 0.0), (0.30, -0.05, 0.40), 0.02, 0.016, "Wood")                     # stick stuck in the tar
sphere((0.18, 0.02, 0.03), (0.05, 0.05, 0.04), "Tar")
done("TarPit")

names = list(OUT)
for i, n in enumerate(names):
    for m in OUT[n]: m.location = ((i - 2.5) * 1.15, 0.0, 0)
render_preview(OUT_PNG, (0.0, -8.0, 3.8), (0, 0.0, 0.2), size=(1600, 560), lens=40)
print("done")
