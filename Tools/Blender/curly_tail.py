# Scripted Curly Tail miniature for Critters of Calamity (Blender 5.x, run headless via bpy).
# The boar warlord plus his five Golden Arms and Armor as separate "Gear_<equipment id>" meshes,
# which the game shows only while that item is equipped.
import bpy, bmesh, math, sys
from mathutils import Vector

OUT_FBX = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else (sys.argv[1] if len(sys.argv) > 1 else "/tmp/CurlyTail.fbx")
OUT_PNG = sys.argv[sys.argv.index("--") + 2] if "--" in sys.argv else (sys.argv[2] if len(sys.argv) > 2 else "/tmp/curly.png")

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

# ======================= Curly Tail (front faces -Y, ~1.5 tall) =======================
GROUPS = {}
parts = []

# stocky hind legs and cloven hooves
for x in (-0.21, 0.21):
    limb((x, 0.04, 0.66), (x * 1.1, -0.02, 0.10), 0.16, 0.10, "Body")
    sphere((x * 1.05, 0.0, 0.50), (0.17, 0.17, 0.20), "Body")                       # thigh bulk
    cyl((x * 1.1, -0.04, 0.07), 0.115, 0.14, "Ink", taper=0.9)                       # hoof

# barrel torso leaning forward, belly, and the great shoulder hump
sphere((0, 0.02, 0.88), (0.40, 0.34, 0.40), "Body", rot=(-14, 0, 0))
sphere((0, -0.14, 0.80), (0.31, 0.24, 0.32), "Belly", rot=(-14, 0, 0))
sphere((0, 0.10, 1.16), (0.42, 0.30, 0.26), "Body", rot=(-20, 0, 0))

# head thrust forward and low under the hump
sphere((0, -0.30, 1.20), (0.21, 0.26, 0.21), "Body", rot=(-10, 0, 0))
cyl((0, -0.53, 1.13), 0.115, 0.22, "Body", rot=(96, 0, 0), taper=0.85)            # snout
cyl((0, -0.645, 1.115), 0.10, 0.03, "Belly", rot=(96, 0, 0))                       # nose disc
for x in (-0.035, 0.035):
    sphere((x, -0.665, 1.115), (0.022, 0.01, 0.03), "Ink", subd=0)                  # nostrils
for s in (-1, 1):
    # angry eyes under heavy brows
    sphere((s * 0.10, -0.47, 1.28), (0.032, 0.02, 0.026), "Ink", subd=0)
    cube((s * 0.10, -0.47, 1.325), (0.07, 0.025, 0.02), "Ink", rot=(0, -s * 22, 0), bevel=0.008, subd=0)
    # tusks: thick curves from the jaw, up and outward
    tube([(s * 0.08, -0.56, 1.07), (s * 0.15, -0.62, 1.10), (s * 0.19, -0.64, 1.18), (s * 0.18, -0.60, 1.26)], 0.024, "Bone")
    cone((s * 0.18, -0.60, 1.29), 0.024, 0.07, "Bone", rot=(-15, s * 10, 0))
    # torn ears, laid back
    cone((s * 0.17, -0.18, 1.38), 0.08, 0.20, "Body", rot=(-50, s * 30, 0))
    cone((s * 0.17, -0.195, 1.37), 0.05, 0.13, "Belly", rot=(-50, s * 30, 0))

# bristle mane from brow over the hump and down the spine
for i in range(14):
    t = i / 13
    y = -0.30 + t * 0.75
    z = 1.38 - 0.10 * t + 0.10 * math.sin(t * math.pi) - 0.45 * max(0, t - 0.55)
    cone((0, y, z), 0.045 - 0.015 * t, 0.16, "Ink", rot=(-35 - 40 * t, 0, 0))

