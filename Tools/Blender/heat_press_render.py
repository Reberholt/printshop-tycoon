"""
Preview scene + animation for the heat press (called from heat_press.py, mode "render").
Builds a cosy workshop corner like the reference art and animates one full press cycle:
shirt slides on -> transfer lands -> lid closes -> glow + steam -> lid opens -> film peels ->
finished shirt with logo -> onto the stack.  Renders PNG frames (portrait 9:16) with Cycles.
"""
import math
import os

import bpy
from mathutils import Vector

import heat_press as hp

FPS = 24
SECONDS = 25


def sec(t):
    return int(round(t * FPS)) + 1


# --------------------------------------------------------------------------- geometry helpers

def poly_mesh(name, points2d, thickness, z, material, bevel=0.012, parent=None):
    """Extruded flat outline (XY) - used for the T-shirt and the star."""
    mesh = bpy.data.meshes.new(name)
    n = len(points2d)
    verts = [(x, y, z) for x, y in points2d] + [(x, y, z + thickness) for x, y in points2d]
    faces = [list(range(n))[::-1], list(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        faces.append([i, j, n + j, n + i])
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    hp.finish(obj, material, bevel, 3, parent)
    return obj


def tshirt_outline(scale=1.0):
    # Front view of a tee (x right, y up), centred; ~0.62 m wide, 0.62 m tall.
    pts = [(-0.13, 0.31), (-0.06, 0.27), (0.06, 0.27), (0.13, 0.31), (0.31, 0.2), (0.24, 0.07), (0.17, 0.11),
           (0.17, -0.31), (-0.17, -0.31), (-0.17, 0.11), (-0.24, 0.07), (-0.31, 0.2)]
    return [(x * scale, y * scale) for x, y in pts]


def star_outline(r_out, r_in):
    pts = []
    for i in range(10):
        r = r_out if i % 2 == 0 else r_in
        a = math.radians(90 + i * 36)
        pts.append((r * math.cos(a), r * math.sin(a)))
    return pts


def key_vis(obj, frame, visible):
    obj.hide_render = not visible
    obj.hide_viewport = not visible
    obj.keyframe_insert("hide_render", frame=frame)
    obj.keyframe_insert("hide_viewport", frame=frame)


def key_loc(obj, frame, loc):
    obj.location = loc
    obj.keyframe_insert("location", frame=frame)


def key_rotx(obj, frame, angle):
    obj.rotation_euler = (angle, 0, 0)
    obj.keyframe_insert("rotation_euler", frame=frame)


def ease_all():
    for action in bpy.data.actions:
        for fc in action.fcurves:
            for kp in fc.keyframe_points:
                kp.interpolation = "BEZIER"
                kp.easing = "AUTO"


# --------------------------------------------------------------------------- scene

def build_workshop():
    floor = hp.mat("Floor", "#E9CFA5", rough=0.7)
    wall = hp.mat("Wall", "#FFF1DE", rough=0.9)
    trim = hp.mat("Trim", "#7FC8C2", rough=0.6)
    glass = hp.mat("Glass", "#BFE6FF", rough=0.1, emission="#DFF3FF", strength=1.2)
    shelfm = hp.mat("Shelf", "#C98B5A", rough=0.6)
    hp.box("Floor", (8, 8, 0.1), (0, 0, -0.05), floor, 0.0)
    hp.box("BackWall", (8, 0.1, 4), (0, -1.6, 2), wall, 0.0)
    hp.box("SideWall", (0.1, 8, 4), (-2.2, 0, 2), wall, 0.0)
    hp.box("Trim", (8, 0.02, 0.12), (0, -1.54, 1.0), trim, 0.0)
    hp.box("WindowFrame", (1.3, 0.06, 1.0), (1.3, -1.55, 2.1), hp.WHITE, 0.02)
    hp.box("Window", (1.15, 0.03, 0.85), (1.3, -1.52, 2.1), glass, 0.0)
    # Shelves with coloured shirt stacks (like the reference background).
    colors = ["#FFFFFF", "#2E86DE", "#E5322D", "#FFC72C", "#FFFFFF", "#38C172"]
    for row in range(2):
        z = 1.35 + row * 0.55
        hp.box("WallShelf", (1.6, 0.35, 0.05), (-1.0, -1.38, z), shelfm, 0.01)
        for i in range(4):
            c = hp.mat("Stack" + str(row) + str(i), colors[(i + row * 2) % len(colors)], rough=0.6)
            for k in range(3):
                hp.box("Tee", (0.3, 0.26, 0.06), (-1.6 + i * 0.4, -1.38, z + 0.06 + k * 0.065), c, 0.015)
    # Poster with a red tee.
    hp.box("Poster", (0.55, 0.02, 0.7), (-0.15, -1.54, 2.35), hp.mat("PosterBg", "#FFE3E0", rough=0.8), 0.0)
    poly_mesh("PosterTee", [(x, y) for x, y in tshirt_outline(0.7)], 0.01, 0, hp.mat("PosterTee", "#E5322D"), 0.004)
    pt = bpy.data.objects["PosterTee"]
    pt.rotation_euler = (math.radians(90), 0, 0)
    pt.location = (-0.15, -1.52, 2.35)
    # Plant.
    hp.cyl("Pot", 0.16, 0.3, (1.55, -1.1, 0.15), (0, 0, 0), hp.mat("Pot", "#E07A5F"), 0.02)
    hp.sphere("Leaves", 0.3, (1.55, -1.1, 0.55), hp.mat("Leaves", "#4CAF6A", rough=0.6))
    # Finished stack on the table (grows when the shirt is added).
    return floor


def build_lights_camera():
    scene = bpy.context.scene
    world = bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = hp.hex_color("#FFE9D2")
    bg.inputs["Strength"].default_value = 0.55

    sun = bpy.data.lights.new("Sun", "SUN")
    sun.energy = 3.2
    sun.angle = math.radians(12)
    sun.color = (1.0, 0.93, 0.84)
    so = bpy.data.objects.new("Sun", sun)
    so.rotation_euler = (math.radians(50), math.radians(15), math.radians(35))
    scene.collection.objects.link(so)

    key = bpy.data.lights.new("Key", "AREA")
    key.energy = 260
    key.size = 2.5
    ko = bpy.data.objects.new("Key", key)
    ko.location = (1.8, 2.2, 3.0)
    ko.rotation_euler = (math.radians(50), 0, math.radians(140))
    scene.collection.objects.link(ko)

    cam = bpy.data.cameras.new("Camera")
    cam.lens = 35
    co = bpy.data.objects.new("Camera", cam)
    scene.collection.objects.link(co)
    scene.camera = co
    target = bpy.data.objects.new("CamTarget", None)
    scene.collection.objects.link(target)
    target.location = (0, 0, 1.12)
    track = co.constraints.new("TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"
    rig = bpy.data.objects.new("CamRig", None)
    scene.collection.objects.link(rig)
    rig.location = (0, 0, 0)
    co.parent = rig
    co.location = (0.0, 2.15, 1.85)
    return co, rig, target


# --------------------------------------------------------------------------- animation

def animate(pivot, rig, cam, target):
    scene = bpy.context.scene
    scene.frame_start = 1
    scene.frame_end = SECONDS * FPS
    scene.render.fps = FPS

    shirt_mat = hp.mat("Shirt", "#FFFFFF", rough=0.75)
    star_mat = hp.mat("Star", "#FFC72C", rough=0.45)
    film_mat = hp.mat("Film", "#EAF4FF", rough=0.15)
    steam_mat = hp.mat("Steam", "#FFFFFF", rough=0.9)
    steam_bsdf = steam_mat.node_tree.nodes["Principled BSDF"]
    steam_bsdf.inputs["Alpha"].default_value = 0.35
    steam_mat.blend_method = "BLEND"

    shirt = poly_mesh("Shirt", tshirt_outline(1.0), 0.025, 0, shirt_mat, 0.01)
    star_on_shirt = poly_mesh("StarPrint", star_outline(0.11, 0.045), 0.004, 0.026, star_mat, 0.002, shirt)
    film = poly_mesh("Film", [(-0.18, -0.16), (0.18, -0.16), (0.18, 0.2), (-0.18, 0.2)], 0.004, 0, film_mat, 0.002)
    star_on_film = poly_mesh("StarFilm", star_outline(0.11, 0.045), 0.003, 0.004, star_mat, 0.001, film)

    platen_top = 1.057
    on_platen = Vector((0, 0.08, platen_top))
    start = Vector((1.25, 0.65, 0.84))     # comes in from the table, front right
    stack = Vector((-0.95, 0.35, 0.84))    # finished stack, front left

    # Finished stack on the table (two shirts already there + the new one lands on top).
    for i in range(2):
        s = poly_mesh("Done" + str(i), tshirt_outline(1.0), 0.025, 0, shirt_mat, 0.01)
        poly_mesh("DoneStar" + str(i), star_outline(0.11, 0.045), 0.004, 0.026, star_mat, 0.002, s)
        s.location = stack + Vector((0, 0, i * 0.03))
        s.rotation_euler = (0, 0, math.radians(8 * i))
    hp.box("SideTable", (0.75, 0.75, 0.06), (-0.95, 0.35, 0.81), hp.WOOD, 0.02)
    hp.cyl("SideLeg", 0.06, 0.78, (-0.95, 0.35, 0.39), (0, 0, 0), hp.WOOD_DARK, 0.01, 16)
    hp.box("Box", (0.55, 0.45, 0.4), (1.3, -0.9, 0.2), hp.mat("Cardboard", "#D9A066", rough=0.8), 0.02)
    hp.box("RightTable", (0.75, 0.75, 0.06), (1.25, 0.65, 0.81), hp.WOOD, 0.02)
    hp.cyl("RightLeg", 0.06, 0.78, (1.25, 0.65, 0.39), (0, 0, 0), hp.WOOD_DARK, 0.01, 16)

    # --- Lid: open idle, close, press, open with a little bounce.
    open_angle = math.radians(50)
    key_rotx(pivot, sec(0), open_angle)
    key_rotx(pivot, sec(5.2), open_angle)
    key_rotx(pivot, sec(6.4), 0)
    key_rotx(pivot, sec(12.0), 0)
    key_rotx(pivot, sec(12.9), open_angle + math.radians(4))
    key_rotx(pivot, sec(13.3), open_angle)

    # --- Shirt: slide in, lie on the platen, lift and show, onto the stack.
    key_loc(shirt, sec(0), start)
    key_loc(shirt, sec(2.6), start)
    key_loc(shirt, sec(3.8), on_platen)
    key_loc(shirt, sec(14.6), on_platen)
    key_loc(shirt, sec(15.4), on_platen + Vector((0, 0.15, 0.35)))
    key_loc(shirt, sec(17.2), on_platen + Vector((0, 0.15, 0.35)))
    key_loc(shirt, sec(18.6), stack + Vector((0, 0, 0.065)))
    shirt.rotation_euler = (0, 0, 0)
    shirt.keyframe_insert("rotation_euler", frame=sec(15.4))
    shirt.rotation_euler = (math.radians(-55), 0, 0)  # held up towards the camera
    shirt.keyframe_insert("rotation_euler", frame=sec(15.9))
    shirt.keyframe_insert("rotation_euler", frame=sec(17.2))
    shirt.rotation_euler = (0, 0, math.radians(16))
    shirt.keyframe_insert("rotation_euler", frame=sec(18.6))

    # --- Transfer film: drops onto the shirt, gets pressed, peels off and flies away.
    film_on = on_platen + Vector((0, 0.04, 0.027))
    key_loc(film, sec(0), film_on + Vector((0.2, 0.4, 0.7)))
    key_loc(film, sec(3.9), film_on + Vector((0.2, 0.4, 0.7)))
    key_loc(film, sec(4.7), film_on)
    key_loc(film, sec(13.6), film_on)
    key_loc(film, sec(14.5), film_on + Vector((0.5, 0.6, 0.6)))
    film.rotation_euler = (0, 0, 0)
    film.keyframe_insert("rotation_euler", frame=sec(13.6))
    film.rotation_euler = (math.radians(-70), math.radians(20), 0)
    film.keyframe_insert("rotation_euler", frame=sec(14.5))
    key_vis(film, sec(0), False)
    key_vis(film, sec(3.9), True)
    key_vis(film, sec(14.5), False)
    # The logo moves from the film to the shirt when the film is peeled.
    key_vis(star_on_shirt, sec(0), False)
    key_vis(star_on_shirt, sec(13.6), True)
    key_vis(star_on_film, sec(0), True)
    key_vis(star_on_film, sec(13.6), False)

    # --- Heat: pad glows while pressing, status lamp switches to orange.
    pad = hp.MATS["HeatPad"].node_tree.nodes["Principled BSDF"].inputs["Emission Strength"]
    for t, v in ((0, 0.0), (6.4, 0.0), (7.2, 2.5), (11.6, 2.5), (12.6, 0.0)):
        pad.default_value = v
        pad.keyframe_insert("default_value", frame=sec(t))
    lamp_color = hp.MATS["Lamp"].node_tree.nodes["Principled BSDF"].inputs["Emission Color"]
    for t, c in ((0, "#38C172"), (6.4, "#38C172"), (6.5, "#FF8A1F"), (12.0, "#FF8A1F"), (12.1, "#38C172")):
        lamp_color.default_value = hp.hex_color(c)
        lamp_color.keyframe_insert("default_value", frame=sec(t))

    # --- Steam puffs while pressing (and a big puff when the lid opens).
    for i in range(10):
        puff = hp.sphere("Steam" + str(i), 0.045, (0, 0, 0), steam_mat)
        t0 = 6.8 + i * 0.55 if i < 8 else 12.4 + (i - 8) * 0.2
        x = (-0.4 + (i * 0.37) % 0.8)
        base = Vector((x, 0.45 if i % 2 else -0.24, 1.16))
        key_vis(puff, sec(0), False)
        key_vis(puff, sec(t0), True)
        key_vis(puff, sec(t0 + 1.6), False)
        puff.scale = (0.4, 0.4, 0.4)
        puff.keyframe_insert("scale", frame=sec(t0))
        puff.scale = (1.1, 1.1, 1.1)
        puff.keyframe_insert("scale", frame=sec(t0 + 1.6))
        key_loc(puff, sec(t0), base)
        key_loc(puff, sec(t0 + 1.6), base + Vector((0.05, 0.08, 0.45)))

    # --- Camera: gentle orbit, a closer look while pressing, hero shot at the end.
    rig.rotation_euler = (0, 0, math.radians(18))
    rig.keyframe_insert("rotation_euler", frame=sec(0))
    rig.rotation_euler = (0, 0, math.radians(-8))
    rig.keyframe_insert("rotation_euler", frame=sec(9))
    rig.rotation_euler = (0, 0, math.radians(10))
    rig.keyframe_insert("rotation_euler", frame=sec(17))
    rig.rotation_euler = (0, 0, math.radians(-22))
    rig.keyframe_insert("rotation_euler", frame=sec(25))
    key_loc(cam, sec(0), (0.0, 3.1, 2.25))
    key_loc(cam, sec(7.5), (0.0, 2.35, 1.95))
    key_loc(cam, sec(12), (0.0, 2.4, 1.95))
    key_loc(cam, sec(16), (0.0, 2.2, 1.75))
    key_loc(cam, sec(25), (0.0, 3.0, 2.2))
    key_loc(target, sec(0), (0, 0, 1.12))
    key_loc(target, sec(16), (0, 0.15, 1.25))
    key_loc(target, sec(19), (-0.35, 0.2, 1.0))
    key_loc(target, sec(25), (0, 0, 1.1))
    ease_all()
    # Visibility keys must not be smoothed.
    for action in bpy.data.actions:
        for fc in action.fcurves:
            if fc.data_path.startswith("hide"):
                for kp in fc.keyframe_points:
                    kp.interpolation = "CONSTANT"


# --------------------------------------------------------------------------- main

def main(args):
    out_dir = os.path.abspath(args[0]) if args else os.path.abspath("frames")
    width = int(args[1]) if len(args) > 1 else 540
    height = int(args[2]) if len(args) > 2 else 960
    samples = int(args[3]) if len(args) > 3 else 24
    only_frame = int(args[4]) if len(args) > 4 else 0
    os.makedirs(out_dir, exist_ok=True)

    hp.clear_scene()
    hp.init_materials()
    build_workshop()
    root, pivot = hp.build_press()
    cam, rig, target = build_lights_camera()
    animate(pivot, rig, cam, target)

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_adaptive_sampling = True
    scene.cycles.max_bounces = 4
    # This Blender build has no OpenImageDenoise: rely on more samples + a wider pixel filter.
    scene.cycles.use_denoising = False
    scene.cycles.filter_width = 2.0
    scene.cycles.sample_clamp_indirect = 3.0
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "Filmic" if "Filmic" in [v for v in ("Filmic", "AgX")] else "Standard"
    scene.view_settings.look = "None"
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = os.path.join(out_dir, "frame_")

    if only_frame:
        scene.frame_set(only_frame)
        scene.render.filepath = os.path.join(out_dir, "still_%04d.png" % only_frame)
        bpy.ops.render.render(write_still=True)
    else:
        bpy.ops.render.render(animation=True)
    print("RENDER DONE", out_dir)
