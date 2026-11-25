# Scripted Platypus Papacy miniatures (Blender 5.x, headless bpy): Pilgrim, Crusader and the Platy-Pope.
#   python3 papacy.py <outdir> <preview.png>
import bpy, math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
bpy.ops.wm.read_factory_settings(use_empty=True)
import cc_common as cc
from cc_common import *

OUT_DIR = sys.argv[1] if len(sys.argv) > 1 else "/tmp"
OUT_PNG = sys.argv[2] if len(sys.argv) > 2 else "/tmp/papacy.png"

make_materials({
    "Body":    "#2E8B9A",  # Papacy primary (fur)
    "Blanket": "#F5E6A8",  # Papacy secondary: trims
    "Bill":    "#2B3238",  # bill and webbed feet
    "Ink":     "#2A2320",
    "Robe":    "#F4EEDC",  # Palette.HolyWhite
    "Rope":    "#E5D5B5",  # Palette.ParchmentMap: pilgrim's habit
    "Leather": "#5A4130",
    "Wood":    "#8A6440",
    "Iron":    "#B8B2A6",  # Palette.RuinStoneLight
    "Royal":   "#9E2A2B",  # crusader's cross
    "Gold":    "#F2B83B",
})

def platypus(robe, sleeves=True):
    """Upright robed platypus, front faces -Y, ~1.15 tall to the top of the head."""
    cyl((0, 0.0, 0.40), 0.31, 0.78, robe, taper=0.62, subd=1)                   # robe body
    sphere((0, 0.0, 0.80), (0.21, 0.19, 0.12), robe)                               # shoulders
    sphere((0, -0.04, 0.95), (0.17, 0.17, 0.155), "Body")                          # head
    cube((0, -0.29, 0.90), (0.12, 0.15, 0.032), "Bill", rot=(-10, 0, 0), bevel=0.03, subd=1)   # the duck bill
    for s in (-1, 1):
        sphere((s * 0.03, -0.38, 0.93), (0.012, 0.01, 0.006), "Ink", subd=0)     # nostrils
        sphere((s * 0.095, -0.15, 1.00), (0.026, 0.018, 0.026), "Ink", subd=0)    # eyes
        sphere((s * 0.13, -0.27, 0.025), (0.09, 0.12, 0.022), "Bill", subd=1)     # webbed feet under the hem
    cube((0, 0.40, 0.05), (0.15, 0.22, 0.03), "Body", rot=(10, 0, 0), bevel=0.04, subd=1)  # paddle tail behind
    hands = {}
    for s in (-1, 1):
        sh = (s * 0.21, -0.02, 0.80); hd = (s * 0.24, -0.24, 0.58)
        if sleeves:
            limb(sh, hd, 0.075, 0.095, robe)
        sphere((hd[0], hd[1] - 0.04, hd[2] - 0.01), (0.05, 0.05, 0.045), "Body")
        hands[s] = (hd[0], hd[1] - 0.05, hd[2] - 0.01)
    return hands

# ======================= Pilgrim =======================
cc.parts = []
h = platypus("Rope")
shell((0, -0.02, 0.97), (0.20, 0.20, 0.19), "Rope", [((0, 0.0 - 0.10, 0), (0, 1, 0)), ((0, 0, 0.83), (0, 0, 1))], thick=0.025)  # hood
shell((0, -0.02, 0.97), (0.20, 0.20, 0.19), "Rope", [((0, 0, 1.04), (0, 0, 1))], thick=0.025)                                   # hood crown
torus((0, 0.0, 0.62), 0.215, 0.018, "Leather", scale=(1, 0.95, 1))                                                             # rope belt
tube([(0.05, -0.20, 0.61), (0.07, -0.23, 0.48), (0.06, -0.24, 0.36)], 0.012, "Leather")
R = h[-1]
limb((R[0], R[1], 0.02), (R[0], R[1], 1.30), 0.018, 0.016, "Wood")                                                              # walking staff
torus((R[0], R[1], 1.30), 0.045, 0.012, "Wood", rot=(90, 0, 0))
sphere((h[1][0] + 0.02, h[1][1] - 0.03, h[1][2] - 0.02), (0.05, 0.02, 0.06), "Blanket", subd=0)                                 # scallop pilgrim badge
G = export_groups({"Pilgrim": cc.parts}, os.path.join(OUT_DIR, "Pilgrim.fbx"), decimate_over=250, ratio=0.25)[0]
G.location.x = 10

