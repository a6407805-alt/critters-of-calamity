# Scripted Goat Cabal miniatures (Blender 5.x, headless bpy): the Goat cultist, the five demons, the Summoning Circle,
# and the Initiation Rite and Effigy tokens.
#   python3 goat_cabal.py <outdir> <units_preview.png> <pieces_preview.png>
import bpy, math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
bpy.ops.wm.read_factory_settings(use_empty=True)
import cc_common as cc
from cc_common import *
from mathutils import Vector

OUT_DIR = sys.argv[1] if len(sys.argv) > 1 else "/tmp"
OUT_UNITS = sys.argv[2] if len(sys.argv) > 2 else "/tmp/goat_units.png"
OUT_PIECES = sys.argv[3] if len(sys.argv) > 3 else "/tmp/goat_pieces.png"

make_materials({
    "Body":    "#4B3F5C",  # Goat Cabal primary: cult robes
    "Blanket": "#D2462F",  # secondary: sashes, sigils
    "Cloth":   "#2E2738",  # darker robe lining
    "Fur":     "#D8D0C0",  # goat fur
    "Hell":    "#7E1F22",  # demon hide
    "Ink":     "#2A2320",
    "Bone":    "#E8E0CC",  # horns, teeth
    "Gold":    "#F2B83B",
    "Iron":    "#8E8A84",
    "Wood":    "#8A6440",
    "Rope":    "#D9C58F",  # straw
    "Stone":   "#9A9CA3",
    "Leather": "#5A4130",
    "Flame":   "#F7C948",
    "Ember":   "#E8822E",
})

def smooth(pts, n=5):
    P = [Vector(p) for p in pts]; P = [P[0]] + P + [P[-1]]
    out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for j in range(n):
            t = j / n
            out.append(tuple(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3)))
    out.append(tuple(P[-2]))
    return out

def curled_horn(base, s, size, mat="Bone", turns=1.1, up=0.0):
    """A ram's horn curling back and down from base; s = side (-1 left / +1 right)."""
    pts = []
    for i in range(18):
        t = i / 17
        a = t * turns * 2 * math.pi
        r = size * (1 - 0.55 * t)
        pts.append((base[0] + s * (0.02 + size * 0.9 * t), base[1] + r * math.sin(a) * 0.9, base[2] + up * t + r * (1 - math.cos(a)) * 0.6 - size * 0.4 * t))
    tube(pts, size * 0.22, mat)

def straight_horn(base, s, length, mat="Bone", lean=25, back=20):
    tip = (base[0] + s * length * math.sin(math.radians(lean)), base[1] + length * math.sin(math.radians(back)), base[2] + length)
    limb(base, tip, length * 0.16, 0.004, mat)

def flame(c, s=1.0):
    x, y, z = c
    sphere((x, y, z + 0.03 * s), (0.04 * s, 0.04 * s, 0.045 * s), "Ember", subd=0)
    cone((x, y, z + 0.09 * s), 0.035 * s, 0.10 * s, "Ember")
    cone((x, y - 0.01 * s, z + 0.08 * s), 0.022 * s, 0.07 * s, "Flame")

def candle(c, h=0.12, r=0.025):
    cyl((c[0], c[1], c[2] + h / 2), r, h, "Bone", subd=0)
    flame((c[0], c[1], c[2] + h), 0.6)

def goat_head(c, k=1.0, fur="Fur", horns=True, eyes="Blanket"):
    """Long goat head pointing forward (-Y) at c, with beard and horns."""
    x, y, z = c
    sphere((x, y, z), (0.12 * k, 0.15 * k, 0.12 * k), fur)
    cone((x, y - 0.17 * k, z - 0.03 * k), 0.085 * k, 0.20 * k, fur, rot=(100, 0, 0))     # long muzzle
    sphere((x, y - 0.26 * k, z - 0.05 * k), (0.05 * k, 0.04 * k, 0.04 * k), fur)
    sphere((x, y - 0.295 * k, z - 0.04 * k), (0.03 * k, 0.012 * k, 0.018 * k), "Ink", subd=0)   # nose
    cone((x, y - 0.17 * k, z - 0.16 * k), 0.035 * k, 0.12 * k, fur, rot=(180, 0, 0))     # beard
    for s in (-1, 1):
        sphere((x + s * 0.075 * k, y - 0.09 * k, z + 0.04 * k), (0.03 * k, 0.015 * k, 0.018 * k), eyes, subd=0)   # glowing slit eyes
        sphere((x + s * 0.14 * k, y + 0.02 * k, z + 0.01 * k), (0.08 * k, 0.025 * k, 0.03 * k), fur, rot=(0, s * -20, s * -25))   # floppy ears
        if horns:
            curled_horn((x + s * 0.05 * k, y + 0.02 * k, z + 0.10 * k), s, 0.09 * k)

