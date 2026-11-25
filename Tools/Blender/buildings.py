# Scripted building miniatures for Critters of Calamity (Blender 5.x, headless bpy): War Party, Burrow, the Citadel,
# Shrine, Bunker, Wallow and Prickly Thicket. Each sits on a ~1.0 wide footprint, front faces -Y.
#   python3 buildings.py <outdir> <preview.png>
import bpy, math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
bpy.ops.wm.read_factory_settings(use_empty=True)
import cc_common as cc
from cc_common import *
from mathutils import Vector

OUT_DIR = sys.argv[1] if len(sys.argv) > 1 else "/tmp"
OUT_PNG = sys.argv[2] if len(sys.argv) > 2 else "/tmp/buildings.png"

# Body/Blanket are the owner's colors; the preview uses each faction's own.
make_materials({
    "Body":    "#C86428",
    "Blanket": "#F5C445",
    "Ink":     "#2A2320",
    "Wood":    "#8A6440",
    "Leather": "#5A4130",
    "Rope":    "#E5D5B5",
    "Stone":   "#B0B2B8",  # Palette.RuinStoneLight
    "Robe":    "#F4EEDC",  # Palette.HolyWhite
    "Gold":    "#F2B83B",
    "Earth":   "#6A4E36",
    "Grass":   "#5C8A4A",  # Palette.CanopyLight
    "Water":   "#68B5D6",  # Palette.RiverTile
    "Mud":     "#6B5138",
    "Leaf":    "#4F8F4A",
    "Carrot":  "#E8822E",
    "Thorn":   "#A4532A",
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

def tri_prism(verts, faces, mat, thick=0.0):
    me = bpy.data.meshes.new("tp"); me.from_pydata(verts, [], faces); me.update()
    o = bpy.data.objects.new("tp", me); bpy.context.scene.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    if thick:
        sm = o.modifiers.new("T", 'SOLIDIFY'); sm.thickness = thick
    return finish(o, mat, 0, bevel=0.005)

def tent(w, y0, y1, h, mat):
    """A-frame tent with its ridge along Y from y0 (front) to y1 (back)."""
    v = [(-w / 2, y0, 0), (w / 2, y0, 0), (0, y0, h), (-w / 2, y1, 0), (w / 2, y1, 0), (0, y1, h)]
    return tri_prism(v, [(0, 2, 5, 3), (1, 4, 5, 2), (3, 5, 4), (0, 1, 2)], mat, thick=0.02)

OUT = {}
def done(name):
    m = export_groups({name: cc.parts}, os.path.join(OUT_DIR, name + ".fbx"), decimate_over=300, ratio=0.4)[0]
    m.location.x = 50 + len(OUT) * 5
    OUT[name] = m

# ======================= War Party: a war tent, banner and weapon rack =======================
cc.parts = []
tent(0.62, -0.30, 0.40, 0.50, "Blanket")                                              # canvas tent (owner secondary)
tri_prism([(-0.13, -0.312, 0.0), (0.13, -0.312, 0.0), (0.0, -0.312, 0.30)], [(0, 1, 2)], "Ink", thick=0.005)  # dark doorway
for s_ in (-1, 1):
    tri_prism([(s_ * 0.0, -0.318, 0.30), (s_ * 0.13, -0.318, 0.0), (s_ * 0.20, -0.33, 0.0)], [(0, 1, 2)], "Body", thick=0.01)  # tied-back flaps
cube((0, 0.05, 0.51), (0.018, 0.40, 0.018), "Wood", bevel=0, subd=0)                   # ridge pole
limb((0, 0.42, 0), (0, 0.42, 0.55), 0.015, 0.015, "Wood")                             # back tent pole
limb((0.40, -0.20, 0), (0.40, -0.20, 0.95), 0.016, 0.014, "Wood")                    # banner pole
cube((0.52, -0.20, 0.80), (0.12, 0.008, 0.11), "Body", bevel=0, subd=0)               # banner
sphere((0.52, -0.21, 0.81), (0.045, 0.01, 0.045), "Blanket", subd=0)                  # boar sigil
for (x, z) in ((0.52, 0.69), (0.62, 0.70)):
    cone((x, -0.20, z), 0.03, 0.06, "Body", rot=(180, 0, 0))                          # swallowtail
for i in range(3):                                                                     # spear rack
    limb((-0.48 + i * 0.07, -0.22, 0), (-0.45 + i * 0.07, -0.26, 0.55), 0.01, 0.01, "Wood")
    cone((-0.45 + i * 0.07, -0.265, 0.59), 0.022, 0.07, "Stone")
cube((-0.41, -0.24, 0.30), (0.14, 0.02, 0.015), "Wood", bevel=0, subd=0)
torus((0.32, -0.48, 0.03), 0.08, 0.025, "Stone")                                       # campfire ring
for a in (30, 120):
    cyl((0.32, -0.48, 0.05), 0.015, 0.14, "Wood", rot=(0, 90, a), subd=0)
cone((0.32, -0.48, 0.10), 0.045, 0.12, "Ember")
cone((0.32, -0.49, 0.09), 0.03, 0.08, "Flame")
done("WarParty")

# ======================= Burrow: grassy earth mound with a round door =======================
cc.parts = []
sphere((0, 0.05, 0.0), (0.48, 0.44, 0.34), "Earth")
shell((0, 0.05, 0.0), (0.485, 0.445, 0.345), "Grass", [((0, 0, 0.16), (0, 0, 1))], thick=0.03)   # grassy cap
sphere((0, -0.33, 0.14), (0.15, 0.08, 0.14), "Ink")                                    # the dark tunnel mouth
torus((0, -0.36, 0.14), 0.15, 0.025, "Wood", rot=(90, 0, 0), scale=(1, 1, 0.95))       # wooden door frame
for x in (-0.24, 0.26):
    sphere((x, -0.30, 0.04), (0.07, 0.06, 0.05), "Stone", subd=0)
for i, (x, y) in enumerate(((0.30, -0.42), (0.38, -0.32), (0.33, -0.26))):            # a little carrot stash
    cone((x, y, 0.04), 0.025, 0.14, "Carrot", rot=(90, 0, 30 + i * 40))
    sphere((x + 0.07 * math.sin(math.radians(30 + i * 40)), y - 0.07 * math.cos(math.radians(30 + i * 40)), 0.04), (0.02, 0.02, 0.02), "Leaf", subd=0)
for i in range(5):                                                                     # tufts on top
    a = i * 1.3
    cone((0.25 * math.cos(a), 0.05 + 0.22 * math.sin(a), 0.30 - 0.05 * (i % 2)), 0.02, 0.09, "Leaf")
done("Burrow")

# ======================= The Citadel: white tower, gold dome, two turrets =======================
cc.parts = []
cyl((0, 0.05, 0.03), 0.48, 0.06, "Stone", subd=0)                                      # plinth
cyl((0, 0.05, 0.36), 0.24, 0.62, "Robe", subd=0)                                       # main tower
for i in range(10):                                                                    # crenellations
    a = i / 10 * 2 * math.pi
    cyl((0.235 * math.cos(a), 0.05 + 0.235 * math.sin(a), 0.70), 0.04, 0.08, "Robe", subd=0)
sphere((0, 0.05, 0.76), (0.20, 0.20, 0.20), "Gold", subd=1)                            # gold dome
cone((0, 0.05, 1.00), 0.035, 0.14, "Gold")
cube((0, 0.05, 1.10), (0.012, 0.012, 0.06), "Gold", bevel=0, subd=0)                    # spire cross
cube((0, 0.05, 1.12), (0.04, 0.012, 0.012), "Gold", bevel=0, subd=0)
cube((0, -0.19, 0.16), (0.08, 0.02, 0.13), "Ink", bevel=0.02, subd=0)                  # arched door
sphere((0, -0.19, 0.29), (0.08, 0.02, 0.06), "Ink", subd=0)
for x in (-0.32, 0.32):                                                                # side turrets with owner-colored roofs
    cyl((x, -0.05, 0.24), 0.10, 0.42, "Robe", subd=0)
    cone((x, -0.05, 0.53), 0.13, 0.20, "Body")
    cube((x, -0.15, 0.30), (0.02, 0.01, 0.05), "Ink", bevel=0, subd=0)
for i in range(2):                                                                     # windows on the tower
    cube((0, -0.20, 0.45 + i * 0.12), (0.025, 0.01, 0.04), "Ink", bevel=0, subd=0)
cube((0.0, -0.215, 0.62), (0.10, 0.008, 0.035), "Blanket", bevel=0, subd=0)            # secondary-color sash
done("TheCitadel")

# ======================= Shrine: torii gate, stone lantern and an offering box =======================
cc.parts = []
cube((0, 0.10, 0.02), (0.40, 0.30, 0.02), "Stone", bevel=0.01, subd=0)                 # stone footing
for x in (-0.24, 0.24):
    limb((x, -0.05, 0.03), (x * 0.94, -0.05, 0.62), 0.04, 0.035, "Body")              # torii pillars (owner primary)
    cyl((x, -0.05, 0.05), 0.055, 0.06, "Ink", subd=0)
cube((0, -0.05, 0.53), (0.30, 0.03, 0.025), "Body", bevel=0, subd=0)                   # nuki
curve = [(-0.40, -0.05, 0.66), (-0.20, -0.05, 0.63), (0.0, -0.05, 0.625), (0.20, -0.05, 0.63), (0.40, -0.05, 0.66)]
tube(smooth(curve), 0.035, "Body")                                                     # kasagi lintel
tube(smooth([(p[0], p[1], p[2] + 0.04) for p in curve]), 0.03, "Ink")
cube((0, -0.07, 0.59), (0.05, 0.01, 0.04), "Blanket", bevel=0, subd=0)                 # name plaque
cube((0, 0.22, 0.10), (0.11, 0.08, 0.07), "Wood", bevel=0.01, subd=0)                  # offering box
for i in range(3):
    cyl((-0.05 + i * 0.05, 0.22, 0.18 + 0.01 * i), 0.035, 0.01, "Gold", subd=0)        # coin offerings
# stone lantern
L = (0.34, 0.25, 0)
cyl((L[0], L[1], 0.03), 0.07, 0.04, "Stone", subd=0)
cyl((L[0], L[1], 0.16), 0.025, 0.22, "Stone", subd=0)
cube((L[0], L[1], 0.30), (0.06, 0.06, 0.05), "Stone", bevel=0.01, subd=0)
cube((L[0], L[1] - 0.06, 0.30), (0.025, 0.005, 0.025), "Flame", bevel=0, subd=0)
cone((L[0], L[1], 0.39), 0.10, 0.07, "Stone")
done("Shrine")

# ======================= Bunker: half-buried log dugout with a sod roof =======================
cc.parts = []
sphere((0, 0.10, -0.05), (0.50, 0.44, 0.22), "Earth")                                   # earth berm
for i in range(5):                                                                     # front log wall
    cyl((0, -0.22, 0.04 + i * 0.065), 0.035, 0.66 - i * 0.04, "Wood", rot=(0, 90, 0), subd=0)
    for x in (-0.33 + i * 0.02, 0.33 - i * 0.02):
        cyl((x, -0.22, 0.04 + i * 0.065), 0.034, 0.005, "Leather", rot=(0, 90, 0), subd=0)    # log end rings
sphere((0, 0.06, 0.26), (0.38, 0.34, 0.14), "Grass")                                 # sod roof over the logs
cube((0, -0.255, 0.20), (0.15, 0.02, 0.025), "Ink", bevel=0, subd=0)                   # firing slit
cube((-0.18, -0.25, 0.08), (0.06, 0.015, 0.08), "Ink", bevel=0, subd=0)                # low door
cyl((0.22, 0.20, 0.46), 0.04, 0.16, "Stone", subd=0)                                    # stovepipe chimney
for i, z in enumerate((0.58, 0.66, 0.75)):
    sphere((0.22 + 0.02 * i, 0.20, z), (0.04 + 0.01 * i,) * 3, "Stone", subd=0)        # smoke puffs
for (x, y) in ((-0.38, -0.30), (0.38, -0.32)):                                          # charcoal sacks
    sphere((x, y, 0.06), (0.07, 0.06, 0.07), "Leather")
    cube((x, y - 0.01, 0.07), (0.04, 0.005, 0.02), "Ink", bevel=0, subd=0)
cube((0, -0.27, 0.36), (0.05, 0.01, 0.025), "Body", bevel=0, subd=0)                    # owner badge on the lintel
done("Bunker")

# ======================= Wallow: a warm mud pool with reeds and a wash bucket =======================
cc.parts = []
cyl((0, 0.02, 0.025), 0.48, 0.05, "Mud", subd=0)                                        # muddy bank
cyl((0, 0.02, 0.045), 0.40, 0.02, "Water", subd=0)                                      # the pool
for i in range(9):                                                                      # rim stones
    a = i / 9 * 2 * math.pi
    sphere((0.45 * math.cos(a), 0.02 + 0.45 * math.sin(a), 0.06), (0.06, 0.05, 0.04), "Stone", subd=0)
for x, y, h in ((0.30, 0.30, 0.42), (0.36, 0.22, 0.34), (0.26, 0.36, 0.30), (-0.34, 0.28, 0.36)):
    limb((x, y, 0.05), (x + 0.03, y, h), 0.01, 0.006, "Leaf")                           # reeds
    cyl((x + 0.03, y, h - 0.06), 0.018, 0.08, "Leather", subd=0)                        # cattail heads
cyl((-0.12, -0.10, 0.06), 0.09, 0.01, "Leaf", subd=0)                                   # lily pad
cyl((0.10, 0.05, 0.06), 0.06, 0.01, "Leaf", subd=0)
cyl((-0.36, -0.30, 0.10), 0.08, 0.14, "Wood", taper=1.15, subd=0)                       # wash bucket
torus((-0.36, -0.30, 0.15), 0.085, 0.01, "Body")                                       # owner-colored band
cube((0.32, -0.32, 0.08), (0.10, 0.06, 0.025), "Blanket", rot=(0, 0, 20), bevel=0.02, subd=0)   # folded towel
done("Wallow")

# ======================= Prickly Thicket: a knot of thorny brambles =======================
cc.parts = []
sphere((0, 0, 0.0), (0.42, 0.40, 0.10), "Earth")
for j in range(7):
    a0 = j / 7 * 2 * math.pi
    pts = []
    for i in range(6):
        t = i / 5
        a = a0 + t * 1.6
        r = 0.08 + 0.28 * math.sin(t * math.pi * 0.9)
        pts.append((r * math.cos(a), r * math.sin(a), 0.02 + 0.55 * t * (1.1 - 0.4 * (j % 2))))
    tube(smooth(pts, 4), 0.03, "Blanket")                                                # bramble canes (owner secondary green)
    for i in range(1, 5):                                                                # thorns along each cane
        p = Vector(pts[i]); n = Vector((p.x, p.y, 0)).normalized() if p.length > 0.01 else Vector((1, 0, 0))
        limb(p, p + n * 0.08 + Vector((0, 0, 0.03)), 0.014, 0.002, "Thorn")
    tip = pts[-1]
    sphere(tip, (0.045, 0.045, 0.05), "Body")                                            # pink buds (owner primary)
for (yaw, x, y) in ((30, 0.25, -0.20), (160, -0.28, -0.10), (260, 0.05, 0.28)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, location=(x, y, 0.10))
    lf = bpy.context.object; lf.scale = (0.05, 0.11, 0.012); lf.rotation_euler = (math.radians(-20), 0, math.radians(yaw)); finish(lf, "Leaf", 1)
done("PricklyThicket")

# ======================= preview: all seven in a row, each in its faction's colors =======================
names = list(OUT)
for i, n in enumerate(names):
    OUT[n].location = ((i - 3) * 1.15, 0.2 if i % 2 else -0.2, 0)
render_preview(OUT_PNG, (0.0, -8.5, 4.2), (0, 0.0, 0.25), size=(1600, 640), lens=40)
print("done")
