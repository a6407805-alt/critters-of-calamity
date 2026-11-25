# Scripted Necro-mouser miniatures (Blender 5.x, headless bpy): Skeleton, the Necromouser, the Bloat, the Vanguard and
# the Brute. Equipment is exported as "Gear_<equipment id>" meshes that the game shows only while equipped.
#   python3 necro.py <outdir> <preview.png>
import bpy, math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
bpy.ops.wm.read_factory_settings(use_empty=True)
import cc_common as cc
from cc_common import *

OUT_DIR = sys.argv[1] if len(sys.argv) > 1 else "/tmp"
OUT_PNG = sys.argv[2] if len(sys.argv) > 2 else "/tmp/necro.png"

make_materials({
    "Body":    "#7B46B8",  # Necro-mouser primary: robes, rags
    "Blanket": "#D6C9E8",  # secondary: glowing eyes, trims
    "Rot":     "#9DA88C",  # undead hide
    "Bone":    "#E8E0CC",
    "Ink":     "#2A2320",
    "Iron":    "#B8B2A6",
    "Leather": "#5A4130",
    "Wood":    "#8A6440",
    "Gold":    "#F2B83B",
    "Nose":    "#D98A8A",
})

def mouse_head(c, k, skin, hollow=False):
    """Mouse head centered at c (front -Y), big round ears. hollow = skull with dark sockets and glowing eyes."""
    x, y, z = c
    sphere((x, y, z), (0.15 * k, 0.17 * k, 0.14 * k), skin)
    cone((x, y - 0.20 * k, z - 0.02 * k), 0.09 * k, 0.18 * k, skin, rot=(95, 0, 0))                 # pointed snout
    sphere((x, y - 0.29 * k, z - 0.03 * k), (0.025 * k, 0.02 * k, 0.02 * k), "Ink" if hollow else "Nose", subd=0)
    for s in (-1, 1):
        cyl((x + s * 0.13 * k, y + 0.02 * k, z + 0.12 * k), 0.10 * k, 0.02 * k, skin, rot=(80, 0, s * 20), subd=0)   # round ears
        if hollow:
            sphere((x + s * 0.065 * k, y - 0.12 * k, z + 0.03 * k), (0.04 * k, 0.03 * k, 0.04 * k), "Ink", subd=0)
            sphere((x + s * 0.065 * k, y - 0.14 * k, z + 0.03 * k), (0.016 * k, 0.01 * k, 0.016 * k), "Blanket", subd=0)
        else:
            sphere((x + s * 0.065 * k, y - 0.13 * k, z + 0.03 * k), (0.025 * k, 0.015 * k, 0.025 * k), "Blanket", subd=0)

def tail(start, k, mat):
    pts = [start]
    for i in range(1, 12):
        t = i / 11
        pts.append((start[0] + 0.12 * k * math.sin(t * 3), start[1] + 0.30 * k * t, start[2] - 0.12 * k * t + 0.08 * k * math.sin(t * 5)))
    tube(pts, 0.012 * k, mat)

# ======================= Skeleton: a risen mouse =======================
cc.parts = []
k = 1.0
for i in range(5):                                                               # spine and ribs
    z = 0.40 + i * 0.07
    sphere((0, 0.04, z), (0.035, 0.035, 0.03), "Bone", subd=0)
    if 0 < i < 4:
        torus((0, -0.02, z), 0.12 - abs(i - 2) * 0.02, 0.012, "Bone", scale=(1, 0.75, 0.5))
sphere((0, 0.02, 0.35), (0.12, 0.09, 0.05), "Bone")                              # pelvis
for s in (-1, 1):
    limb((s * 0.08, 0.02, 0.34), (s * 0.10, -0.02, 0.05), 0.022, 0.018, "Bone")  # legs
    sphere((s * 0.10, -0.08, 0.02), (0.05, 0.09, 0.02), "Bone", subd=0)
    limb((s * 0.13, 0.0, 0.66), (s * 0.20, -0.14, 0.42), 0.018, 0.015, "Bone")  # arms
    sphere((s * 0.20, -0.16, 0.40), (0.03, 0.03, 0.03), "Bone", subd=0)
sphere((0, 0.0, 0.68), (0.14, 0.08, 0.04), "Bone", subd=0)                       # shoulders
mouse_head((0, -0.04, 0.84), 0.9, "Bone", hollow=True)
tail((0, 0.08, 0.36), 1.0, "Bone")
tube([(-0.15, -0.02, 0.70), (0, -0.12, 0.64), (0.15, -0.02, 0.70)], 0.02, "Body")   # tattered purple rag
SK = export_groups({"Skeleton": cc.parts}, os.path.join(OUT_DIR, "Skeleton.fbx"), decimate_over=250, ratio=0.3)[0]
SK.location.x = 10