def robed(k=1.0, robe="Body", sash="Blanket", height=0.78):
    """A hooded-robe body (front -Y), returns the hand positions."""
    cyl((0, 0, height * 0.5 * k), 0.30 * k, height * k, robe, taper=0.55, subd=1)
    sphere((0, 0, height * k), (0.20 * k, 0.17 * k, 0.11 * k), robe)
    tube([(-0.15 * k, -0.14 * k, height * 0.62 * k), (0, -0.20 * k, height * 0.55 * k), (0.15 * k, -0.14 * k, height * 0.62 * k)], 0.022 * k, sash)
    hands = {}
    for s in (-1, 1):
        sh = (s * 0.20 * k, -0.02 * k, height * k); hd = (s * 0.23 * k, -0.24 * k, height * 0.72 * k)
        limb(sh, hd, 0.07 * k, 0.09 * k, robe)
        hands[s] = (hd[0], hd[1] - 0.04 * k, hd[2] - 0.01 * k)
    return hands

OUT = {}
def done(name, extra=None, ratio=0.3):
    groups = {name: cc.parts}
    if extra: groups.update(extra)
    ms = export_groups(groups, os.path.join(OUT_DIR, name + ".fbx"), decimate_over=250, ratio=ratio)
    for m in ms: m.location.x = 50 + len(OUT) * 5
    OUT[name] = ms

# ======================= Goat: a robed cultist goat holding a candle =======================
cc.parts = []
h = robed()
shell((0, -0.02, 0.92), (0.19, 0.20, 0.19), "Cloth", [((0, -0.12, 0), (0, 1, 0)), ((0, 0, 0.80), (0, 0, 1))], thick=0.025)   # hood
goat_head((0, -0.06, 0.94), 0.95)
for s in (-1, 1): sphere(h[s], (0.045, 0.045, 0.04), "Fur", subd=0)
candle((h[-1][0], h[-1][1] - 0.02, h[-1][2] + 0.02))
for s in (-1, 1): sphere((s * 0.12, -0.25, 0.03), (0.05, 0.07, 0.03), "Ink", subd=0)   # cloven hooves under the hem
done("Goat", ratio=0.22)

# ======================= Faust: a gaunt horned scholar with an open Grimoire =======================
cc.parts = []
h = robed(1.1, robe="Cloth", sash="Gold", height=0.90)
sphere((0, -0.05, 1.12), (0.13, 0.14, 0.15), "Hell")                               # long face
cone((0, -0.10, 0.96), 0.06, 0.14, "Hell", rot=(180, 0, 0))                        # pointed beard
for s in (-1, 1):
    sphere((s * 0.05, -0.17, 1.15), (0.025, 0.012, 0.016), "Flame", subd=0)
    straight_horn((s * 0.06, -0.02, 1.24), s, 0.22, lean=18, back=25)
shell((0, -0.02, 1.10), (0.17, 0.18, 0.19), "Cloth", [((0, -0.10, 0), (0, 1, 0)), ((0, 0, 0.98), (0, 0, 1))], thick=0.025)
L, R = h[1], h[-1]
book = (0.0, -0.33, 0.70)
for s in (-1, 1):
    cube((book[0] + s * 0.09, book[1], book[2]), (0.09, 0.12, 0.012), "Leather", rot=(-30, s * -12, 0), bevel=0.005, subd=0)
    cube((book[0] + s * 0.085, book[1] - 0.005, book[2] + 0.012), (0.08, 0.11, 0.006), "Bone", rot=(-30, s * -12, 0), bevel=0, subd=0)
