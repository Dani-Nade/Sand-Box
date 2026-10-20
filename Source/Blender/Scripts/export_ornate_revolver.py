"""
Exports Ornate_Revolver_Inspect.blend to the Unity project as an FBX (model + inspect animation).

Run from the repo root (Blender does not need to be open):
    "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b Source/Blender/Ornate_Revolver_Inspect.blend \
        --python Source/Blender/Scripts/export_ornate_revolver.py

The .blend is never saved - the script only rearranges things in memory before exporting:
  * groups every "Cylinder | ..." piece under a "Cylinder" empty on the spin axis (the game spins it per shot)
  * adds a "Muzzle" empty at the barrel tip (muzzle flash + tracers come from here)
  * parents INSPECT_CTRL under a root turned so the barrel (+X in Blender) faces -Y, which Unity imports as forward
  * game-optimises: curves too dense for real time (the 0.2 mm engraving tubes, ~1M triangles) are left out -
    bake those into a normal map instead - and bevels on tiny parts / curve bevels are simplified
  * exports meshes + curves (converted to mesh) with the INSPECT action baked
The studio camera and lights are left out.
"""
import math
import os

import bpy
from mathutils import Vector

OUTPUT = os.path.join(os.getcwd(), "Assets", "_Project", "Art", "Models", "Weapons", "SM_OrnateRevolver.fbx")
CONTROL = "INSPECT_CTRL"
MUZZLE_TIP = Vector((0.19, 0.0, 0.055))  # just past the muzzle face, in INSPECT_CTRL space

# Game optimisation.
MAX_CURVE_TRIANGLES = 50_000   # curves denser than this are skipped (bake them into a normal map instead)
CURVE_BEVEL_RESOLUTION = 1     # cross-section detail of curve tubes
THIN_CURVE_DEPTH = 0.0005      # tubes thinner than this (m) get the minimum cross-section
SMALL_PART_SIZE = 0.02         # parts smaller than this (m) get a single-segment bevel...
SMALL_PART_BEVEL_SEGMENTS = 1
SMALL_PART_DECIMATE = 0.25     # ...and are decimated to this fraction (192-sided 1 cm screws don't need it)


def triangle_count(obj):
    mesh = obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
    count = sum(len(p.vertices) - 2 for p in mesh.polygons)
    obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()
    return count


def optimise(objects):
    """Simplifies geometry for real time. Returns the objects that should be exported."""
    keep = []
    for obj in objects:
        if obj.type == "CURVE":
            thin = obj.data.bevel_depth < THIN_CURVE_DEPTH
            obj.data.bevel_resolution = 0 if thin else min(obj.data.bevel_resolution, CURVE_BEVEL_RESOLUTION)
            bpy.context.view_layer.update()
            tris = triangle_count(obj)
            if tris > MAX_CURVE_TRIANGLES:
                print(f"[export_ornate_revolver] SKIPPED '{obj.name}' ({tris:,} triangles) - too dense for real time")
                continue
        elif obj.type == "MESH" and max(obj.dimensions) < SMALL_PART_SIZE:
            for mod in obj.modifiers:
                if mod.type == "BEVEL":
                    mod.segments = min(mod.segments, SMALL_PART_BEVEL_SEGMENTS)
            decimate = obj.modifiers.new("GameDecimate", "DECIMATE")
            decimate.ratio = SMALL_PART_DECIMATE
            # Keep the weighted-normal shading on top of the reduced mesh.
            weighted = next((m for m in obj.modifiers if m.type == "WEIGHTED_NORMAL"), None)
            if weighted is not None:
                bpy.context.view_layer.objects.active = obj
                bpy.ops.object.modifier_move_to_index(modifier=weighted.name, index=len(obj.modifiers) - 1)
        keep.append(obj)
    return keep


def make_empty(name, parent, location):
    empty = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(empty)
    empty.empty_display_size = 0.02
    empty.parent = parent
    empty.location = location
    return empty


def reparent_keep_transform(obj, new_parent):
    world = obj.matrix_world.copy()
    obj.parent = new_parent
    obj.matrix_parent_inverse = new_parent.matrix_world.inverted()
    obj.matrix_world = world


def main():
    scene = bpy.context.scene
    scene.frame_set(scene.frame_start)  # rest pose (REST marker) so pivots land in the right place
    control = bpy.data.objects[CONTROL]

    # 1. Cylinder pivot on the spin axis (average of the drum pieces' centres, which all share the axis).
    drum_parts = [o for o in control.children if o.name.startswith("Cylinder |")]
    centre = sum((o.location for o in drum_parts), Vector()) / max(1, len(drum_parts))
    cylinder = make_empty("Cylinder", control, centre)
    bpy.context.view_layer.update()
    for part in drum_parts:
        reparent_keep_transform(part, cylinder)

    # 2. Muzzle marker.
    make_empty("Muzzle", control, MUZZLE_TIP)

    # 3. Root that turns the gun to face Blender -Y (Unity +Z). The inspect animation stays relative to it.
    root = make_empty("OrnateRevolver", None, Vector())
    root.rotation_euler = (0.0, 0.0, math.radians(-90.0))
    bpy.context.view_layer.update()
    control.parent = root
    control.matrix_parent_inverse.identity()
    bpy.context.view_layer.update()

    # 4. Optimise, then select the gun hierarchy only.
    gun = [root] + optimise(list(root.children_recursive))
    bpy.ops.object.select_all(action="DESELECT")
    for obj in gun:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root

    os.makedirs(os.path.dirname(OUTPUT), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=OUTPUT,
        use_selection=True,
        object_types={"EMPTY", "MESH", "OTHER"},  # OTHER = curves, converted to mesh
        use_mesh_modifiers=True,                  # bake the bevel / weighted normal modifiers
        mesh_smooth_type="FACE",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_anim=True,
        bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,
        add_leaf_bones=False,
    )

    counts = sorted(((triangle_count(o), o.name) for o in gun if o.type in {"MESH", "CURVE"}), reverse=True)
    for count, name in counts[:8]:
        print(f"[export_ornate_revolver]   {count:>7,}  {name}")
    print(f"[export_ornate_revolver] {len(gun)} objects, ~{sum(c for c, _ in counts):,} triangles -> {OUTPUT}")


main()