# ======================= The Necromouser: hooded, skull-lantern staff =======================
cc.parts = []
cyl((0, 0.0, 0.38), 0.30, 0.76, "Body", taper=0.55, subd=1)                     # long robe
sphere((0, 0.0, 0.76), (0.19, 0.17, 0.10), "Body")
mouse_head((0, -0.08, 0.90), 0.95, "Rot")
shell((0, -0.04, 0.94), (0.21, 0.22, 0.21), "Body", [((0, -0.10, 0), (0, 1, 0)), ((0, 0, 0.78), (0, 0, 1))], thick=0.025)  # deep hood
cone((0, 0.10, 1.16), 0.10, 0.22, "Body", rot=(-40, 0, 0))                       # hood point
tube([(-0.16, -0.10, 0.74), (0, -0.20, 0.70), (0.16, -0.10, 0.74)], 0.02, "Blanket")
for s in (-1, 1):
    limb((s * 0.18, -0.02, 0.76), (s * 0.24, -0.22, 0.54), 0.07, 0.09, "Body")  # sleeves
    sphere((s * 0.24, -0.27, 0.53), (0.045, 0.045, 0.04), "Rot")
R = (-0.24, -0.29, 0.53)
limb((R[0], R[1], 0.02), (R[0], R[1], 1.30), 0.017, 0.015, "Wood")               # staff
tube([(R[0], R[1], 1.30), (R[0] + 0.06, R[1], 1.38), (R[0] + 0.13, R[1], 1.33)], 0.014, "Wood")
mouse_head((R[0] + 0.13, R[1] - 0.04, 1.22), 0.35, "Bone", hollow=True)         # dangling skull lantern
tail((0, 0.25, 0.08), 1.2, "Rot")
NM = export_groups({"Necromouser": cc.parts}, os.path.join(OUT_DIR, "Necromouser.fbx"), decimate_over=250, ratio=0.3)[0]
NM.location.x = 20

# ======================= The Bloat: swollen, stitched; Ring of Favor, Locket of Memory =======================
cc.parts = []
sphere((0, 0.0, 0.40), (0.36, 0.34, 0.36), "Rot")                                # bloated body
sphere((0, -0.14, 0.40), (0.26, 0.22, 0.26), "Blanket")                          # sickly glowing belly
for i in range(5):                                                               # stitches across the belly
    z = 0.26 + i * 0.07
    cube((0, -0.345 + 0.0 * i, z), (0.07, 0.008, 0.008), "Ink", rot=(12 - i * 6, 0, 0), bevel=0, subd=0)
tube([(0, -0.33, 0.24), (0, -0.36, 0.40), (0, -0.33, 0.56)], 0.008, "Ink")
for s in (-1, 1):
    sphere((s * 0.20, -0.05, 0.05), (0.08, 0.11, 0.05), "Rot")                  # stubby feet
    sphere((s * 0.36, -0.08, 0.40), (0.07, 0.07, 0.09), "Rot")                   # little arms
    sphere((s * 0.14 * 1.6, 0.10, 0.62), (0.05, 0.05, 0.05), "Blanket", subd=0)  # pustules
mouse_head((0, -0.12, 0.76), 0.9, "Rot")
tail((0, 0.33, 0.30), 1.0, "Rot")
BL = {"TheBloat": cc.parts}
cc.parts = []
torus((0.36, -0.13, 0.36), 0.045, 0.014, "Gold", rot=(0, 90, 0))                # Ring of Favor on the right paw
sphere((0.36, -0.17, 0.40), (0.022, 0.022, 0.022), "Body", subd=0)
BL["Gear_ring_of_favor"] = cc.parts
cc.parts = []
tube([(-0.12, -0.12, 0.66), (0, -0.30, 0.58), (0.12, -0.12, 0.66)], 0.008, "Gold")   # Locket of Memory
cyl((0, -0.31, 0.54), 0.05, 0.02, "Gold", rot=(90, 0, 0), subd=0)
BL["Gear_locket_of_memory"] = cc.parts
BLx = export_groups(BL, os.path.join(OUT_DIR, "TheBloat.fbx"), decimate_over=250, ratio=0.3)
for o in BLx: o.location.x = 30

# ======================= The Vanguard: armored skeleton; Anankian Helmet and Shield =======================
cc.parts = []
sphere((0, 0.02, 0.36), (0.13, 0.10, 0.06), "Bone")
cyl((0, 0.0, 0.53), 0.17, 0.30, "Body", taper=0.85, subd=1)                       # purple surcoat over the ribs
for s in (-1, 1):
    limb((s * 0.09, 0.02, 0.36), (s * 0.11, -0.02, 0.05), 0.03, 0.022, "Bone")
    sphere((s * 0.11, -0.09, 0.025), (0.06, 0.10, 0.025), "Bone", subd=0)
    sphere((s * 0.19, 0.0, 0.70), (0.06, 0.06, 0.05), "Iron")                   # pauldrons
    limb((s * 0.19, 0.0, 0.70), (s * 0.24, -0.16, 0.48), 0.022, 0.018, "Bone")
    sphere((s * 0.24, -0.18, 0.46), (0.035, 0.035, 0.035), "Bone", subd=0)
