# Scripted Horde of the Millions miniatures (Blender 5.x, headless bpy): the Millions rabbit and the King of the Millions.
#   python3 horde.py <Millions.fbx> <KingOfTheMillions.fbx> <preview.png>
import bpy, math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
bpy.ops.wm.read_factory_settings(use_empty=True)
import cc_common as cc
from cc_common import *

OUT_M, OUT_K, OUT_PNG = (sys.argv[1:4] + ["/tmp/Millions.fbx", "/tmp/King.fbx", "/tmp/horde.png"][len(sys.argv[1:4]):])

make_materials({
    "Body":    "#D99B66",  # Horde primary
    "Blanket": "#F5E6D3",  # Horde secondary: belly, inner ears
    "Nose":    "#D98A8A",
    "Bone":    "#E8E0CC",  # teeth, cotton tail, ermine
    "Ink":     "#2A2320",
    "Royal":   "#9E2A2B",  # king's mantle
    "Gold":    "#F2B83B",
    "Carrot":  "#E8822E",
    "Leaf":    "#5E9E4B",
})

def ear(base, length, lean, tilt, s, bent=False):
    """Long ear rising from base; s = side (-1/1). Bent ears flop forward halfway up."""
    lower = length if not bent else length * 0.55
    c = (base[0] + s * 0.02, base[1], base[2] + lower * 0.5)
    sphere(c, (0.055 * length / 0.45, 0.026, lower * 0.53), "Body", rot=(tilt, s * lean, 0))
    sphere((c[0], c[1] - 0.012, c[2]), (0.032 * length / 0.45, 0.02, lower * 0.42), "Blanket", rot=(tilt, s * lean, 0))
    if bent:
        hl = length * 0.25   # upper half flops forward and down from the top of the lower half
        top = (c[0] + s * 0.01, base[1] - 0.866 * hl * 0.85, base[2] + lower - 0.5 * hl * 0.85)
        sphere(top, (0.05 * length / 0.45, 0.024, hl), "Body", rot=(120, 0, s * 8))
        sphere((top[0], top[1] - 0.012, top[2] + 0.01), (0.03 * length / 0.45, 0.02, hl * 0.8), "Blanket", rot=(120, 0, s * 8))

def rabbit(k=1.0, belly=1.0):
    """Upright sitting rabbit, front faces -Y. k scales the whole figure."""
    v = lambda x, y, z: (x * k, y * k, z * k)
    r = lambda x, y, z: (x * k, y * k, z * k)
    sphere(v(0, 0.08, 0.22), r(0.26, 0.30, 0.22), "Body")                         # haunches
    for s in (-1, 1):
        sphere(v(s * 0.15, -0.18, 0.045), r(0.07, 0.17, 0.045), "Body")           # big hind feet
        sphere(v(s * 0.15, -0.32, 0.04), r(0.05, 0.04, 0.03), "Blanket", subd=0)
    sphere(v(0, 0.0, 0.44), r(0.22 * belly, 0.20 * belly, 0.27), "Body", rot=(-12, 0, 0))
    sphere(v(0, -0.10 * belly, 0.42), r(0.16 * belly, 0.13 * belly, 0.21), "Blanket", rot=(-12, 0, 0))
    sphere(v(0, -0.09, 0.74), r(0.17, 0.17, 0.155), "Body")                       # head
    for s in (-1, 1):
        sphere(v(s * 0.075, -0.20, 0.69), r(0.08, 0.07, 0.065), "Body")           # cheeks
        sphere(v(s * 0.085, -0.215, 0.79), r(0.032, 0.022, 0.038), "Ink", subd=0) # big eyes
        sphere(v(s * 0.075, -0.235, 0.80), r(0.01, 0.006, 0.01), "Bone", subd=0)  # glint
        sphere(v(s * 0.11, -0.20, 0.49), r(0.055, 0.06, 0.05), "Body")            # front paws
        tube([v(s * 0.06, -0.265, 0.70), v(s * 0.15, -0.28, 0.72)], 0.003 * k, "Ink")   # whiskers
    sphere(v(0, -0.27, 0.72), r(0.035, 0.022, 0.025), "Nose", subd=0)
    cube(v(0, -0.255, 0.635), r(0.03, 0.008, 0.03), "Bone", bevel=0.004, subd=0)   # buck teeth
    sphere(v(0, 0.34, 0.24), r(0.08, 0.08, 0.08), "Bone")                         # cotton tail

