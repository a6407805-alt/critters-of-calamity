# Scripted Capybara Caravan miniatures (Blender 5.x, headless bpy): the Capybara and the Riverkeeper.
# (The Capybara Trader has its own script, capybara_trader.py.)
#   python3 capybaras.py <outdir> <preview.png>
import bpy, math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
bpy.ops.wm.read_factory_settings(use_empty=True)
import cc_common as cc
from cc_common import *

OUT_DIR = sys.argv[1] if len(sys.argv) > 1 else "/tmp"
OUT_PNG = sys.argv[2] if len(sys.argv) > 2 else "/tmp/capys.png"

make_materials({
    "Body":    "#9C6B3B",  # Capybara Caravan primary
    "Belly":   "#B98A5A",
    "Blanket": "#5BC0BE",  # secondary: towel, lantern glass trim
    "Ink":     "#2A2320",
    "Wood":    "#8A6440",
    "Rope":    "#E5D5B5",  # straw hat
    "Carrot":  "#E8822E",  # the yuzu on the head
    "Leaf":    "#5E9E4B",
    "Flame":   "#F7C948",
    "Gold":    "#F2B83B",
})

def capybara(k=1.0):
    """Calm, boxy capybara standing on four legs, front faces -Y. Returns the top-of-head point."""
    v = lambda x, y, z: (x * k, y * k, z * k)
    sphere(v(0, 0.08, 0.40), (0.33 * k, 0.58 * k, 0.29 * k), "Body")                 # long barrel body, low to the ground
    sphere(v(0, 0.03, 0.32), (0.26 * k, 0.48 * k, 0.20 * k), "Belly")
    cube(v(0, -0.55, 0.56), (0.15 * k, 0.19 * k, 0.16 * k), "Body", rot=(-10, 0, 0), bevel=0.06 * k)   # blunt head
    cube(v(0, -0.72, 0.49), (0.13 * k, 0.07 * k, 0.10 * k), "Body", rot=(-10, 0, 0), bevel=0.045 * k)  # square muzzle
    for s in (-1, 1):
        sphere(v(s * 0.05, -0.79, 0.52), (0.018 * k, 0.01 * k, 0.013 * k), "Ink", subd=0)          # nostrils
        cube(v(s * 0.10, -0.74, 0.63), (0.04 * k, 0.01 * k, 0.008 * k), "Ink", rot=(-10, 0, -8 * s), bevel=0, subd=0)  # sleepy eyes
        sphere(v(s * 0.11, -0.45, 0.72), (0.045 * k, 0.028 * k, 0.038 * k), "Body")               # small round ears
        for y in (-0.32, 0.46):
            cyl(v(s * 0.19, y, 0.14), 0.085 * k, 0.28 * k, "Body", taper=0.85, subd=1)            # short legs
            sphere(v(s * 0.19, y - 0.03, 0.02), (0.09 * k, 0.11 * k, 0.04 * k), "Ink", subd=0)
    return v(0, -0.53, 0.73)

# ======================= Capybara: unbothered, with a yuzu on its head =======================
cc.parts = []
top = capybara()
sphere((top[0], top[1], top[2] + 0.06), (0.08, 0.08, 0.07), "Carrot")
cone((top[0] + 0.02, top[1], top[2] + 0.14), 0.035, 0.05, "Leaf", rot=(0, 40, 0))
CP = export_groups({"Capybara": cc.parts}, os.path.join(OUT_DIR, "Capybara.fbx"), decimate_over=250, ratio=0.22)[0]
CP.location.x = 10

# ======================= Riverkeeper: sun hat, towel, lantern pole =======================
cc.parts = []
K = 1.15
top = capybara(K)
cyl((top[0], top[1], top[2] + 0.02), 0.30, 0.025, "Rope", subd=0)                          # wide straw sun hat
cyl((top[0], top[1], top[2] + 0.09), 0.14, 0.13, "Rope", taper=0.85, subd=0)
torus((top[0], top[1], top[2] + 0.05), 0.145, 0.018, "Blanket")                            # hat band
shell((0, 0.10 * K, 0.42 * K), (0.35 * K, 0.30 * K, 0.31 * K), "Blanket", [((0, 0, 0.50 * K), (0, 0, 1))], thick=0.025)  # towel over the back
# lantern on a pole planted at the shoulder: a light for weary travellers
p0 = (0.30 * K, -0.30 * K, 0.02)
limb(p0, (0.30 * K, -0.32 * K, 1.45), 0.02, 0.018, "Wood")
tube([(0.30 * K, -0.32 * K, 1.42), (0.38 * K, -0.40 * K, 1.48), (0.46 * K, -0.46 * K, 1.42)], 0.014, "Wood")
L = (0.46 * K, -0.46 * K, 1.28)
cyl((L[0], L[1], L[2]), 0.06, 0.14, "Flame", subd=0)
cyl((L[0], L[1], L[2] + 0.08), 0.075, 0.025, "Gold", subd=0)
cyl((L[0], L[1], L[2] - 0.08), 0.075, 0.025, "Gold", subd=0)
cone((L[0], L[1], L[2] + 0.12), 0.06, 0.06, "Gold")
RK = export_groups({"Riverkeeper": cc.parts}, os.path.join(OUT_DIR, "Riverkeeper.fbx"), decimate_over=250, ratio=0.3)[0]

CP.location = (-1.0, 0.6, 0)
RK.location = (0.6, 0.0, 0)
render_preview(OUT_PNG, (1.2, -4.4, 2.2), (0.0, 0.2, 0.6))
print("done")
