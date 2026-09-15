"""
Pit Striker Phase 2 P0 asset generator for Blender 3.6 / 4.x

Run (Blender):
  blender --background --python Tools/Blender/phase2_p0/generate_all_p0.py

Or from Blender Scripting workspace: open this file and Run Script.

Outputs FBX to Tools/Blender/phase2_p0/output/
"""
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import bpy
from ps_common import (
    clear_scene, ensure_material, assign_mat, set_origin_world,
    apply_all_transforms, join_objects, add_cylinder, add_cube, add_plane, export_fbx
)
from mathutils import Vector
import math

OUT = SCRIPT_DIR / "output"
OUT.mkdir(parents=True, exist_ok=True)


def build_bamboo_fence():
    """Prop_BambooFence_Module — span 2.0m, height ~1.0m, pivot ground centre, +Y along rail in Blender (= +Z Unity forward after FBX)."""
    clear_scene()
    mat_bark = ensure_material("M_Bamboo_Bark", (0.55, 0.40, 0.18, 1), roughness=0.85)
    mat_rope = ensure_material("M_Rope", (0.35, 0.28, 0.16, 1), roughness=0.9)

    # In Blender: X = right, Y = forward (export -Z forward maps carefully — we align rail along Y)
    post_h, post_r = 1.0, 0.04
    rail_len, rail_r = 2.0, 0.025
    half = rail_len * 0.5

    p0 = add_cylinder("Post_A", post_r, post_h, location=(-half, 0, post_h * 0.5), vertices=10)
    p1 = add_cylinder("Post_B", post_r, post_h, location=( half, 0, post_h * 0.5), vertices=10)
    # rails along X between posts
    r0 = add_cylinder("Rail_Low", rail_r, rail_len, location=(0, 0, 0.35), vertices=8)
    r0.rotation_euler = (0, math.pi * 0.5, 0)
    r1 = add_cylinder("Rail_High", rail_r, rail_len, location=(0, 0, 0.75), vertices=8)
    r1.rotation_euler = (0, math.pi * 0.5, 0)

    # small rope tie cubes at joints
    ties = []
    for x in (-half, half):
        for z in (0.35, 0.75):
            t = add_cube(f"Tie_{x}_{z}", (0.04, 0.04, 0.06), location=(x, 0, z))
            assign_mat(t, mat_rope)
            ties.append(t)

    parts = [p0, p1, r0, r1] + ties
    for o in (p0, p1, r0, r1):
        apply_all_transforms(o)
        assign_mat(o, mat_bark)
    for t in ties:
        apply_all_transforms(t)

    obj = join_objects(parts, "Prop_BambooFence_Module")
    set_origin_world(obj, Vector((0, 0, 0)))
    export_fbx(OUT / "Prop_BambooFence_Module.fbx", [obj])
    print("OK Prop_BambooFence_Module", len(obj.data.polygons), "faces")
    return obj


def build_water_channel():
    """Env_WaterChannel_Segment — L4 x W0.9 x depth 0.35, pivot on water surface centre."""
    clear_scene()
    mat_water = ensure_material("M_Water_Clear", (0.25, 0.55, 0.75, 0.7), roughness=0.15)
    mat_stone = ensure_material("M_RiverStone", (0.35, 0.35, 0.32, 1), roughness=0.9)

    length, width, depth = 4.0, 0.9, 0.35
    # outer box shell minus inner — approximate with bottom + two banks + ends using thin cubes
    bottom = add_cube("Channel_Bottom", (length, width * 0.85, 0.06), location=(0, 0, -depth + 0.03))
    bank_l = add_cube("Bank_L", (length, 0.15, depth), location=(0, -width * 0.5, -depth * 0.5))
    bank_r = add_cube("Bank_R", (length, 0.15, depth), location=(0,  width * 0.5, -depth * 0.5))
    # water plane at z=0 (surface)
    water = add_plane("Water", 1.0, location=(0, 0, 0))
    water.scale = (length * 0.5, width * 0.35, 1)
    bpy.ops.object.transform_apply(scale=True)

    for o in (bottom, bank_l, bank_r):
        apply_all_transforms(o)
        assign_mat(o, mat_stone)
    assign_mat(water, mat_water)

    # Join banks+bottom; keep water separate then join all for single export
    shell = join_objects([bottom, bank_l, bank_r], "Channel_Shell")
    obj = join_objects([shell, water], "Env_WaterChannel_Segment")
    set_origin_world(obj, Vector((0, 0, 0)))  # water surface centre
    export_fbx(OUT / "Env_WaterChannel_Segment.fbx", [obj])
    print("OK Env_WaterChannel_Segment", len(obj.data.polygons), "faces")
    return obj


def build_paddy():
    """Env_PaddyField_Patch — 8x8m plane + low bunds 0.2m."""
    clear_scene()
    mat = ensure_material("M_Paddy_Green", (0.20, 0.55, 0.18, 1), roughness=0.65)
    mat_bund = ensure_material("M_Paddy_Bund", (0.40, 0.32, 0.18, 1), roughness=0.85)

    field = add_plane("Paddy_Field", 8.0, location=(0, 0, 0))
    assign_mat(field, mat)

    bunds = []
    # four edge bunds
    for name, scale, loc in [
        ("Bund_N", (8.2, 0.35, 0.2), (0, 4.0, 0.1)),
        ("Bund_S", (8.2, 0.35, 0.2), (0, -4.0, 0.1)),
        ("Bund_E", (0.35, 8.2, 0.2), (4.0, 0, 0.1)),
        ("Bund_W", (0.35, 8.2, 0.2), (-4.0, 0, 0.1)),
    ]:
        b = add_cube(name, scale, location=loc)
        apply_all_transforms(b)
        assign_mat(b, mat_bund)
        bunds.append(b)

    apply_all_transforms(field)
    obj = join_objects([field] + bunds, "Env_PaddyField_Patch")
    set_origin_world(obj, Vector((0, 0, 0)))
    export_fbx(OUT / "Env_PaddyField_Patch.fbx", [obj])
    print("OK Env_PaddyField_Patch", len(obj.data.polygons), "faces")
    return obj