# ======================= Millions =======================
cc.parts = []
rabbit()
ear((-0.06, -0.06, 0.84), 0.45, 10, -6, -1)
ear((0.06, -0.06, 0.84), 0.45, 10, -6, 1, bent=True)       # one floppy ear: scrappy, endless, a little sick
sphere((0.10, -0.22, 0.88), (0.05, 0.012, 0.018), "Blanket", rot=(0, 25, 0), subd=0)   # bandage over the brow
M = {"Millions": cc.parts}
millions = export_groups(M, OUT_M, decimate_over=250, ratio=0.2)[0]
millions.location.x = 10   # out of the way while the king is built

# ======================= King of the Millions =======================
cc.parts = []
K = 1.35
rabbit(K, belly=1.35)
ear((-0.07 * K, -0.07 * K, 0.85 * K), 0.50 * K, 12, -4, -1)
ear((0.07 * K, -0.07 * K, 0.85 * K), 0.50 * K, 12, -4, 1)
# royal mantle over the shoulders and back, ermine trim with black spots
shell((0, 0.04 * K, 0.50 * K), (0.30 * K, 0.27 * K, 0.36 * K), "Royal", [((0, -0.04 * K, 0), (0, 1, 0)), ((0, 0, 0.16 * K), (0, 0, 1))], thick=0.03)
arc = []   # ermine trim follows the mantle's edge from shoulder to shoulder, around the back (never across the face)
for i in range(21):
    a = math.pi * i / 20
    arc.append((0.27 * K * math.cos(a), 0.03 * K + 0.22 * K * math.sin(a), 0.56 * K + 0.05 * K * math.sin(a)))
tube(arc, 0.04 * K, "Bone")
for i in range(1, 9):
    a = math.pi * i / 9
    sphere((0.27 * K * math.cos(a) * 1.0, 0.03 * K + 0.22 * K * math.sin(a) - 0.0, 0.56 * K + 0.05 * K * math.sin(a) + 0.05 * K), (0.014, 0.014, 0.014), "Ink", subd=0)
# gold crown seated between the ears
torus((0, -0.09 * K, 0.89 * K), 0.10 * K, 0.02 * K, "Gold")
for i in range(6):
    a = i / 6 * 2 * math.pi
    cone((0.10 * K * math.cos(a), -0.09 * K + 0.10 * K * math.sin(a), 0.95 * K), 0.024 * K, 0.09 * K, "Gold")
sphere((0, -0.19 * K, 0.91 * K), (0.025, 0.016, 0.025), "Royal", subd=0)
# carrot sceptre in the right paw
px, py, pz = -0.11 * K, -0.22 * K, 0.49 * K
limb((px - 0.02, py - 0.03, 0.10), (px + 0.03, py - 0.05, 0.95 * K), 0.018, 0.016, "Gold")
cone((px + 0.035, py - 0.05, 0.95 * K + 0.09), 0.055, 0.20, "Carrot", rot=(180, 0, 0))
for s in (-1, 0, 1):
    cone((px + 0.035 + s * 0.025, py - 0.05, 0.95 * K + 0.24), 0.02, 0.12, "Leaf", rot=(0, s * 25, 0))
KG = {"KingOfTheMillions": cc.parts}
king = export_groups(KG, OUT_K, decimate_over=250, ratio=0.35)[0]

# ======================= preview: the king and three of his Millions =======================
millions.location = (-1.1, 0.4, 0)
for dx, dy in ((-0.45, 0.9), (1.0, 0.5)):
    c = millions.copy(); c.data = millions.data; bpy.context.scene.collection.objects.link(c); c.location = (dx, dy, 0)
king.location = (0.2, -0.1, 0)
render_preview(OUT_PNG, (0.6, -4.2, 2.1), (0, 0.2, 0.6))
print("done")
