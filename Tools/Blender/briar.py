# Scripted Briared Flower miniatures (Blender 5.x, headless bpy): the Thorned Seed and the Blooming Briar.
# The Briar's six thorns are separate meshes named "Count_1".."Count_6"; the game shows one per Thorn it carries.
#   python3 briar.py <outdir> <preview.png>
import bpy, math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
bpy.ops.wm.read_factory_settings(use_empty=True)
import cc_common as cc
from cc_common import *
from mathutils import Vector

OUT_DIR = sys.argv[1] if len(sys.argv) > 1 else "/tmp"
OUT_PNG = sys.argv[2] if len(sys.argv) > 2 else "/tmp/briar.png"

make_materials({
    "Body":    "#C2295A",  # Briared Flower primary: petals
    "Blanket": "#5E8C31",  # secondary: vines, leaves
    "Ink":     "#2A2320",
    "Bone":    "#E8E0CC",
    "Wood":    "#8A6440",  # seed husk
    "Flame":   "#F7C948",  # pollen heart
    "Thorn":   "#8E4A2A",
})

def smooth(pts, n=6):
    """Catmull-Rom through the points so vines bend instead of kinking."""
    P = [Vector(p) for p in pts]; P = [P[0]] + P + [P[-1]]
    out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for j in range(n):
            t = j / n
            out.append(tuple(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3)))
    out.append(tuple(P[-2]))
    return out

def leaf(c, size, yaw, pitch=-25):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, location=c)
    o = bpy.context.object; o.scale = (size * 0.45, size, size * 0.08)
    o.rotation_euler = (math.radians(pitch), 0, math.radians(yaw)); finish(o, "Blanket", 1)

# ======================= Thorned Seed: a seedling scuttling on roots =======================
cc.parts = []
sphere((0, 0, 0.26), (0.17, 0.16, 0.20), "Wood")                                    # seed husk
for s in (-1, 1):
    sphere((s * 0.06, -0.15, 0.30), (0.03, 0.015, 0.035), "Ink", subd=0)             # eyes
    sphere((s * 0.055, -0.162, 0.31), (0.01, 0.005, 0.01), "Bone", subd=0)
for i in range(4):                                                                  # little root legs
    a = math.radians(45 + 90 * i)
    tube(smooth([(0.10 * math.cos(a), 0.10 * math.sin(a), 0.12), (0.19 * math.cos(a), 0.19 * math.sin(a), 0.07), (0.24 * math.cos(a), 0.24 * math.sin(a), 0.0)]), 0.022, "Blanket")
limb((0, 0, 0.44), (0.0, -0.02, 0.62), 0.022, 0.016, "Blanket")                    # sprout
leaf((-0.08, -0.01, 0.58), 0.10, 70, -20)
leaf((0.08, -0.01, 0.58), 0.10, -70, -20)
cone((0, -0.02, 0.68), 0.04, 0.10, "Body")                                          # a thorny pink bud
for a in (0, 120, 240):
    cone((0.15 * math.cos(math.radians(a)), 0.14 * math.sin(math.radians(a)), 0.30), 0.02, 0.07, "Thorn", rot=(0, 90, a))
SD = export_groups({"ThornedSeed": cc.parts}, os.path.join(OUT_DIR, "ThornedSeed.fbx"), decimate_over=250, ratio=0.25)[0]
SD.location.x = 10

# ======================= Blooming Briar: a walking bramble crowned by a sun-seeking bloom =======================
cc.parts = []
# root feet and a twisting vine trunk
for i in range(5):
    a = math.radians(90 + i * 72)
    tube(smooth([(0.08 * math.cos(a), 0.08 * math.sin(a), 0.34), (0.22 * math.cos(a), 0.22 * math.sin(a), 0.16), (0.32 * math.cos(a), 0.32 * math.sin(a), 0.04), (0.38 * math.cos(a), 0.38 * math.sin(a), 0.0)]), 0.05, "Blanket")
for j in range(3):                                                                  # three braided vines
    pts = []
    for i in range(14):
        t = i / 13
        a = t * 4.0 + j * 2.09
        pts.append((0.09 * math.cos(a) * (1 - 0.3 * t), 0.09 * math.sin(a) * (1 - 0.3 * t) + 0.04 * t, 0.12 + 0.95 * t))
    tube(smooth(pts, 3), 0.075, "Blanket")
for (z, yaw) in ((0.45, 40), (0.62, -130), (0.80, 160), (0.55, -30)):
    leaf((0.16 * math.cos(math.radians(yaw)), 0.16 * math.sin(math.radians(yaw)), z), 0.17, yaw + 90, -30)
# the bloom, tilted up toward the sun, with a toothy pollen heart
B = Vector((0, -0.05, 1.12))
tilt = math.radians(35)   # bloom faces up and forward, toward the sun
def bloom_pt(r, ang, lift):
    x = r * math.cos(ang); yy = r * math.sin(ang)
    # rotate the flower plane about X so it faces up-and-forward
    return (B.x + x, B.y + yy * math.cos(tilt) - lift * math.sin(tilt), B.z + yy * math.sin(tilt) + lift * math.cos(tilt))
for ring, (r, n, sz, lift) in enumerate(((0.26, 8, 0.15, -0.02), (0.17, 6, 0.12, 0.04))):
    for i in range(n):
        ang = i / n * 2 * math.pi + ring * 0.3
        sphere(bloom_pt(r, ang, lift), (sz, sz * 0.6, sz * 0.25), "Body", rot=(math.degrees(tilt) + 15 * (1 if ring else 0), 0, math.degrees(ang) + 90))
c = bloom_pt(0, 0, 0.06)
sphere(c, (0.12, 0.12, 0.07), "Flame", rot=(math.degrees(tilt), 0, 0))
sphere(bloom_pt(0, 0, 0.09), (0.07, 0.07, 0.04), "Ink", rot=(math.degrees(tilt), 0, 0))   # a hungry mouth in the heart
for i in range(8):
    ang = i / 8 * 2 * math.pi
    cone(bloom_pt(0.075, ang, 0.11), 0.012, 0.05, "Bone", rot=(math.degrees(tilt), 0, 0))
sphere(bloom_pt(0.05, math.radians(90), 0.13), (0.02, 0.02, 0.02), "Ink", subd=0)
BR = {"BloomingBriar": cc.parts}
# six great thorns jutting from the trunk: one per Thorn the Briar carries
for i in range(6):
    cc.parts = []
    a = math.radians(30 + i * 60)
    z = 0.38 + (i % 3) * 0.17
    base = Vector((0.07 * math.cos(a), 0.07 * math.sin(a), z))
    d = Vector((math.cos(a), math.sin(a), 0.35)).normalized()
    limb(base, base + d * 0.26, 0.045, 0.004, "Thorn")
    BR["Count_%d" % (i + 1)] = cc.parts
BRx = export_groups(BR, os.path.join(OUT_DIR, "BloomingBriar.fbx"), decimate_over=250, ratio=0.3)

SD.location = (-0.9, 0.2, 0)
c2 = SD.copy(); c2.data = SD.data; bpy.context.scene.collection.objects.link(c2); c2.location = (0.95, 0.3, 0)
for o in BRx: o.location = (0.05, 0.0, 0)
render_preview(OUT_PNG, (0.3, -3.9, 2.0), (0.0, 0.1, 0.65))
print("done")
