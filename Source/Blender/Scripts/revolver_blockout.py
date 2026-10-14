"""
Sand House - revolver blockout generator (Blender 4.2+ / 5.x).

Builds a game-ready BLOCKOUT of a big, heavy, beat-up revolver (S&W Model 29 style, chunkier):
correct real-world scale, parts split and named the way the game expects, pivots in the right
places, rag-wrapped grip and procedural "dirty" materials. Use it as the base you refine by hand.

How to use inside Blender:  Scripting tab -> Open this file -> Run Script.
It (re)builds everything inside a "Revolver" collection and leaves the rest of your scene alone.

Conventions (match the Unity weapon code - see CONTRIBUTING.md):
  * 1 unit = 1 metre. Barrel points down Blender's -Y (becomes Unity's +Z / forward on export).
  * The root empty "SM_Revolver" sits where the shooting hand holds the gun (top of the grip).
  * "Cylinder" origin is on its spin axis (it spins 60 deg per shot in game).
  * "Hammer" and "Trigger" origins are on their pivots (for future animation).
  * "Muzzle" is an empty at the barrel tip (muzzle flash + tracers come from here).

Command line (used to build the .blend + preview):
  blender -b --python revolver_blockout.py -- --save Revolver.blend --render preview.png
"""

import math
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

# ---------------------------------------------------------------------------------------------
# Dimensions (metres). Tweak these to change the feel - e.g. BARREL_RADIUS for more girth.
# ---------------------------------------------------------------------------------------------
BORE_Z = 0.0355            # height of the barrel's centre line above the hand position
FRAME_WIDTH = 0.038
BARREL_RADIUS = 0.0118     # thick "girthy" barrel (a real Model 29 is ~0.010)
BARREL_START_Y = -0.075    # front of the frame
BARREL_LENGTH = 0.165
CYL_RADIUS = 0.0225
CYL_LENGTH = 0.048
CYL_CENTER = Vector((0.0, -0.033, 0.0215))
CHAMBER_PITCH = 0.0135     # distance from cylinder axis to each chamber
GRIP_ANGLE = 20.0          # degrees the grip rakes back
GRIP_TOP = Vector((0.0, 0.012, -0.004))
GRIP_SIZE = Vector((0.034, 0.05, 0.118))
WRAP_BANDS = 7
SEED = 7

COLLECTION = "Revolver"


# ---------------------------------------------------------------------------------------------
# Scene helpers
# ---------------------------------------------------------------------------------------------
def get_collection(name, clear=True):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    elif clear:
        for obj in list(col.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
    return col


def link(obj, col):
    for c in obj.users_collection:
        c.objects.unlink(obj)
    col.objects.link(obj)
    return obj


def bake_transform(obj):
    """Apply rotation and scale into the mesh (keeps location) - like Ctrl+A > Rotation & Scale."""
    obj.data.transform(Matrix.LocRotScale(None, obj.rotation_euler, obj.scale))
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)


def set_origin(obj, point):
    """Move the object's origin (pivot) to a world-space point without moving the mesh."""
    point = Vector(point)
    offset = point - obj.location
    obj.data.transform(Matrix.Translation(-offset))
    obj.location = point


def box(name, size, location, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=[math.radians(a) for a in rotation])
    obj = bpy.context.active_object
    obj.name = obj.data.name = name
    obj.scale = size
    bake_transform(obj)
    return obj


def cylinder(name, radius, depth, location, rotation=(90, 0, 0), vertices=32):
    """Cylinder along Blender's Y axis by default (rotation 90 on X)."""
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location,
                                        rotation=[math.radians(a) for a in rotation])
    obj = bpy.context.active_object
    obj.name = obj.data.name = name
    bake_transform(obj)
    return obj


def torus(name, major, minor, location, rotation=(0, 0, 0), scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=40, minor_segments=10,
                                     location=location, rotation=[math.radians(a) for a in rotation])
    obj = bpy.context.active_object
    obj.name = obj.data.name = name
    obj.scale = scale
    bake_transform(obj)
    return obj


def cut(target, cutter, solver="EXACT"):
    """Boolean-subtract the cutter from the target (applied), then delete the cutter."""
    mod = target.modifiers.new("Cut", "BOOLEAN")
    mod.operation = "DIFFERENCE"
    mod.solver = solver
    mod.object = cutter
    with bpy.context.temp_override(object=target, active_object=target):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter, do_unlink=True)


