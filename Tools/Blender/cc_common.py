# Shared helpers for the scripted Critters of Calamity miniatures (Blender 5.x via bpy).
# Material names are roles the game recolors at runtime (see CritterModels.SculptedModel).
import bpy, bmesh, math
from mathutils import Vector
scene = bpy.context.scene

def hexc(h):
    h = h.lstrip('#'); r, g, b = (int(h[i:i+2], 16) / 255 for i in (0, 2, 4))
    lin = lambda c: c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin(r), lin(g), lin(b), 1)

MATS = {}
def make_materials(PAL):
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
    cu.bevel_depth = radius; cu.bevel_resolution = 2; cu.use_fill_caps = True
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


def export_groups(GROUPS, path, decimate_over=300, ratio=0.3):
    """Join each group into one mesh named after its key, export them all to one FBX."""
    global scene
    scene = bpy.context.scene
    exported = []
    for name, objs in GROUPS.items():
        bpy.ops.object.select_all(action='DESELECT')
        for o in objs: o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        bpy.ops.object.convert(target='MESH')
        for o in list(bpy.context.selected_objects):
            if len(o.data.polygons) > decimate_over:
                bpy.context.view_layer.objects.active = o
                dec = o.modifiers.new("Decimate", 'DECIMATE'); dec.ratio = ratio
                bpy.ops.object.modifier_apply(modifier="Decimate")
        bpy.context.view_layer.objects.active = bpy.context.selected_objects[0]
        bpy.ops.object.join()
        m = bpy.context.object; m.name = name; m.data.name = name
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)  # identity rotation/scale: the game sets those itself
        bpy.context.scene.cursor.location = (0, 0, 0)
        bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
        print(name, "tris:", sum(len(p.vertices) - 2 for p in m.data.polygons))
        exported.append(m)
    bpy.ops.object.select_all(action='DESELECT')
    for m in exported: m.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', path_mode='STRIP', bake_space_transform=True)
    return exported

def render_preview(path, cam_loc, look_at, size=(1000, 700), lens=40):
    bpy.ops.mesh.primitive_plane_add(size=12, location=(0, 0, 0))
    ground = bpy.context.object
    gm = bpy.data.materials.new("Ground"); gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = hexc("#4E7C48")
    ground.data.materials.append(gm)
    bpy.ops.object.camera_add(location=cam_loc)
    cam = bpy.context.object; bpy.context.scene.camera = cam
    cam.rotation_euler = (Vector(look_at) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.lens = lens
    bpy.ops.object.light_add(type='SUN', location=(3, -2, 5)); sun = bpy.context.object
    sun.data.energy = 3.5; sun.data.color = (1.0, 0.91, 0.78); sun.rotation_euler = (math.radians(50), 0, math.radians(25))
    world = bpy.data.worlds.new("W"); bpy.context.scene.world = world; world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = hexc("#7892AB")
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
    if "Gold" in MATS: MATS["Gold"].node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = 0.8
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.samples = 48; sc.cycles.device = 'CPU'
    sc.render.resolution_x, sc.render.resolution_y = size
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
