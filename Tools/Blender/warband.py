# Scripted Warband miniature for Critters of Calamity (Blender 5.x, run headless via bpy).
# Curly Tail's rank-and-file boar: lean and upright, spear and round shield. Kept light since up to six share a tile.
import bpy, bmesh, math, sys
from mathutils import Vector

OUT_FBX = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else (sys.argv[1] if len(sys.argv) > 1 else "/tmp/Warband.fbx")
OUT_PNG = sys.argv[sys.argv.index("--") + 2] if "--" in sys.argv else (sys.argv[2] if len(sys.argv) > 2 else "/tmp/warband.png")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

def hexc(h):
    h = h.lstrip('#'); r, g, b = (int(h[i:i+2], 16) / 255 for i in (0, 2, 4))
    lin = lambda c: c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin(r), lin(g), lin(b), 1)

# Material names are what the game remaps to faction / Palette colors at runtime
PAL = {
    "Body":    "#C86428",  # Curly Tail's Warband primary
    "Belly":   "#D9895A",  # lighter hide (game: primary toward parchment)
    "Blanket": "#F5C445",  # Warband secondary: belt buckle, cloak trim
    "Cloth":   "#5E2E16",  # war cloak (game: primary darkened toward ink)
    "Leather": "#5A4130",  # belt, straps
    "Bone":    "#E8E0CC",  # Palette.BoneWhite: tusks
    "Ink":     "#2A2320",  # Palette.InkAccent: eyes, bristles, hooves
    "Wood":    "#8A6440",  # Palette.WoodPlank: axe haft
    "Gold":    "#F2B83B",  # Palette.AuthorityGold: forged Golden Arms and Armor
}
MATS = {}
for name, hx in PAL.items():
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = hexc(hx)
    bsdf.inputs["Roughness"].default_value = 0.6 if name not in ("Gold", "Blanket") else 0.4
    MATS[name] = m

parts = []
def finish(obj, mat, subd=2, smooth=True, bevel=0.0):
    obj.data.materials.append(MATS[mat])
    if bevel > 0:
        b = obj.modifiers.new("Bevel", 'BEVEL'); b.width = bevel; b.segments = 3
    if subd:
        s = obj.modifiers.new("Subd", 'SUBSURF'); s.levels = subd; s.render_levels = subd
    if smooth and obj.type == 'MESH':
        for p in obj.data.polygons: p.use_smooth = True
    parts.append(obj)
    return obj

def sphere(loc, scale, mat, subd=1, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, location=loc, rotation=[math.radians(a) for a in rot])
    o = bpy.context.object; o.scale = scale; return finish(o, mat, subd)

def cube(loc, scale, mat, rot=(0, 0, 0), bevel=0.04, subd=1):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=[math.radians(a) for a in rot])
    o = bpy.context.object; o.scale = scale
    bpy.ops.object.transform_apply(scale=True)
    return finish(o, mat, subd, bevel=bevel)

def cyl(loc, radius, depth, mat, rot=(0, 0, 0), verts=20, subd=0, taper=1.0):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=radius, radius2=radius * taper, depth=depth,
                                    location=loc, rotation=[math.radians(a) for a in rot])
    o = bpy.context.object; return finish(o, mat, subd, bevel=min(0.02, radius * 0.3))