sphere((book[0], book[1] - 0.04, book[2] + 0.10), (0.05, 0.05, 0.05), "Flame", subd=1)   # a glimmer of forbidden knowledge
limb((R[0] - 0.02, R[1], R[2] - 0.05), (R[0] - 0.08, R[1] - 0.05, R[2] + 0.22), 0.008, 0.004, "Bone")   # quill
done("Faust")

# ======================= The Midian: a gold-hoarding demon with gilded hands =======================
cc.parts = []
for i in range(14):                                                               # a spill of gold coins at its feet
    a = i * 2.4; r = 0.18 + 0.12 * (i % 3) / 2
    cyl((r * math.cos(a), -0.05 + r * math.sin(a), 0.015 + 0.012 * (i % 4)), 0.05, 0.015, "Gold", rot=(8 * (i % 3), 0, 0), subd=0)
h = robed(1.15, robe="Hell", sash="Gold", height=0.80)
sphere((0, 0.02, 0.62), (0.30, 0.27, 0.30), "Hell")                                # big belly
sphere((0, -0.02, 1.07), (0.15, 0.15, 0.14), "Hell")
for s in (-1, 1):
    sphere((s * 0.06, -0.14, 1.10), (0.028, 0.012, 0.018), "Flame", subd=0)
    curled_horn((s * 0.07, 0.0, 1.17), s, 0.08, mat="Gold")
cube((0, -0.15, 1.02), (0.06, 0.01, 0.012), "Gold", bevel=0, subd=0)               # golden grin
torus((0, -0.01, 1.19), 0.10, 0.02, "Gold")                                        # crown band
for i in range(5):
    a = i / 5 * 2 * math.pi
    cone((0.10 * math.cos(a), -0.01 + 0.10 * math.sin(a), 1.24), 0.02, 0.07, "Gold")
for s in (-1, 1): sphere(h[s], (0.06, 0.06, 0.055), "Gold", subd=1)               # everything they touch turns to gold
cyl((h[-1][0], h[-1][1] - 0.04, h[-1][2] + 0.07), 0.05, 0.015, "Gold", rot=(70, 0, 0), subd=0)   # an offered coin
sphere((0.30, 0.15, 0.12), (0.12, 0.10, 0.13), "Rope")                             # coin sack
done("TheMidian")

# ======================= Serpentine Sovereign: a crowned cobra rising from its coils =======================
cc.parts = []
coil = []
for i in range(40):                                                               # coils on the ground
    a = i * 0.42; r = 0.30 - i * 0.004
    coil.append((r * math.cos(a), r * math.sin(a), 0.07 + i * 0.006))
tube(smooth(coil[::2], 4), 0.075, "Hell")
neck = [coil[-1], (0.0, 0.05, 0.45), (0.0, -0.02, 0.70), (0.0, -0.08, 0.92), (0.0, -0.12, 1.02)]
tube(smooth(neck, 6), 0.065, "Hell")
sphere((0, -0.05, 0.84), (0.20, 0.05, 0.22), "Hell", rot=(15, 0, 0))              # cobra hood
sphere((0, -0.075, 0.84), (0.12, 0.02, 0.15), "Blanket", rot=(15, 0, 0))          # hood markings
sphere((0, -0.16, 1.04), (0.09, 0.12, 0.07), "Hell")                               # head
for s in (-1, 1):
    sphere((s * 0.05, -0.24, 1.07), (0.02, 0.012, 0.014), "Flame", subd=0)
    cone((s * 0.03, -0.25, 0.98), 0.008, 0.05, "Bone", rot=(180, 0, 0))            # fangs
tube([(0, -0.27, 1.02), (0, -0.33, 1.00), (0.015, -0.36, 1.02)], 0.006, "Blanket")    # forked tongue
torus((0, -0.15, 1.11), 0.065, 0.014, "Gold")
for i in range(5):
    a = i / 5 * 2 * math.pi
    cone((0.065 * math.cos(a), -0.15 + 0.065 * math.sin(a), 1.15), 0.014, 0.06, "Gold")
done("SerpentineSovereign")

# ======================= The Lord of War: an armored, great-horned brute with a greatsword =======================
cc.parts = []
for s in (-1, 1):
    limb((s * 0.15, 0.02, 0.55), (s * 0.17, -0.02, 0.08), 0.12, 0.09, "Hell")
    cube((s * 0.17, -0.06, 0.05), (0.08, 0.12, 0.05), "Iron", bevel=0.02, subd=0)  # iron sabatons