# thick arms hanging forward, big fists
SH = {-1: (-0.42, -0.02, 1.10), 1: (0.42, -0.02, 1.10)}
FIST = {-1: (-0.47, -0.26, 0.66), 1: (0.47, -0.24, 0.66)}
for s in (-1, 1):
    el = ((SH[s][0] + FIST[s][0]) / 2 + s * 0.06, -0.08, 0.88)
    sphere(SH[s], (0.15, 0.15, 0.15), "Body")
    limb(SH[s], el, 0.12, 0.10, "Body")
    limb(el, FIST[s], 0.10, 0.09, "Body")
    sphere(FIST[s], (0.10, 0.10, 0.09), "Body")
    for k in (-1, 0, 1):
        sphere((FIST[s][0] + k * 0.04, FIST[s][1] - 0.08, FIST[s][2] - 0.04), (0.022, 0.02, 0.03), "Ink", subd=0)

# war cloak over the hump and down the back, with a secondary-color trim and a fur collar
shell((0, 0.06, 1.02), (0.47, 0.38, 0.45), "Cloth", [((0, 0.02, 0), (0, 1, 0)), ((0, 0, 0.42), (0, 0, 1))], thick=0.03)
torus((0, 0.02, 1.20), 0.36, 0.06, "Belly", rot=(-18, 0, 0), scale=(1.05, 0.8, 1))
tube([(-0.44, 0.0, 1.05), (-0.46, 0.05, 0.80), (-0.45, 0.10, 0.58)], 0.018, "Blanket")
tube([(0.44, 0.0, 1.05), (0.46, 0.05, 0.80), (0.45, 0.10, 0.58)], 0.018, "Blanket")

# belt with a gold-colored warband buckle
torus((0, 0.02, 0.66), 0.37, 0.035, "Leather", rot=(-10, 0, 0), scale=(1, 0.86, 1))
cube((0, -0.31, 0.63), (0.07, 0.02, 0.055), "Blanket", rot=(-10, 0, 0), bevel=0.01, subd=0)

# the curly tail, of course
tail = [(0.0, 0.36, 0.72)]
for i in range(1, 22):
    a = i * 0.55
    rr = 0.06 * (1 - i / 30)
    tail.append((rr * math.cos(a), 0.40 + i * 0.006, 0.80 + rr * math.sin(a)))
tube(tail, 0.022, "Body")

GROUPS["CurlyTail"] = parts

# ======================= Golden Arms and Armor (each its own mesh) =======================
R, L = FIST[-1], FIST[1]

parts = []   # Golden Sword in the right fist, blade raised
cyl((R[0], R[1] - 0.02, R[2]), 0.035, 0.24, "Leather", rot=(0, 0, 0))               # grip through the fist
cube((R[0], R[1] - 0.02, R[2] + 0.15), (0.13, 0.03, 0.025), "Gold", bevel=0.01, subd=0)  # crossguard
cube((R[0], R[1] - 0.02, R[2] + 0.50), (0.045, 0.012, 0.33), "Gold", bevel=0.006, subd=0)
cone((R[0], R[1] - 0.02, R[2] + 0.88), 0.045, 0.10, "Gold")
sphere((R[0], R[1] - 0.02, R[2] - 0.15), (0.04, 0.04, 0.04), "Gold", subd=0)        # pommel
GROUPS["Gear_golden_sword"] = parts

parts = []   # Golden War Axe strapped diagonally across the back
a0, a1 = Vector((-0.30, 0.42, 0.62)), Vector((0.34, 0.40, 1.50))
limb(a0, a1, 0.028, 0.028, "Wood")
for s in (-1, 1):
    hb = a1 - (a1 - a0).normalized() * 0.14
    cube((hb.x + s * 0.13, hb.y + 0.02, hb.z - s * 0.09), (0.13, 0.02, 0.11), "Gold", rot=(0, -37 - s * 0, 0), bevel=0.02, subd=0)
tube([(-0.40, -0.05, 1.12), (-0.20, -0.30, 0.95), (0.20, -0.30, 0.85), (0.40, 0.05, 0.75)], 0.016, "Leather")  # strap across the chest
GROUPS["Gear_golden_war_axe"] = parts

