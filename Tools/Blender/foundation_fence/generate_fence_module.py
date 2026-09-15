"""
Foundation fence module v2 — Prop_Fence_Module
Natural wooden/bamboo continuous fence (not ladder frames).

Specs:
  exact span 2.0 m (tile end-to-end), height ~1.15 m
  post diameter 0.08, vertical stakes between posts
  ground pivot centre, identity scale, no strays

Run:
  blender --background --python Tools/Blender/foundation_fence/generate_fence_module.py
"""
import sys
import math
from pathlib import Path
from mathutils import Vector

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

import bpy
from ps_common import (
    clear_scene, ensure_material, assign_mat, set_origin_world,
    apply_all_transforms, join_objects, add_cylinder, add_cube, export_fbx
)

OUT = SCRIPT_DIR / "output"
OUT.mkdir(parents=True, exist_ok=True)


def build_fence_module():
    clear_scene()
    mat_bamboo = ensure_material("M_Bamboo_Fence", (0.58, 0.42, 0.22, 1), roughness=0.78)
    mat_dark = ensure_material("M_Bamboo_Dark", (0.42, 0.30, 0.14, 1), roughness=0.85)
    mat_rope = ensure_material("M_Fence_Rope", (0.36, 0.28, 0.16, 1), roughness=0.9)

    span = 2.0
    height = 1.15
    post_r = 0.04
    stake_r = 0.028
    binder_r = 0.018
    half = span * 0.5

    # End posts: planted slightly below ground
    post_len = height + 0.10
    post_z = (height - 0.10) * 0.5
    p0 = add_cylinder("Post_L", post_r, post_len, location=(-half, 0, post_z), vertices=12)
    p1 = add_cylinder("Post_R", post_r, post_len, location=( half, 0, post_z), vertices=12)

    # Dense vertical bamboo stakes between posts (natural fence look)
    stakes = []
    n_stakes = 7
    inner = span - 0.16  # clear of post centres
    for i in range(n_stakes):
        t = (i + 1) / (n_stakes + 1)
        x = -half + 0.08 + t * (span - 0.16)
        # slight height variation
        h = height - 0.05 - (i % 3) * 0.04
        z = (h - 0.06) * 0.5
        s = add_cylinder(f"Stake_{i}", stake_r, h + 0.06, location=(x, 0, z), vertices=10)
        stakes.append(s)

    # Two horizontal binders (lash rails) — thicker than before
    binders = []
    for i, z in enumerate((0.38, 0.92)):
        b = add_cylinder(f"Binder_{i}", binder_r, span - 0.04, location=(0, 0, z), vertices=10)
        b.rotation_euler = (0, math.pi * 0.5, 0)
        binders.append(b)

    # Rope/lash cubes at posts
    ties = []
    for x in (-half, half):
        for z in (0.38, 0.92):
            t = add_cube(f"Lash_{x}_{z}", (0.06, 0.06, 0.08), location=(x, 0, z))
            assign_mat(t, mat_rope)
            ties.append(t)

    parts = [p0, p1] + stakes + binders + ties
    for o in [p0, p1] + stakes:
        apply_all_transforms(o)
        assign_mat(o, mat_bamboo)
    for o in binders:
        apply_all_transforms(o)
        assign_mat(o, mat_dark)
    for t in ties:
        apply_all_transforms(t)

    obj = join_objects(parts, "Prop_Fence_Module")
    set_origin_world(obj, Vector((0, 0, 0)))
    obj.location = (0, 0, 0)
    obj.rotation_euler = (0, 0, 0)
    obj.scale = (1, 1, 1)

    out_path = OUT / "Prop_Fence_Module.fbx"
    export_fbx(out_path, [obj])
    print("OK Prop_Fence_Module faces=", len(obj.data.polygons), "path=", out_path)
    bb = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    xs = [v.x for v in bb]; ys = [v.y for v in bb]; zs = [v.z for v in bb]
    print(f"BOUNDS x={min(xs):.3f}..{max(xs):.3f} y={min(ys):.3f}..{max(ys):.3f} z={min(zs):.3f}..{max(zs):.3f}")
    print(f"SIZE span={max(xs)-min(xs):.3f} depth={max(ys)-min(ys):.3f} height={max(zs)-min(zs):.3f}")
    print(f"POST_SPACING (centre-to-centre)={span:.3f}")
    return obj


def main():
    print("=== Foundation Prop_Fence_Module v2 (solid bamboo) ===")
    build_fence_module()
    for p in sorted(OUT.glob("*.fbx")):
        print(" ", p.name, p.stat().st_size, "bytes")
    print("=== DONE ===")


if __name__ == "__main__":
    main()