sphere((0, 0.0, 0.78), (0.32, 0.25, 0.32), "Hell", rot=(-10, 0, 0))
shell((0, 0.0, 0.80), (0.33, 0.26, 0.33), "Iron", [((0, -0.04, 0), (0, -1, 0)), ((0, 0, 0.58), (0, 0, 1))], thick=0.03, rot=(-10, 0, 0))   # breastplate
cube((0, -0.27, 0.82), (0.05, 0.01, 0.12), "Blanket", bevel=0, subd=0)             # war sigil
sphere((0, -0.08, 1.15), (0.14, 0.15, 0.14), "Hell")
for s in (-1, 1):
    sphere((s * 0.05, -0.21, 1.17), (0.025, 0.012, 0.016), "Flame", subd=0)
    straight_horn((s * 0.09, -0.04, 1.22), s, 0.32, lean=40, back=-5)
    sphere((s * 0.33, 0.0, 1.00), (0.13, 0.13, 0.11), "Iron")                      # spiked pauldrons
    for j in range(3):
        cone((s * (0.33 + 0.05 * (j - 1)), 0.0, 1.12), 0.025, 0.10, "Iron")
    limb((s * 0.33, 0.0, 0.98), (s * 0.40, -0.18, 0.62), 0.09, 0.08, "Hell")
    sphere((s * 0.40, -0.21, 0.58), (0.08, 0.08, 0.075), "Hell")
R = (-0.40, -0.23, 0.58)
limb((R[0], R[1], R[2] - 0.10), (R[0], R[1] - 0.02, R[2] + 0.14), 0.025, 0.025, "Leather")
cube((R[0], R[1] - 0.01, R[2] + 0.16), (0.15, 0.03, 0.03), "Iron", bevel=0.01, subd=0)
cube((R[0], R[1] - 0.02, R[2] + 0.62), (0.07, 0.015, 0.45), "Iron", bevel=0.005, subd=0)   # greatsword
cone((R[0], R[1] - 0.02, R[2] + 1.11), 0.07, 0.12, "Iron")
done("LordOfWar")

# ======================= The Horned Beast: a hulking ram-horned beast on all fours =======================
cc.parts = []
sphere((0, 0.10, 0.62), (0.36, 0.55, 0.36), "Hell")                                 # massive body
sphere((0, -0.25, 0.80), (0.38, 0.30, 0.34), "Cloth")                               # shaggy dark mane over the shoulders
for s in (-1, 1):
    for y in (-0.28, 0.45):
        limb((s * 0.24, y, 0.45), (s * 0.26, y - 0.04, 0.08), 0.12, 0.09, "Hell")
        cube((s * 0.26, y - 0.07, 0.04), (0.08, 0.09, 0.04), "Ink", bevel=0.02, subd=0)   # cloven hooves
goat_head((0, -0.55, 0.88), 1.6, fur="Hell", horns=False, eyes="Flame")
for s in (-1, 1):
    curled_horn((s * 0.08, -0.52, 1.06), s, 0.22, turns=1.25, up=0.05)              # huge ram horns
for i in range(5):
    cone((0, -0.05 + i * 0.14, 0.98 - 0.04 * i), 0.05, 0.16, "Ink", rot=(-25, 0, 0)) # spine ridges
tail = [(0, 0.62, 0.70), (0, 0.78, 0.60), (0.05, 0.86, 0.42)]
tube(smooth(tail), 0.04, "Hell")
cone((0.05, 0.88, 0.36), 0.05, 0.10, "Ink", rot=(160, 0, 0))
torus((0, -0.47, 0.70), 0.12, 0.025, "Iron", rot=(90, 0, 0))                        # a broken shackle collar
done("HornedBeast")

# ======================= Summoning Circle: a rune ring of standing stones and candles =======================
cc.parts = []
cyl((0, 0, 0.02), 0.50, 0.04, "Stone", subd=0)
torus((0, 0, 0.045), 0.40, 0.018, "Blanket")
torus((0, 0, 0.045), 0.25, 0.014, "Blanket")
for i in range(5):                                                                    # a five-pointed rune star
    a1 = math.radians(90 + i * 72); a2 = math.radians(90 + (i + 2) * 72)
    tube([(0.40 * math.cos(a1), 0.40 * math.sin(a1), 0.045), (0.40 * math.cos(a2), 0.40 * math.sin(a2), 0.045)], 0.012, "Blanket")