parts = []   # Golden Gauntlet over the left fist
sphere((L[0], L[1] - 0.005, L[2]), (0.12, 0.12, 0.11), "Gold", subd=1)
cyl((L[0] - 0.02, L[1] + 0.01, L[2] + 0.12), 0.11, 0.12, "Gold", rot=(-15, 0, 10), taper=1.15)
for k in (-1, 0, 1):
    cone((L[0] + k * 0.05, L[1] - 0.12, L[2] - 0.02), 0.02, 0.07, "Gold", rot=(90, 0, 0))   # knuckle spikes
GROUPS["Gear_golden_gauntlet"] = parts

parts = []   # Golden Chest Plate over the belly, with pauldrons
shell((0, 0.02, 0.88), (0.415, 0.355, 0.415), "Gold", [((0, -0.12, 0), (0, -1, 0)), ((0, 0, 0.74), (0, 0, 1))], thick=0.03, rot=(-14, 0, 0))
for s in (-1, 1):
    shell((s * 0.42, -0.02, 1.13), (0.18, 0.18, 0.16), "Gold", [((0, 0, 1.08), (0, 0, 1))], thick=0.025)
    sphere((s * 0.42, -0.02, 1.30), (0.03, 0.03, 0.03), "Gold", subd=0)
cube((0, -0.40, 0.95), (0.03, 0.01, 0.12), "Blanket", rot=(-14, 0, 0), bevel=0, subd=0)   # center rib
GROUPS["Gear_golden_chestplate"] = parts

parts = []   # Tyrant's Crown between the ears
torus((0, -0.24, 1.40), 0.13, 0.025, "Gold", rot=(-12, 0, 0))
for i in range(7):
    ang = i / 7 * 2 * math.pi
    x, y = 0.13 * math.cos(ang), 0.13 * math.sin(ang)
    cone((x, -0.24 + y * 0.98, 1.47 + y * 0.2), 0.03, 0.13 + 0.03 * (i % 2), "Gold", rot=(-12, 0, 0))
sphere((0, -0.37, 1.43), (0.03, 0.02, 0.03), "Blanket", subd=0)                     # jewel
GROUPS["Gear_tyrants_crown"] = parts

# ======================= join, export =======================
exported = []
for name, objs in GROUPS.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.convert(target='MESH')
    for o in list(bpy.context.selected_objects):
        if len(o.data.polygons) > 400:
            bpy.context.view_layer.objects.active = o
            dec = o.modifiers.new("Decimate", 'DECIMATE'); dec.ratio = 0.45
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

# ======================= preview: bare (left) and fully forged (right) =======================
gear = [m for m in exported if m.name.startswith("Gear_")]
bare = bpy.data.objects["CurlyTail"].copy(); bare.data = bare.data.copy(); scene.collection.objects.link(bare)
bare.location.x = -1.25
for m in exported: m.location.x = 0.55
bpy.ops.mesh.primitive_plane_add(size=10, location=(0, 0, 0))
ground = bpy.context.object
gm = bpy.data.materials.new("Ground"); gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = hexc("#4E7C48")
ground.data.materials.append(gm)
bpy.ops.object.camera_add(location=(0.6, -4.6, 2.2))
cam = bpy.context.object; scene.camera = cam
d = Vector((-0.35, 0, 0.82)) - cam.location
cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
cam.data.lens = 40
bpy.ops.object.light_add(type='SUN', location=(3, -2, 5)); sun = bpy.context.object
sun.data.energy = 3.5; sun.data.color = (1.0, 0.91, 0.78); sun.rotation_euler = (math.radians(50), 0, math.radians(25))
world = bpy.data.worlds.new("W"); scene.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = hexc("#7892AB")
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
for m in bpy.data.materials:
    if m.name == "Gold":
        m.node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = 0.8
scene.render.engine = 'CYCLES'; scene.cycles.samples = 48; scene.cycles.device = 'CPU'
scene.render.resolution_x = 1100; scene.render.resolution_y = 750
scene.render.filepath = OUT_PNG
bpy.ops.render.render(write_still=True)
print("done")
