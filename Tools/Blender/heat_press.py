"""
Printshop Tycoon - stylised heat press (rounded, toy-like 3D look) built by script.

  Export the Unity model:
    blender -b -P Tools/Blender/heat_press.py -- export
  Render the preview video frames (Cycles, portrait 9:16):
    blender -b -P Tools/Blender/heat_press.py -- render <frames_dir> [width height samples]

Modelling conventions (so Unity gets it right):
  * metres, front of the machine faces +Y here (= -Z, towards the camera, in Unity),
  * origin = floor centre of the machine, matching the game's footprint (1.6 x 2.4 m incl. operator space),
  * "LidPivot" empty sits on the hinge; rotating it around X lifts the lid,
  * named parts the game looks up: LidPivot, HeatPlate, Platen, StatusLamp.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["export"]
MODE = ARGS[0]
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FBX_PATH = os.path.join(ROOT, "Assets", "PrintshopTycoon", "Resources", "PrintshopTycoon", "Models", "HeatPress.fbx")

# --------------------------------------------------------------------------- helpers

def hex_color(h, a=1.0):
    h = h.lstrip("#")
    srgb = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    lin = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb]
    return (*lin, a)


MATS = {}


def mat(name, hexcol, metallic=0.0, rough=0.45, emission=None, strength=0.0):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = hex_color(hexcol)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    if emission:
        bsdf.inputs["Emission Color"].default_value = hex_color(emission)
        bsdf.inputs["Emission Strength"].default_value = strength
    m.diffuse_color = hex_color(hexcol)  # what the FBX/Unity importer reads
    MATS[name] = m
    return m


def finish(obj, material, bevel=0.0, segments=4, parent=None):
    obj.data.materials.clear()
    obj.data.materials.append(material)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.limit_method = "ANGLE"
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.shade_smooth()
    if hasattr(obj.data, "use_auto_smooth"):
        obj.data.use_auto_smooth = True
        obj.data.auto_smooth_angle = math.radians(40)
    if parent is not None:
        set_parent(obj, parent)
    return obj


def set_parent(obj, parent):
    bpy.context.view_layer.update()  # make sure matrix_world reflects a freshly set location
    mw = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = mw


def box(name, size, loc, material, bevel=0.02, parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(o, material, bevel, parent=parent)


def cyl(name, radius, depth, loc, rot, material, bevel=0.01, verts=32, parent=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return finish(o, material, bevel, parent=parent)


def sphere(name, radius, loc, material, parent=None, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=radius, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(o, material, 0.0, parent=parent)


def text(name, body, size, loc, material, parent=None, extrude=0.004, align="CENTER"):
    """Text standing upright, facing +Y and readable from the front (+Y)."""
    bpy.ops.object.text_add(location=loc, rotation=(math.radians(90), 0, math.radians(180)))
    o = bpy.context.active_object
    o.name = name
    o.data.body = body
    o.data.size = size
    o.data.extrude = extrude
    o.data.align_x = align
    o.data.align_y = "CENTER"
    bpy.ops.object.convert(target="MESH")
    o = bpy.context.active_object
    o.name = name
    return finish(o, material, 0.0, parent=parent)


def triangle_sign(name, center, size, parent):
    """Yellow warning triangle sticker (thin prism) facing +Y."""
    bpy.ops.mesh.primitive_cylinder_add(vertices=3, radius=size, depth=0.006, location=center,
                                        rotation=(math.radians(90), 0, math.radians(30)))
    o = bpy.context.active_object
    o.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    finish(o, mat("Warning", "#FFC72C", rough=0.4), 0.0, parent=parent)
    mark = box(name + "Mark", (0.006, 0.004, size * 0.6), (center[0], center[1] + 0.004, center[2] + size * 0.05),
               mat("Black", "#1C1C1E", rough=0.5), 0.0, parent)
    return o


def empty(name, loc, parent=None):
    o = bpy.data.objects.new(name, None)
    o.empty_display_size = 0.1
    o.location = loc
    bpy.context.scene.collection.objects.link(o)
    if parent is not None:
        set_parent(o, parent)
    return o


# --------------------------------------------------------------------------- the press

def init_materials():
    """(Re)creates the shared materials - call after clearing the scene."""
    global RED, RED_DARK, BLACK, RUBBER, SILVER, STEEL, HEATPAD, WOOD, WOOD_DARK, SCREEN, SCREEN_TEXT, WHITE
    MATS.clear()
    RED = mat("PressRed", "#E5322D", rough=0.38)
    RED_DARK = mat("PressRedDark", "#B9231F", rough=0.45)
    BLACK = mat("Black", "#1C1C1E", rough=0.5)
    RUBBER = mat("Rubber", "#2A2A2E", rough=0.8)
    SILVER = mat("Silver", "#C9CED6", metallic=0.85, rough=0.28)
    STEEL = mat("Steel", "#9AA1AB", metallic=0.8, rough=0.35)
    HEATPAD = mat("HeatPad", "#F2B33D", rough=0.5, emission="#FF7A1A", strength=0.0)
    WOOD = mat("Wood", "#D9A36B", rough=0.6)
    WOOD_DARK = mat("WoodDark", "#B07A47", rough=0.65)
    SCREEN = mat("Screen", "#13213A", rough=0.2)
    SCREEN_TEXT = mat("ScreenText", "#7FE3FF", rough=0.3, emission="#7FE3FF", strength=1.5)
    WHITE = mat("White", "#F7F7F5", rough=0.5)


def build_press():
    root = empty("HeatPress", (0, 0, 0))

    # Rounded wooden work table (the press is a table-top machine, like the reference).
    box("TableTop", (1.34, 1.1, 0.08), (0, 0.02, 0.76), WOOD, 0.03, root)
    for sx in (-1, 1):
        for sy in (-1, 1):
            cyl("TableLeg", 0.05, 0.72, (sx * 0.57, 0.02 + sy * 0.45, 0.36), (0, 0, 0), WOOD_DARK, 0.01, 16, root)
    box("TableShelf", (1.2, 0.95, 0.04), (0, 0.02, 0.22), WOOD_DARK, 0.015, root)
    # Folded blank tees on the shelf.
    for i in range(3):
        box("ShelfTee", (0.36, 0.32, 0.05), (-0.32 + i * 0.32, 0.05, 0.27), WHITE, 0.015, root)

    # Base: chunky red body with a warning sticker (no brand lettering).
    box("Base", (1.0, 0.86, 0.16), (0, 0.04, 0.88), RED, 0.05, root)
    triangle_sign("BaseWarning", (-0.4, 0.474, 0.88), 0.035, root)
    for sx in (-1, 1):
        box("Foot", (0.12, 0.12, 0.03), (sx * 0.4, 0.38, 0.795), RUBBER, 0.01, root)

    # Lower platen: soft black pad on a short neck.
    box("PlatenNeck", (0.22, 0.22, 0.05), (0, 0.06, 0.985), STEEL, 0.01, root)
    box("Platen", (0.86, 0.64, 0.05), (0, 0.08, 1.03), BLACK, 0.025, root)

    # Rear column with the hinge, and the control box (screen "160° / 15s" + buttons) on the right.
    box("Column", (0.26, 0.2, 0.36), (0, -0.33, 1.13), RED_DARK, 0.04, root)
    cyl("HingeAxle", 0.045, 0.34, (0, -0.33, 1.3), (0, math.radians(90), 0), STEEL, 0.005, 24, root)
    box("ControlBox", (0.26, 0.2, 0.26), (-0.36, -0.3, 1.12), RED, 0.04, root)
    box("Screen", (0.18, 0.012, 0.11), (-0.36, -0.195, 1.16), SCREEN, 0.006, root)
    text("ScreenTemp", "160°", 0.042, (-0.36, -0.187, 1.18), SCREEN_TEXT, root, 0.002)
    text("ScreenTime", "15s", 0.036, (-0.36, -0.187, 1.135), SCREEN_TEXT, root, 0.002)
    for i, col in enumerate(("#E5322D", "#FFC72C", "#38C172")):
        cyl("Button", 0.016, 0.02, (-0.42 + i * 0.06, -0.192, 1.06), (math.radians(90), 0, 0),
            mat("Btn" + str(i), col, rough=0.35), 0.003, 16, root)
    sphere("StatusLamp", 0.026, (-0.25, -0.19, 1.22), mat("Lamp", "#38C172", rough=0.3, emission="#38C172", strength=2.0), root)

    # Lid assembly on the hinge: red housing, silver heat plate, yellow heat pad edge, handle.
    pivot = empty("LidPivot", (0, -0.33, 1.3), root)
    box("LidArm", (0.24, 0.38, 0.1), (0, -0.16, 1.3), RED_DARK, 0.035, pivot)
    box("Lid", (0.9, 0.68, 0.13), (0, 0.1, 1.22), RED, 0.05, pivot)
    box("HeatPad", (0.88, 0.66, 0.02), (0, 0.1, 1.145), HEATPAD, 0.008, pivot)
    box("HeatPlate", (0.86, 0.64, 0.035), (0, 0.1, 1.12), SILVER, 0.012, pivot)
    triangle_sign("LidWarning", (-0.33, 0.441, 1.22), 0.03, pivot)
    # Handle: two silver arms and a black rubber grip with silver caps.
    for sx in (-1, 1):
        cyl("HandleArm", 0.022, 0.32, (sx * 0.2, 0.36, 1.37), (math.radians(-55), 0, 0), SILVER, 0.004, 16, pivot)
        cyl("GripCap", 0.042, 0.05, (sx * 0.27, 0.46, 1.46), (0, math.radians(90), 0), SILVER, 0.008, 24, pivot)
    cyl("Grip", 0.038, 0.5, (0, 0.46, 1.46), (0, math.radians(90), 0), RUBBER, 0.01, 24, pivot)
    return root, pivot


# --------------------------------------------------------------------------- export / render

def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()


def export_fbx():
    clear_scene()
    init_materials()
    build_press()
    os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=False,
        object_types={"EMPTY", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=False,  # Unity does the axis conversion (ModelImportSettings.bakeAxisConversion)
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
    )
    tris = sum(len(o.data.polygons) for o in bpy.data.objects if o.type == "MESH")
    print("EXPORTED", FBX_PATH, "objects:", len(bpy.data.objects), "faces(before modifiers):", tris)


if __name__ == "__main__":
    if MODE == "export":
        export_fbx()
    else:
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        import heat_press_render
        heat_press_render.main(ARGS[1:])