for i in range(5):                                                                    # standing stones
    a = math.radians(54 + i * 72)
    cube((0.46 * math.cos(a), 0.46 * math.sin(a), 0.17), (0.05, 0.04, 0.15), "Stone", rot=(0, 0, math.degrees(a)), bevel=0.02, subd=0)
for i in range(5):
    a = math.radians(90 + i * 72)
    candle((0.40 * math.cos(a), 0.40 * math.sin(a), 0.04), h=0.10, r=0.022)
cyl((0, 0, 0.08), 0.08, 0.08, "Cloth", subd=0)                                       # central altar stone
sphere((0, 0, 0.14), (0.05, 0.06, 0.05), "Bone")                                     # a goat skull on it
for s in (-1, 1): curled_horn((s * 0.03, 0.0, 0.17), s, 0.035)
done("SummoningCircle")

# ======================= Initiation Rite token: a small altar, a dagger and candles =======================
cc.parts = []
cube((0, 0, 0.10), (0.26, 0.16, 0.10), "Cloth", bevel=0.02, subd=0)
cube((0, 0, 0.205), (0.28, 0.18, 0.012), "Blanket", bevel=0, subd=0)                # altar cloth
for x in (-0.18, 0.18): candle((x, 0.02, 0.21), h=0.16, r=0.03)
limb((0.02, -0.02, 0.22), (0.02, -0.02, 0.42), 0.012, 0.012, "Leather")              # ritual dagger, point down
cube((0.02, -0.02, 0.21), (0.06, 0.012, 0.012), "Gold", bevel=0, subd=0)
cone((0.02, -0.02, 0.17), 0.02, 0.08, "Iron", rot=(180, 0, 0))
sphere((-0.05, 0.0, 0.24), (0.035, 0.035, 0.03), "Hell", subd=0)                    # an offering
done("InitiationRite")

# ======================= Effigy token: a straw goat effigy on a stake =======================
cc.parts = []
cyl((0, 0, 0.02), 0.20, 0.04, "Wood", subd=0)
limb((0, 0, 0.0), (0, 0, 0.75), 0.02, 0.018, "Wood")                                # stake
sphere((0, 0, 0.42), (0.10, 0.08, 0.16), "Rope")                                     # straw body
limb((-0.20, 0, 0.50), (0.20, 0, 0.50), 0.025, 0.025, "Rope")                        # straw arms
goat_head((0, -0.02, 0.66), 0.7, fur="Rope", horns=False, eyes="Ink")
for s in (-1, 1): straight_horn((s * 0.03, 0.0, 0.73), s, 0.10, mat="Wood", lean=35, back=10)
for z in (0.36, 0.48): torus((0, 0, z), 0.09, 0.008, "Blanket", scale=(1, 0.8, 1))   # red binding cords
done("Effigy")

# ======================= previews =======================
units = ["Goat", "Faust", "TheMidian", "SerpentineSovereign", "LordOfWar", "HornedBeast"]
for i, n in enumerate(units):
    for m in OUT[n]: m.location = ((i - 2.5) * 1.25, 0.1 if i % 2 else -0.1, 0)
for n in ("SummoningCircle", "InitiationRite", "Effigy"):
    for m in OUT[n]: m.location.x = 80
render_preview(OUT_UNITS, (0.0, -7.6, 3.0), (0, 0.0, 0.55), size=(1600, 640), lens=40)
for n in units:
    for m in OUT[n]: m.location.x = 80
for i, n in enumerate(("SummoningCircle", "InitiationRite", "Effigy")):
    for m in OUT[n]: m.location = ((i - 1) * 1.4, 0, 0)
bpy.context.scene.camera.location = (0.0, -4.4, 2.6)
bpy.context.scene.camera.rotation_euler = (Vector((0, 0, 0.2)) - Vector((0.0, -4.4, 2.6))).to_track_quat('-Z', 'Y').to_euler()
bpy.context.scene.render.resolution_x, bpy.context.scene.render.resolution_y = (1300, 600)
bpy.context.scene.render.filepath = OUT_PIECES
bpy.ops.render.render(write_still=True)
print("done")