def merge(target, pieces):
    """Join pieces into the target mesh (all pieces should share the target's material)."""
    bm = bmesh.new()
    bm.from_mesh(target.data)
    to_local = target.matrix_world.inverted()
    for piece in pieces:
        mesh = piece.data.copy()
        mesh.transform(to_local @ piece.matrix_world)
        bm.from_mesh(mesh)
        bpy.data.meshes.remove(mesh)
        bpy.data.objects.remove(piece, do_unlink=True)
    bm.to_mesh(target.data)
    bm.free()
    return target


def bevel(obj, width, segments=2):
    mod = obj.modifiers.new("Bevel", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"
    mod.angle_limit = math.radians(35)
    obj.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL").keep_sharp = True
    return obj


def smooth(obj):
    for poly in obj.data.polygons:
        poly.use_smooth = True


def assign(obj, material):
    obj.data.materials.clear()
    obj.data.materials.append(material)
    return obj


# ---------------------------------------------------------------------------------------------
# Materials: procedural "used and abused" looks. Fine for previews; for the game, bake/paint
# textures (see the guide) because Unity can't read Blender node materials.
# ---------------------------------------------------------------------------------------------
def socket(sockets, identifier):
    for s in sockets:
        if s.identifier == identifier:
            return s
    raise KeyError(identifier)


def new_material(name):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    out.location = (600, 0)
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (300, 0)
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat, nodes, links, bsdf


def ramp(nodes, stops, location):
    node = nodes.new("ShaderNodeValToRGB")
    node.location = location
    elements = node.color_ramp.elements
    while len(elements) > len(stops):
        elements.remove(elements[-1])
    while len(elements) < len(stops):
        elements.new(0.5)
    for element, (pos, color) in zip(elements, stops):
        element.position = pos
        element.color = (*color, 1.0)
    return node


def noise(nodes, scale, detail, location):
    node = nodes.new("ShaderNodeTexNoise")
    node.location = location
    node.inputs["Scale"].default_value = scale
    node.inputs["Detail"].default_value = detail
    return node


def grime(nodes, links, base_color_socket, dirt_color, crevice_amount, location):
    """Darkens crevices (ambient occlusion) and adds blotchy dirt on top of a base colour."""
    x, y = location
    ao = nodes.new("ShaderNodeAmbientOcclusion")
    ao.location = (x, y - 200)
    ao.inputs["Distance"].default_value = 0.01
    blotch = noise(nodes, 12.0, 6.0, (x, y - 400))
    blotch_ramp = ramp(nodes, [(0.45, (0, 0, 0)), (0.7, (1, 1, 1))], (x + 180, y - 400))
    links.new(blotch.outputs["Fac"], blotch_ramp.inputs["Fac"])

    crevice = ramp(nodes, [(0.0, (1, 1, 1)), (1.0, (0, 0, 0))], (x + 180, y - 200))
    links.new(ao.outputs["AO"], crevice.inputs["Fac"])

    dirt_mask = nodes.new("ShaderNodeMath")
    dirt_mask.operation = "MAXIMUM"
    dirt_mask.location = (x + 440, y - 300)
    links.new(crevice.outputs["Color"], dirt_mask.inputs[0])
    links.new(blotch_ramp.outputs["Color"], dirt_mask.inputs[1])

    strength = nodes.new("ShaderNodeMath")
    strength.operation = "MULTIPLY"
    strength.location = (x + 600, y - 300)
    strength.inputs[1].default_value = crevice_amount
    links.new(dirt_mask.outputs["Value"], strength.inputs[0])

    mix = nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.location = (x + 780, y)
    links.new(strength.outputs["Value"], socket(mix.inputs, "Factor_Float"))
    links.new(base_color_socket, socket(mix.inputs, "A_Color"))
    socket(mix.inputs, "B_Color").default_value = (*dirt_color, 1.0)
    return socket(mix.outputs, "Result_Color")


def make_materials():
    mats = {}

    # Blued steel, worn and grimy.
    mat, nodes, links, bsdf = new_material("M_Revolver_Steel")
    n = noise(nodes, 18.0, 6.0, (-900, 0))
    base = ramp(nodes, [(0.35, (0.012, 0.014, 0.018)), (0.7, (0.04, 0.042, 0.048))], (-700, 0))
    links.new(n.outputs["Fac"], base.inputs["Fac"])
    color = grime(nodes, links, base.outputs["Color"], (0.06, 0.045, 0.03), 0.75, (-500, 0))
    links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Metallic"].default_value = 0.85
    rough = ramp(nodes, [(0.3, (0.22, 0.22, 0.22)), (0.75, (0.55, 0.55, 0.55))], (-100, -300))
    links.new(noise(nodes, 14.0, 4.0, (-300, -300)).outputs["Fac"], rough.inputs["Fac"])
    links.new(rough.outputs["Color"], bsdf.inputs["Roughness"])
    mats["steel"] = mat

    # Case-hardened / bare steel for hammer, trigger, latch, screws.
    mat, nodes, links, bsdf = new_material("M_Revolver_Worn")
    n = noise(nodes, 80.0, 6.0, (-900, 0))
    base = ramp(nodes, [(0.3, (0.18, 0.16, 0.14)), (0.7, (0.35, 0.33, 0.3))], (-700, 0))
    links.new(n.outputs["Fac"], base.inputs["Fac"])
    color = grime(nodes, links, base.outputs["Color"], (0.08, 0.06, 0.04), 0.7, (-500, 0))
    links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Metallic"].default_value = 0.85
    bsdf.inputs["Roughness"].default_value = 0.5
    mats["worn"] = mat

    # Red-brown wood grip (mostly hidden under the rag).
    mat, nodes, links, bsdf = new_material("M_Revolver_Wood")
    wave = nodes.new("ShaderNodeTexWave")
    wave.location = (-900, 0)
    wave.inputs["Scale"].default_value = 18.0
    wave.inputs["Distortion"].default_value = 6.0
    base = ramp(nodes, [(0.2, (0.22, 0.07, 0.03)), (0.8, (0.45, 0.16, 0.06))], (-700, 0))
    links.new(wave.outputs["Fac"], base.inputs["Fac"])
    color = grime(nodes, links, base.outputs["Color"], (0.1, 0.06, 0.03), 0.6, (-500, 0))
    links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.55
    mats["wood"] = mat

    # Dirty raggedy cloth wrap.
    mat, nodes, links, bsdf = new_material("M_Revolver_Rag")
    stains = noise(nodes, 9.0, 10.0, (-900, 0))
    base = ramp(nodes, [(0.3, (0.12, 0.085, 0.055)), (0.55, (0.26, 0.2, 0.13)), (0.8, (0.38, 0.32, 0.23))], (-700, 0))
    links.new(stains.outputs["Fac"], base.inputs["Fac"])
    color = grime(nodes, links, base.outputs["Color"], (0.08, 0.055, 0.035), 0.95, (-500, 0))
    links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.95
    weave = noise(nodes, 900.0, 2.0, (-300, -400))
    bump = nodes.new("ShaderNodeBump")
    bump.location = (0, -400)
    bump.inputs["Strength"].default_value = 0.4
    links.new(weave.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    mats["rag"] = mat
    return mats


# ---------------------------------------------------------------------------------------------
# Parts
# ---------------------------------------------------------------------------------------------
def build_frame(mats):
    top_z = CYL_CENTER.z + CYL_RADIUS + 0.002      # underside of the top strap
    bottom_z = CYL_CENTER.z - CYL_RADIUS - 0.002   # top of the frame under the cylinder
    back_y, front_y = 0.018, BARREL_START_Y
    window_back, window_front = CYL_CENTER.y + CYL_LENGTH / 2 + 0.002, CYL_CENTER.y - CYL_LENGTH / 2 - 0.002

    frame = box("Frame", (FRAME_WIDTH, back_y - window_back, 0.075), (0, (back_y + window_back) / 2, 0.021))
    pieces = [
        box("TopStrap", (FRAME_WIDTH * 0.8, window_back - window_front, 0.013), (0, (window_back + window_front) / 2, top_z + 0.0065)),
        box("FrameBottom", (FRAME_WIDTH * 0.92, window_back - window_front, 0.016), (0, (window_back + window_front) / 2, bottom_z - 0.008)),
        box("FrameFront", (FRAME_WIDTH * 0.95, window_front - front_y, 0.075), (0, (window_front + front_y) / 2, 0.021)),
        box("RearSight", (0.012, 0.01, 0.006), (0, window_back - 0.006, top_z + 0.016)),
        # Grip frame: the metal the grip panels bolt onto.
        box("GripFrame", (0.018, 0.03, 0.03), (0, 0.008, -0.012), rotation=(GRIP_ANGLE, 0, 0)),
    ]
    merge(frame, pieces)
    assign(frame, mats["steel"])
    bevel(frame, 0.003, 3)
    return frame


def build_barrel(mats):
    length = BARREL_LENGTH
    mid_y = BARREL_START_Y - length / 2
    tip_y = BARREL_START_Y - length

    barrel = cylinder("Barrel", BARREL_RADIUS, length, (0, mid_y, BORE_Z), vertices=48)
    cut(barrel, cylinder("Bore", BARREL_RADIUS * 0.42, 0.04, (0, tip_y, BORE_Z)))
    pieces = [
        # Ventilated rib on top and a full-length underlug (ejector shroud) underneath = chunky silhouette.
        box("Rib", (0.011, length, 0.007), (0, mid_y, BORE_Z + BARREL_RADIUS + 0.002)),
        box("Underlug", (0.02, length - 0.004, 0.019), (0, mid_y - 0.002, BORE_Z - BARREL_RADIUS - 0.002)),
        box("FrontSight", (0.004, 0.024, 0.012), (0, tip_y + 0.014, BORE_Z + BARREL_RADIUS + 0.008), rotation=(-18, 0, 0)),
    ]
    merge(barrel, pieces)
    assign(barrel, mats["steel"])
    bevel(barrel, 0.0015, 2)
    smooth(barrel)
    return barrel


def build_cylinder(mats):
    cyl = cylinder("Cylinder", CYL_RADIUS, CYL_LENGTH, CYL_CENTER, vertices=64)
    front_y = CYL_CENTER.y - CYL_LENGTH / 2

    for i in range(6):
        angle = math.radians(60 * i)
        # Chamber holes in the front face (visible from the front, shows it's loaded).
        offset = Vector((math.sin(angle), 0, math.cos(angle))) * CHAMBER_PITCH
        cut(cyl, cylinder("Chamber", 0.0058, 0.016, CYL_CENTER + offset + Vector((0, -CYL_LENGTH / 2, 0)), vertices=20))
        # Flutes between the chambers.
        flute_angle = angle + math.radians(30)
        flute_offset = Vector((math.sin(flute_angle), 0, math.cos(flute_angle))) * (CYL_RADIUS + 0.0035)
        cut(cyl, cylinder("Flute", 0.0062, CYL_LENGTH * 0.62, CYL_CENTER + flute_offset + Vector((0, 0.004, 0)), vertices=20))

    cut(cyl, cylinder("CenterPin", 0.0025, 0.01, Vector((CYL_CENTER.x, front_y, CYL_CENTER.z)), vertices=16))
    assign(cyl, mats["steel"])
    bevel(cyl, 0.0012, 2)
    smooth(cyl)
    return cyl  # origin is already on the spin axis


def build_hammer(mats):
    pivot = Vector((0, 0.013, 0.038))
    hammer = box("Hammer", (0.011, 0.016, 0.026), (0, 0.018, 0.05), rotation=(-30, 0, 0))
    spur = box("HammerSpur", (0.017, 0.024, 0.008), (0, 0.027, 0.061), rotation=(-15, 0, 0))
    merge(hammer, [spur])
    set_origin(hammer, pivot)
    assign(hammer, mats["worn"])
    bevel(hammer, 0.0012, 2)
    return hammer


def build_trigger(mats):
    pivot = Vector((0, -0.012, -0.012))
    trigger = box("Trigger", (0.0065, 0.009, 0.03), (0, -0.016, -0.026), rotation=(18, 0, 0))
    set_origin(trigger, pivot)
    assign(trigger, mats["worn"])
    bevel(trigger, 0.0015, 3)

    guard = torus("TriggerGuard", 0.02, 0.0032, (0, -0.014, -0.022), rotation=(0, 90, 0), scale=(1, 1.25, 1))
    assign(guard, mats["steel"])
    smooth(guard)
    return trigger, guard


def build_small_parts(mats):
    parts = []
    latch = box("CylinderLatch", (0.005, 0.014, 0.009), (-FRAME_WIDTH / 2 - 0.002, 0.004, 0.032))
    parts.append(latch)
    for i, (y, z) in enumerate([(0.006, 0.006), (-0.004, -0.008), (0.010, 0.042)]):
        screw = cylinder(f"Screw{i}", 0.0024, 0.003, (FRAME_WIDTH / 2 + 0.0005, y, z), rotation=(0, 90, 0), vertices=16)
        parts.append(screw)
    for part in parts:
        assign(part, mats["worn"])
    return parts


def grip_axis():
    """Unit vector running down the grip (down and back)."""
    a = math.radians(GRIP_ANGLE)
    return Vector((0, math.sin(a), -math.cos(a)))


def build_grip(mats):
    axis = grip_axis()
    center = GRIP_TOP + axis * (GRIP_SIZE.z / 2)
    grip = box("Grip", GRIP_SIZE, center, rotation=(GRIP_ANGLE, 0, 0))
    assign(grip, mats["wood"])
    bevel(grip, 0.012, 4)
    smooth(grip)
    return grip


def build_wrap(mats):
    """Dirty rag wound around the grip: overlapping, slightly crooked bands plus a loose hanging tail."""
    rng = random.Random(SEED)
    axis = grip_axis()
    bands = []
    for i in range(WRAP_BANDS):
        t = 0.12 + 0.78 * i / (WRAP_BANDS - 1) + rng.uniform(-0.03, 0.03)
        center = GRIP_TOP + axis * (GRIP_SIZE.z * t)
        # Crooked, uneven strips of flat cloth (wide along the grip, thin around it).
        tilt_x = GRIP_ANGLE + rng.uniform(-14, 14)
        tilt_y = rng.uniform(-10, 10)
        band = torus("WrapBand", 0.0255, 0.0024, center, rotation=(tilt_x, tilt_y, rng.uniform(0, 360)),
                     scale=(0.8 * rng.uniform(0.98, 1.06), 1.1 * rng.uniform(0.98, 1.06), rng.uniform(3.6, 5.0)))
        bands.append(band)

    wrap = bands[0]
    wrap.name = wrap.data.name = "GripWrap"
    merge(wrap, bands[1:])

    # Loose tail of rag hanging off the bottom of the grip.
    bottom = GRIP_TOP + axis * (GRIP_SIZE.z * 0.92) + Vector((0, 0.022, 0))
    tail = box("WrapTail", (0.016, 0.0025, 0.05), bottom + Vector((0, 0.004, -0.022)), rotation=(GRIP_ANGLE - 25, 8, 0))
    merge(wrap, [tail])

    # Make it look torn and lumpy rather than perfect rings.
    tex = bpy.data.textures.get("RagNoise") or bpy.data.textures.new("RagNoise", "CLOUDS")
    tex.noise_scale = 0.006
    disp = wrap.modifiers.new("Raggedy", "DISPLACE")
    disp.texture = tex
    disp.strength = 0.003
    disp.texture_coords = "GLOBAL"
    assign(wrap, mats["rag"])
    smooth(wrap)
    return wrap


def build_revolver():
    random.seed(SEED)
    col = get_collection(COLLECTION)
    mats = make_materials()

    # Build everything in the scene collection first (ops add there), then move into our collection.
    objects = [
        build_frame(mats),
        build_barrel(mats),
        build_cylinder(mats),
        build_hammer(mats),
        *build_trigger(mats),
        *build_small_parts(mats),
        build_grip(mats),
        build_wrap(mats),
    ]

    root = bpy.data.objects.new("SM_Revolver", None)
    root.empty_display_type = "ARROWS"
    root.empty_display_size = 0.05
    link(root, col)

    muzzle = bpy.data.objects.new("Muzzle", None)
    muzzle.empty_display_type = "SINGLE_ARROW"
    muzzle.empty_display_size = 0.03
    muzzle.location = (0, BARREL_START_Y - BARREL_LENGTH - 0.002, BORE_Z)
    muzzle.rotation_euler = (math.radians(90), 0, 0)  # arrow points out of the barrel (-Y)
    link(muzzle, col)
    objects.append(muzzle)

    for obj in objects:
        link(obj, col)
        obj.parent = root

    return root


# ---------------------------------------------------------------------------------------------
# Export + preview (command line)
# ---------------------------------------------------------------------------------------------
def export_fbx(root, path):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
    )


def setup_preview(output):
    scene = bpy.context.scene
    col = get_collection("Preview")

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.lens = 50
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    cam.location = (0.5, -0.32, 0.16)
    link(cam, col)
    target = bpy.data.objects.new("PreviewTarget", None)
    target.location = (0, -0.085, -0.005)
    link(target, col)
    track = cam.constraints.new("TRACK_TO")
    track.target = target
    scene.camera = cam

    for name, loc, energy, size in [("Key", (0.4, -0.3, 0.5), 14.0, 0.4), ("Rim", (-0.4, 0.2, 0.3), 9.0, 0.3)]:
        light_data = bpy.data.lights.new(name, "AREA")
        light_data.energy = energy
        light_data.size = size
        light = bpy.data.objects.new(name, light_data)
        light.location = loc
        light.constraints.new("TRACK_TO").target = target
        link(light, col)

    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.05, 0.05, 0.06, 1.0)

    scene.render.engine = "CYCLES"
    scene.cycles.samples = 64
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.filepath = output
    bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if bpy.app.background:
        # Command-line build: start from an empty scene (drop the default cube/camera/light).
        for obj in list(bpy.data.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
    root = build_revolver()

    def arg(flag):
        return argv[argv.index(flag) + 1] if flag in argv else None

    if arg("--fbx"):
        export_fbx(root, arg("--fbx"))
    if arg("--save"):
        bpy.ops.wm.save_as_mainfile(filepath=arg("--save"))
    if arg("--render"):
        setup_preview(arg("--render"))


if __name__ == "__main__":
    main()
