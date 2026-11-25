# Scripted Capybara Trader miniature for Critters of Calamity (Blender 5.x, run headless via bpy).
import bpy, bmesh, math, sys
from mathutils import Vector

OUT_FBX = sys.argv[1] if len(sys.argv) > 1 else "/tmp/CapybaraTrader.fbx"
OUT_PNG = sys.argv[2] if len(sys.argv) > 2 else "/tmp/capy.png"

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

def hexc(h):
    h = h.lstrip('#'); r, g, b = (int(h[i:i+2], 16) / 255 for i in (0, 2, 4))
    lin = lambda c: c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin(r), lin(g), lin(b), 1)

# Material names are what the game remaps to faction / Palette colors at runtime
PAL = {
    "Body":    "#9C6B3B",  # Capybara Caravan primary
    "Belly":   "#B98A5A",
    "Blanket": "#5BC0BE",  # Capybara Caravan secondary
    "Quilt":   "#2A2320",  # Palette.InkAccent
    "Ink":     "#2A2320",
    "Wood":    "#8A6440",  # Palette.WoodPlank
    "Rope":    "#E5D5B5",  # Palette.ParchmentMap
    "Gold":    "#F2B83B",  # Palette.AuthorityGold
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

# Front of the model faces -Y. Body ~1.2 long, standing ~1.0 tall.
BX, BY, BZ = 0.38, 0.62, 0.34      # body ellipsoid radii
BC = Vector((0, 0.05, 0.74))       # body center

sphere(BC, (BX, BY, BZ), "Body")                                   # barrel body
sphere(BC + Vector((0, -0.05, -0.12)), (BX * 0.82, BY * 0.85, BZ * 0.75), "Belly")

# blunt boxy head, slightly bowed, sleepy face
head = cube((0, -0.64, 0.95), (0.20, 0.25, 0.24), "Body", rot=(-12, 0, 0), bevel=0.07)
cube((0, -0.86, 0.85), (0.17, 0.09, 0.14), "Body", rot=(-12, 0, 0), bevel=0.06)        # square muzzle
for x in (-0.07, 0.07):
    sphere((x, -0.95, 0.90), (0.022, 0.012, 0.018), "Ink", subd=0)                     # nostrils
    cube((x * 1.9, -0.905, 1.06), (0.05, 0.01, 0.01), "Ink", rot=(-10, 0, -8 * math.copysign(1, x)), bevel=0.0, subd=0)  # sleepy line eyes
    cone((x * 1.7, -0.52, 1.30), 0.10, 0.26, "Body", rot=(-14, 16 * math.copysign(1, x), 0))   # big pointed ears
    cone((x * 1.7, -0.545, 1.28), 0.06, 0.18, "Belly", rot=(-14, 16 * math.copysign(1, x), 0))

# bit through the mouth, with rings, and reins back to the cart
cyl((0, -0.86, 0.80), 0.028, 0.46, "Rope", rot=(0, 90, 0))
for x in (-0.25, 0.25):
    bpy.ops.mesh.primitive_torus_add(location=(x, -0.86, 0.80), rotation=(0, math.radians(90), 0), major_radius=0.04, minor_radius=0.012)
    finish(bpy.context.object, "Gold", 0)
    s = math.copysign(1, x)
    tube([(x, -0.86, 0.80), (s * 0.40, -0.55, 0.66), (s * 0.44, 0.0, 0.62), (s * 0.40, 0.60, 0.62), (s * 0.33, 1.05, 0.55)], 0.012, "Rope")

# quilted blanket: top shell of a slightly bigger ellipsoid, rolled bolsters front and back, diamond stitching
bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, location=BC)
bl = bpy.context.object; bl.scale = (BX * 1.06, BY * 0.78, BZ * 1.07)
bpy.ops.object.transform_apply(scale=True)
bm = bmesh.new(); bm.from_mesh(bl.data)
bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, BC.z - 0.16), plane_no=(0, 0, 1), clear_inner=True)  # (transform_apply moves the mesh to world coords)
bm.to_mesh(bl.data); bm.free()
sol = bl.modifiers.new("Thick", 'SOLIDIFY'); sol.thickness = 0.03; sol.offset = 1.0
finish(bl, "Blanket", 0)
RX, RY, RZ = BX * 1.06 + 0.03, BY * 0.78 + 0.03, BZ * 1.07 + 0.03
SX, SY, SZ = BX * 1.06 + 0.035, BY * 0.78 + 0.035, BZ * 1.07 + 0.035  # where the stitches sit
from mathutils.bvhtree import BVHTree
_dg = bpy.context.evaluated_depsgraph_get()
_bvh = BVHTree.FromObject(bl, _dg)
def on_shell(x, y, lift=0.006):
    # exact blanket surface: cast a ray straight down onto the solidified shell
    hit = _bvh.ray_cast(Vector((x, BC.y + y, BC.z + 2.0)), Vector((0, 0, -1)))  # world coords
    if hit[0] is None:
        return BC.z - 1.0
    return hit[0].z + lift
def shell_point(x, y):
    return (x, BC.y + y, on_shell(x, y))