def build_kerala_house_a():
    """Bldg_KeralaHouse_A — footprint ~8x6, ridge ~4.5, porch 1.5, pivot ground centre, +Y porch forward."""
    clear_scene()
    mat_plaster = ensure_material("M_Plaster_White", (0.92, 0.92, 0.88, 1), roughness=0.75)
    mat_wood = ensure_material("M_Wood_Dark", (0.18, 0.10, 0.06, 1), roughness=0.8)
    mat_roof = ensure_material("M_Roof_Terracotta", (0.75, 0.28, 0.12, 1), roughness=0.7)
    mat_stone = ensure_material("M_Stone_Plinth", (0.45, 0.42, 0.38, 1), roughness=0.9)

    # body 8 x 6 x 3
    body = add_cube("Body", (8, 6, 3), location=(0, 0, 1.65))
    apply_all_transforms(body)
    assign_mat(body, mat_plaster)

    # plinth
    plinth = add_cube("Plinth", (8.3, 6.3, 0.3), location=(0, 0, 0.15))
    apply_all_transforms(plinth)
    assign_mat(plinth, mat_stone)

    # porch slab toward +Y
    porch = add_cube("Porch", (8, 1.5, 0.15), location=(0, 3.75, 0.35))
    apply_all_transforms(porch)
    assign_mat(porch, mat_stone)

    # pillars
    pillars = []
    for i, x in enumerate((-3.2, -1.05, 1.05, 3.2)):
        p = add_cylinder(f"Pillar_{i}", 0.12, 2.6, location=(x, 3.5, 1.5), vertices=10)
        apply_all_transforms(p)
        assign_mat(p, mat_wood)
        pillars.append(p)

    # simple gable roof: two slanted slabs
    roof_l = add_cube("Roof_L", (8.6, 4.2, 0.2), location=(-0.1, 0, 4.0))
    roof_l.rotation_euler = (0, 0, 0)
    roof_l.rotation_euler[0] = math.radians(28)
    apply_all_transforms(roof_l)
    assign_mat(roof_l, mat_roof)
    roof_r = add_cube("Roof_R", (8.6, 4.2, 0.2), location=(0.1, 0, 4.0))
    roof_r.rotation_euler[0] = math.radians(-28)
    apply_all_transforms(roof_r)
    assign_mat(roof_r, mat_roof)

    # porch roof
    porch_roof = add_cube("PorchRoof", (8.2, 1.8, 0.12), location=(0, 3.6, 3.1))
    porch_roof.rotation_euler[0] = math.radians(8)
    apply_all_transforms(porch_roof)
    assign_mat(porch_roof, mat_roof)

    parts = [body, plinth, porch, roof_l, roof_r, porch_roof] + pillars
    obj = join_objects(parts, "Bldg_KeralaHouse_A")
    set_origin_world(obj, Vector((0, 0, 0)))
    export_fbx(OUT / "Bldg_KeralaHouse_A.fbx", [obj])
    print("OK Bldg_KeralaHouse_A", len(obj.data.polygons), "faces")
    return obj


def build_coconut_palm(suffix, height):
    """Veg_CoconutPalm_* — trunk H + simple frond cards, pivot trunk base."""
    clear_scene()
    mat_trunk = ensure_material("M_Palm_Trunk", (0.42, 0.30, 0.16, 1), roughness=0.88)
    mat_frond = ensure_material("M_Palm_Frond", (0.18, 0.45, 0.12, 1), roughness=0.7)

    trunk = add_cylinder("Trunk", 0.18, height, location=(0, 0, height * 0.5), vertices=10)
    # slight lean
    trunk.rotation_euler[0] = math.radians(4)
    apply_all_transforms(trunk)
    assign_mat(trunk, mat_trunk)

    fronds = []
    canopy_z = height * 0.95
    for i in range(8):
        ang = i * (math.pi * 2 / 8)
        f = add_cube(f"Frond_{i}", (0.15, 3.2, 0.05), location=(math.sin(ang) * 0.4, math.cos(ang) * 0.4, canopy_z))
        f.rotation_euler = (math.radians(55), 0, ang)
        apply_all_transforms(f)
        assign_mat(f, mat_frond)
        fronds.append(f)

    obj = join_objects([trunk] + fronds, f"Veg_CoconutPalm_{suffix}")
    set_origin_world(obj, Vector((0, 0, 0)))
    export_fbx(OUT / f"Veg_CoconutPalm_{suffix}.fbx", [obj])
    print(f"OK Veg_CoconutPalm_{suffix}", len(obj.data.polygons), "faces")
    return obj


def main():
    print("=== Pit Striker Phase 2 P0 generate ===")
    print("Output:", OUT)
    build_bamboo_fence()
    build_water_channel()
    build_paddy()
    build_kerala_house_a()
    build_coconut_palm("A", 8.0)
    build_coconut_palm("B", 10.0)
    build_coconut_palm("C", 12.0)
    print("=== ALL P0 DONE ===")
    for p in sorted(OUT.glob("*.fbx")):
        print(" ", p.name, p.stat().st_size, "bytes")


if __name__ == "__main__":
    main()