def cone(loc, r, depth, mat, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(vertices=16, radius1=r, radius2=0.0, depth=depth, location=loc,
                                    rotation=[math.radians(a) for a in rot])
    return finish(bpy.context.object, mat, 0)

def tube(points, radius, mat):
    cu = bpy.data.curves.new("tube", 'CURVE'); cu.dimensions = '3D'
    cu.bevel_depth = radius; cu.bevel_resolution = 2
    sp = cu.splines.new('POLY'); sp.points.add(len(points) - 1)
    for i, p in enumerate(points): sp.points[i].co = (p[0], p[1], p[2], 1)
    o = bpy.data.objects.new("tube", cu); scene.collection.objects.link(o)
    o.data.materials.append(MATS[mat]); parts.append(o); return o

def torus(loc, R, r, mat, rot=(0, 0, 0), scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_torus_add(location=loc, rotation=[math.radians(a) for a in rot], major_radius=R, minor_radius=r,
                                     major_segments=32, minor_segments=10)
    o = bpy.context.object; o.scale = scale; return finish(o, mat, 0)

def shell(loc, radii, mat, keep, thick=0.03, rot=(0, 0, 0)):
    """Part of an ellipsoid surface: keep(plane_co, plane_no) list of planes; everything on the + side of all is kept."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=40, ring_count=20, location=loc, rotation=[math.radians(a) for a in rot])
    o = bpy.context.object; o.scale = radii
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bm = bmesh.new(); bm.from_mesh(o.data)
    for co, no in keep:
        co = Vector(co) - Vector(loc)   # mesh is still in local coords (location not applied)
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=co, plane_no=no, clear_inner=True)
    bm.to_mesh(o.data); bm.free()
    s = o.modifiers.new("Thick", 'SOLIDIFY'); s.thickness = thick; s.offset = 1.0
    return finish(o, mat, 1)

def limb(a, b, r1, r2, mat):
    """Tapered cylinder from point a to point b."""
    a, b = Vector(a), Vector(b)
    d = b - a
    bpy.ops.mesh.primitive_cone_add(vertices=20, radius1=r1, radius2=r2, depth=d.length, location=(a + b) / 2)
    o = bpy.context.object
    o.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
    return finish(o, mat, 0)

# ======================= Warband boar (front faces -Y, ~1.25 tall) =======================
GROUPS = {}
parts = []

for x in (-0.15, 0.15):                                                          # legs and hooves
    limb((x, 0.03, 0.55), (x * 1.1, -0.01, 0.10), 0.12, 0.08, "Body")
    sphere((x * 1.05, 0.0, 0.43), (0.13, 0.13, 0.15), "Body", subd=0)
    cyl((x * 1.1, -0.03, 0.06), 0.09, 0.12, "Ink", taper=0.9)

sphere((0, 0.02, 0.78), (0.30, 0.26, 0.32), "Body", rot=(-10, 0, 0))            # body
sphere((0, -0.11, 0.72), (0.23, 0.18, 0.25), "Belly", rot=(-10, 0, 0))
sphere((0, 0.08, 1.00), (0.30, 0.22, 0.18), "Body", rot=(-18, 0, 0))            # shoulders

sphere((0, -0.22, 1.08), (0.17, 0.21, 0.17), "Body", rot=(-8, 0, 0))            # head
cyl((0, -0.41, 1.03), 0.09, 0.17, "Body", rot=(96, 0, 0), taper=0.85)            # snout
cyl((0, -0.495, 1.02), 0.08, 0.025, "Belly", rot=(96, 0, 0))
for s in (-1, 1):
    sphere((s * 0.028, -0.51, 1.02), (0.017, 0.008, 0.024), "Ink", subd=0)
    sphere((s * 0.08, -0.36, 1.14), (0.026, 0.016, 0.022), "Ink", subd=0)        # eyes
    cube((s * 0.08, -0.36, 1.175), (0.055, 0.02, 0.016), "Ink", rot=(0, -s * 18, 0), bevel=0.006, subd=0)
    tube([(s * 0.06, -0.44, 0.97), (s * 0.12, -0.48, 1.00), (s * 0.14, -0.48, 1.07)], 0.017, "Bone")  # small tusks
    cone((s * 0.13, -0.10, 1.22), 0.06, 0.15, "Body", rot=(-50, s * 30, 0))      # ears
for i in range(8):                                                               # short bristle crest
    t = i / 7
    cone((0, -0.18 + t * 0.45, 1.25 - 0.18 * t), 0.03, 0.11, "Ink", rot=(-40 - 40 * t, 0, 0))

# leather war cap with a Warband-colored crest
shell((0, -0.20, 1.10), (0.185, 0.215, 0.18), "Leather", [((0, 0, 1.185), (0, 0, 1))], thick=0.02)
cube((0, -0.18, 1.29), (0.02, 0.12, 0.04), "Blanket", bevel=0.01, subd=0)

# arms
SH = {-1: (-0.30, -0.02, 0.97), 1: (0.30, -0.02, 0.97)}
FIST = {-1: (-0.36, -0.22, 0.62), 1: (0.34, -0.24, 0.66)}
for s in (-1, 1):
    el = ((SH[s][0] + FIST[s][0]) / 2 + s * 0.04, -0.08, 0.80)
    sphere(SH[s], (0.11, 0.11, 0.11), "Body", subd=0)
    limb(SH[s], el, 0.085, 0.075, "Body")
    limb(el, FIST[s], 0.075, 0.07, "Body")
    sphere(FIST[s], (0.075, 0.075, 0.07), "Body", subd=0)

# spear in the right fist, planted at the foot
R = FIST[-1]
limb((R[0], R[1], 0.02), (R[0], R[1] - 0.02, 1.55), 0.018, 0.018, "Wood")
cone((R[0], R[1] - 0.02, 1.62), 0.04, 0.16, "Ink")
tube([(R[0], R[1] - 0.02, 1.48), (R[0] + 0.03, R[1] - 0.03, 1.43)], 0.012, "Blanket")   # pennant tie

# round shield on the left arm, painted in the Warband secondary with a dark boss
L = FIST[1]
cyl((L[0] + 0.07, L[1] - 0.13, L[2] + 0.06), 0.20, 0.035, "Blanket", rot=(80, 0, -20))
cyl((L[0] + 0.07, L[1] - 0.15, L[2] + 0.06), 0.06, 0.05, "Ink", rot=(80, 0, -20))
torus((L[0] + 0.07, L[1] - 0.13, L[2] + 0.06), 0.20, 0.018, "Wood", rot=(80, 0, -20))

# belt and the curly tail
torus((0, 0.02, 0.60), 0.28, 0.025, "Leather", rot=(-8, 0, 0), scale=(1, 0.88, 1))
tail = [(0.0, 0.27, 0.66)]
for i in range(1, 18):
    a = i * 0.6
    rr = 0.045 * (1 - i / 26)
    tail.append((rr * math.cos(a), 0.30 + i * 0.005, 0.72 + rr * math.sin(a)))
tube(tail, 0.017, "Body")

GROUPS["Warband"] = parts
# ======================= join, export =======================
exported = []
for name, objs in GROUPS.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.convert(target='MESH')
    for o in list(bpy.context.selected_objects):
        if len(o.data.polygons) > 250:
            bpy.context.view_layer.objects.active = o
            dec = o.modifiers.new("Decimate", 'DECIMATE'); dec.ratio = 0.25
            bpy.ops.object.modifier_apply(modifier="Decimate")
    bpy.context.view_layer.objects.active = bpy.context.selected_objects[0]
    bpy.ops.object.join()
    m = bpy.context.object; m.name = name; m.data.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)  # identity rotation/scale: the game sets those itself
    scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    print(name, "tris:", sum(len(p.vertices) - 2 for p in m.data.polygons))
    exported.append(m)

bpy.ops.object.select_all(action='DESELECT')
for m in exported: m.select_set(True)
bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)

# ======================= preview: a squad of three =======================
base = exported[0]
for dx in (-0.9, 0.9):
    c = base.copy(); c.data = base.data; scene.collection.objects.link(c); c.location.x = dx; c.location.y = 0.35
bpy.ops.mesh.primitive_plane_add(size=10, location=(0, 0, 0))
ground = bpy.context.object
gm = bpy.data.materials.new("Ground"); gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = hexc("#4E7C48")
ground.data.materials.append(gm)
bpy.ops.object.camera_add(location=(0.9, -4.4, 2.2))
cam = bpy.context.object; scene.camera = cam
d = Vector((0, 0.1, 0.75)) - cam.location
cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
cam.data.lens = 40
bpy.ops.object.light_add(type='SUN', location=(3, -2, 5)); sun = bpy.context.object
sun.data.energy = 3.5; sun.data.color = (1.0, 0.91, 0.78); sun.rotation_euler = (math.radians(50), 0, math.radians(25))
world = bpy.data.worlds.new("W"); scene.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = hexc("#7892AB")
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
scene.render.engine = 'CYCLES'; scene.cycles.samples = 48; scene.cycles.device = 'CPU'
scene.render.resolution_x = 1000; scene.render.resolution_y = 700
scene.render.filepath = OUT_PNG
bpy.ops.render.render(write_still=True)
print("done")