# ======================= Crusader =======================
cc.parts = []
h = platypus("Robe")
cube((0, -0.215, 0.50), (0.13, 0.02, 0.26), "Robe", rot=(6, 0, 0), bevel=0.01, subd=0)               # tabard front
cube((0, -0.24, 0.55), (0.025, 0.01, 0.17), "Royal", rot=(6, 0, 0), bevel=0, subd=0)                  # red cross
cube((0, -0.24, 0.62), (0.09, 0.01, 0.025), "Royal", rot=(6, 0, 0), bevel=0, subd=0)
cyl((0, -0.03, 1.00), 0.185, 0.20, "Iron", taper=0.95, subd=0)                                       # great helm (bill pokes out)
cyl((0, -0.03, 1.11), 0.17, 0.03, "Iron", subd=0)
cube((0, -0.21, 1.01), (0.13, 0.01, 0.012), "Ink", bevel=0, subd=0)                                   # eye slit
cube((0, -0.03, 1.15), (0.012, 0.10, 0.05), "Royal", bevel=0.005, subd=0)                             # crest
R, L = h[-1], h[1]
limb((R[0], R[1] - 0.02, R[2] - 0.08), (R[0], R[1] - 0.12, R[2] + 0.55), 0.022, 0.012, "Iron")       # sword blade up
cube((R[0], R[1] - 0.04, R[2] + 0.02), (0.08, 0.02, 0.015), "Iron", bevel=0.005, subd=0)              # crossguard
# kite shield on the left arm
bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=0.20, radius2=0.0, depth=0.02, location=(L[0] + 0.06, L[1] - 0.06, L[2] + 0.05), rotation=(math.radians(90), 0, 0))
sh = bpy.context.object; sh.scale = (0.9, 1.4, 1); finish(sh, "Robe", 0, bevel=0.01)
cube((L[0] + 0.06, L[1] - 0.075, L[2] + 0.05), (0.022, 0.006, 0.17), "Royal", bevel=0, subd=0)
cube((L[0] + 0.06, L[1] - 0.075, L[2] + 0.10), (0.10, 0.006, 0.022), "Royal", bevel=0, subd=0)
C = export_groups({"Crusader": cc.parts}, os.path.join(OUT_DIR, "Crusader.fbx"), decimate_over=250, ratio=0.25)[0]
C.location.x = 20

# ======================= Platy-Pope =======================
cc.parts = []
h = platypus("Robe")
shell((0, 0.04, 0.55), (0.36, 0.30, 0.42), "Gold", [((0, 0.0, 0), (0, 1, 0)), ((0, 0, 0.18), (0, 0, 1))], thick=0.02)          # gold-trimmed cope behind
shell((0, 0.04, 0.55), (0.34, 0.28, 0.40), "Royal", [((0, 0.0, 0), (0, 1, 0)), ((0, 0, 0.20), (0, 0, 1))], thick=0.02)
cube((0, -0.22, 0.45), (0.06, 0.02, 0.33), "Gold", rot=(7, 0, 0), bevel=0.01, subd=0)                                           # stole
sphere((0, -0.20, 0.76), (0.05, 0.02, 0.05), "Gold", subd=0)                                                                    # pectoral medallion
# tall mitre with gold bands
bpy.ops.mesh.primitive_cone_add(vertices=24, radius1=0.16, radius2=0.075, depth=0.36, location=(0, -0.05, 1.24))
mi = bpy.context.object; mi.scale = (1, 0.62, 1); finish(mi, "Robe", 0)
cone((0, -0.05, 1.47), 0.075, 0.14, "Robe")                                     # peaked top
cube((0, -0.152, 1.24), (0.025, 0.008, 0.17), "Gold", rot=(-4, 0, 0), bevel=0, subd=0)
torus((0, -0.05, 1.08), 0.16, 0.022, "Gold", scale=(1, 0.64, 1))
sphere((0, -0.05, 1.55), (0.028, 0.028, 0.028), "Gold", subd=0)
# crozier: a gold staff curling at the top like a platypus tail fin
R = h[-1]
limb((R[0], R[1], 0.02), (R[0], R[1], 1.40), 0.02, 0.018, "Gold")
cur = []
for i in range(26):
    a = i * 0.26
    rr = 0.10 * (1 - i / 34)
    cur.append((R[0] + rr * math.sin(a) * 1.0 - 0.0, R[1], 1.40 + 0.10 - rr * math.cos(a)))
tube([(R[0], R[1], 1.40)] + cur, 0.018, "Gold")
P = export_groups({"PlatyPope": cc.parts}, os.path.join(OUT_DIR, "PlatyPope.fbx"), decimate_over=250, ratio=0.3)[0]

# ======================= preview =======================
G.location = (-1.1, 0.2, 0); P.location = (0, 0, 0); C.location = (1.1, 0.2, 0)
render_preview(OUT_PNG, (0.3, -4.4, 2.0), (0, 0.1, 0.7))
print("done")