mouse_head((0, -0.04, 0.86), 1.0, "Bone", hollow=True)
limb((-0.24, -0.20, 0.10), (-0.24, -0.22, 1.20), 0.015, 0.015, "Wood")            # spear
cone((-0.24, -0.22, 1.27), 0.035, 0.14, "Iron")
tail((0, 0.10, 0.36), 1.0, "Bone")
VG = {"TheVanguard": cc.parts}
cc.parts = []
cyl((0, -0.03, 0.93), 0.165, 0.14, "Iron", subd=0)                                # Anankian Helmet
sphere((0, -0.03, 1.00), (0.165, 0.17, 0.09), "Iron", subd=1)
cube((0, -0.03, 1.10), (0.015, 0.14, 0.06), "Gold", bevel=0.005, subd=0)          # crest
VG["Gear_anankian_helmet"] = cc.parts
cc.parts = []
cyl((0.30, -0.22, 0.52), 0.21, 0.03, "Iron", rot=(85, 0, -15), subd=0)             # Anankian Shield
torus((0.30, -0.235, 0.52), 0.20, 0.016, "Gold", rot=(85, 0, -15))
cone((0.295, -0.26, 0.52), 0.06, 0.06, "Gold", rot=(85, 0, -15))
VG["Gear_anankian_shield"] = cc.parts
VGx = export_groups(VG, os.path.join(OUT_DIR, "TheVanguard.fbx"), decimate_over=250, ratio=0.3)
for o in VGx: o.location.x = 40

# ======================= The Brute: hulking undead rat; Anankian Sword, Leather Boots =======================
cc.parts = []
sphere((0, 0.04, 0.62), (0.34, 0.28, 0.32), "Rot", rot=(-15, 0, 0))              # massive torso
sphere((0, 0.10, 0.84), (0.36, 0.24, 0.18), "Rot", rot=(-20, 0, 0))              # hunched shoulders
sphere((0, -0.12, 0.58), (0.22, 0.14, 0.22), "Blanket")
for i in range(3):
    cube((0, -0.255, 0.50 + i * 0.07), (0.12, 0.008, 0.008), "Ink", bevel=0, subd=0)
for s in (-1, 1):
    limb((s * 0.16, 0.04, 0.40), (s * 0.18, 0.0, 0.10), 0.10, 0.07, "Rot")       # legs
    sphere((s * 0.18, -0.06, 0.04), (0.08, 0.12, 0.04), "Rot")                   # bare feet
    limb((s * 0.36, 0.0, 0.84), (s * 0.42, -0.18, 0.40), 0.09, 0.07, "Rot")       # long arms
    sphere((s * 0.42, -0.22, 0.38), (0.08, 0.08, 0.07), "Rot")
    for c in (-1, 0, 1):
        cone((s * 0.42 + c * 0.03, -0.30, 0.33), 0.012, 0.06, "Bone", rot=(130, 0, 0))   # claws
mouse_head((0, -0.32, 0.98), 1.05, "Rot")
cube((0.03, -0.47, 1.06), (0.07, 0.012, 0.012), "Ink", rot=(0, 30, 0), bevel=0, subd=0)    # scar over the eye
tube([(0.3, 0.10, 0.6), (0.0, -0.02, 0.38), (-0.3, 0.10, 0.6)], 0.02, "Body")             # purple rag sash
tail((0, 0.30, 0.42), 1.5, "Rot")
BR = {"TheBrute": cc.parts}
cc.parts = []
R = (-0.42, -0.26, 0.38)                                                          # Anankian Sword (greatsword)
limb((R[0], R[1], R[2] - 0.08), (R[0], R[1] - 0.02, R[2] + 0.14), 0.02, 0.02, "Leather")
cube((R[0], R[1] - 0.01, R[2] + 0.15), (0.11, 0.025, 0.02), "Gold", bevel=0.005, subd=0)
cube((R[0], R[1] - 0.02, R[2] + 0.52), (0.05, 0.012, 0.36), "Iron", bevel=0.004, subd=0)
cone((R[0], R[1] - 0.02, R[2] + 0.92), 0.05, 0.08, "Iron")
BR["Gear_anankian_sword"] = cc.parts
cc.parts = []
for s in (-1, 1):                                                                 # Leather Boots
    cyl((s * 0.18, -0.02, 0.10), 0.095, 0.16, "Leather", subd=0)
    sphere((s * 0.18, -0.08, 0.045), (0.095, 0.13, 0.05), "Leather", subd=0)
    torus((s * 0.18, -0.02, 0.18), 0.095, 0.015, "Gold")
BR["Gear_leather_boots"] = cc.parts
BRx = export_groups(BR, os.path.join(OUT_DIR, "TheBrute.fbx"), decimate_over=250, ratio=0.3)

# ======================= preview: everyone, gear on =======================
SK.location = (-2.1, 0.3, 0); NM.location = (-1.2, 0.1, 0)
for o in BLx: o.location = (-0.15, 0.0, 0)
for o in VGx: o.location = (0.85, 0.1, 0)
for o in BRx: o.location = (2.0, 0.3, 0)
render_preview(OUT_PNG, (0.0, -5.2, 2.2), (0, 0.2, 0.6), size=(1300, 700))
print("done")