# rolled bolsters arch over the back at the blanket's front and back edges (down both sides)
for y in (-RY * 0.82, RY * 0.82):
    lim = RX * math.sqrt(1 - (y / RY) ** 2)
    pts = []
    for i in range(17):
        ang = math.pi * i / 16
        x = -lim * math.cos(ang) * 0.999
        zc = BC.z + RZ * math.sqrt(max(1 - (x / RX) ** 2 - (y / RY) ** 2, 0))
        if zc < BC.z - 0.14: continue
        pts.append((x * 1.03, BC.y + y, zc + 0.04))
    if len(pts) > 2: tube(pts, 0.075, "Blanket")
# diamond quilting stitched onto the shell
for d in (-1, 1):
    for k in range(-5, 6):
        pts = []
        for i in range(60):
            u = -1.0 + i * (2.0 / 59)
            x = u * RX
            y = (u * d * 0.6 + k * 0.17) * RY
            if abs(y) < RY * 0.76 and (x / RX) ** 2 + (y / RY) ** 2 < 0.97 and on_shell(x, y, 0) > BC.z - 0.10:
                pts.append(shell_point(x, y))
            elif len(pts) > 2:
                break
        if len(pts) > 2: tube(pts, 0.0055, "Quilt")

# sturdy legs with splayed toes
for x in (-0.22, 0.22):
    for y in (-0.38, 0.46):
        cyl((x, y, 0.28), 0.10, 0.56, "Body", taper=0.8, subd=1)
        sphere((x, y - 0.03, 0.03), (0.11, 0.13, 0.05), "Body", subd=1)
        for t in (-1, 0, 1):
            sphere((x + t * 0.05, y - 0.13, 0.025), (0.025, 0.04, 0.022), "Ink", subd=0)

# boat-shaped trade cart behind, with poles and goods
bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, location=(0, 1.25, 0.46))
cart = bpy.context.object; cart.scale = (0.40, 0.62, 0.32)
bpy.ops.object.transform_apply(scale=True)
bm = bmesh.new(); bm.from_mesh(cart.data)
bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, 0.46), plane_no=(0, 0, 1), clear_outer=True)  # keep the bottom half: a boat hull
bm.to_mesh(cart.data); bm.free()
s2 = cart.modifiers.new("Thick", 'SOLIDIFY'); s2.thickness = 0.035
finish(cart, "Wood", 1)
for x in (-0.33, 0.33):
    cyl((x, 0.72, 0.58), 0.02, 0.70, "Wood", rot=(84, 0, 0))                                # harness poles
cyl((-0.12, 1.15, 0.55), 0.08, 0.20, "Belly", taper=0.8)                                     # jar
cyl((-0.12, 1.15, 0.66), 0.05, 0.04, "Wood")
cube((0.12, 1.32, 0.51), (0.13, 0.11, 0.09), "Wood", rot=(0, 0, 12), bevel=0.015, subd=0)    # crate
cube((0.12, 1.32, 0.61), (0.14, 0.12, 0.012), "Blanket", rot=(0, 0, 12), bevel=0.0, subd=0)
cyl((0.05, 1.44, 0.67), 0.012, 0.36, "Wood", rot=(18, 0, 0))                                 # big spoon
sphere((0.05, 1.50, 0.85), (0.05, 0.03, 0.065), "Gold", subd=1)
sphere((-0.18, 1.44, 0.52), (0.06, 0.06, 0.06), "Gold", subd=1)                              # gold coin pouch

# ---- join, apply, export ----
for o in parts:
    o.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.convert(target='MESH')
for o in list(bpy.context.selected_objects):
    if not o.name.startswith("tube") and len(o.data.polygons) > 300:
        bpy.context.view_layer.objects.active = o
        dec = o.modifiers.new("Decimate", 'DECIMATE'); dec.ratio = 0.4
        bpy.ops.object.modifier_apply(modifier="Decimate")
bpy.ops.object.join()
model = bpy.context.object
model.name = "CapybaraTrader"

bpy.ops.object.origin_set(type='ORIGIN_CURSOR')  # origin at floor center between the front legs
print("tris:", sum(len(p.vertices) - 2 for p in model.data.polygons))

bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)

# ---- preview render ----
bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 0, 0))
ground = bpy.context.object
gm = bpy.data.materials.new("Ground"); gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = hexc("#4E7C48")
ground.data.materials.append(gm)
bpy.ops.object.camera_add(location=(2.6, -3.0, 1.9))
cam = bpy.context.object; scene.camera = cam
d = Vector((0, 0.3, 0.65)) - cam.location
cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
cam.data.lens = 42
bpy.ops.object.light_add(type='SUN', location=(3, -2, 5)); sun = bpy.context.object
sun.data.energy = 3.5; sun.data.color = (1.0, 0.91, 0.78); sun.rotation_euler = (math.radians(50), 0, math.radians(35))
world = bpy.data.worlds.new("W"); scene.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = hexc("#7892AB")
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
scene.render.engine = 'CYCLES'; scene.cycles.samples = 48; scene.cycles.device = 'CPU'
scene.render.resolution_x = 900; scene.render.resolution_y = 700
scene.render.filepath = OUT_PNG
bpy.ops.render.render(write_still=True)
print("done")
