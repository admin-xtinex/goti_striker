"""
Pit Striker Phase 2 P1 — distant Kerala houses + banana clump + bank plants.
Blender 3.6+ / 4.x / 5.x

Run:
  blender --background --python Tools/Blender/phase2_p1/generate_all_p1.py
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
    apply_all_transforms, join_objects, add_cylinder, add_cube, add_plane, export_fbx
)

OUT = SCRIPT_DIR / "output"
OUT.mkdir(parents=True, exist_ok=True)


def build_house_b(suffix=""):
    """Bldg_KeralaHouse_B_Distant — ~60% of House A, no collider, no billboards."""
    clear_scene()
    mat_plaster = ensure_material("M_Plaster_White", (0.92, 0.92, 0.88, 1), roughness=0.75)
    mat_wood = ensure_material("M_Wood_Dark", (0.18, 0.10, 0.06, 1), roughness=0.8)
    mat_roof = ensure_material("M_Roof_Terracotta", (0.75, 0.28, 0.12, 1), roughness=0.7)
    mat_stone = ensure_material("M_Stone_Plinth", (0.45, 0.42, 0.38, 1), roughness=0.9)

    # footprint ~5x4, ridge ~3.2
    body = add_cube("Body", (5.0, 4.0, 2.2), location=(0, 0, 1.25))
    apply_all_transforms(body)
    assign_mat(body, mat_plaster)

    plinth = add_cube("Plinth", (5.2, 4.2, 0.25), location=(0, 0, 0.12))
    apply_all_transforms(plinth)
    assign_mat(plinth, mat_stone)

    roof_l = add_cube("Roof_L", (5.4, 2.8, 0.15), location=(0, 0, 2.85))
    roof_l.rotation_euler[0] = math.radians(30)
    apply_all_transforms(roof_l)
    assign_mat(roof_l, mat_roof)
    roof_r = add_cube("Roof_R", (5.4, 2.8, 0.15), location=(0, 0, 2.85))
    roof_r.rotation_euler[0] = math.radians(-30)
    apply_all_transforms(roof_r)
    assign_mat(roof_r, mat_roof)

    # 2 porch posts toward +Y
    posts = []
    for i, x in enumerate((-1.6, 1.6)):
        p = add_cylinder(f"Post_{i}", 0.08, 1.8, location=(x, 2.3, 1.05), vertices=8)
        apply_all_transforms(p)
        assign_mat(p, mat_wood)
        posts.append(p)

    name = f"Bldg_KeralaHouse_B_Distant{suffix}"
    obj = join_objects([body, plinth, roof_l, roof_r] + posts, name)
    set_origin_world(obj, Vector((0, 0, 0)))
    export_fbx(OUT / f"{name}.fbx", [obj])
    print("OK", name, len(obj.data.polygons), "faces")
    return obj


def build_banana_clump():
    """Veg_BananaClump — H2.5–3.5m, ground pivot, no collider."""
    clear_scene()
    mat_leaf = ensure_material("M_Banana_Leaf", (0.22, 0.55, 0.14, 1), roughness=0.65)
    mat_stem = ensure_material("M_Banana_Stem", (0.35, 0.45, 0.18, 1), roughness=0.8)

    parts = []
    # 3 stems with leaf cards
    for i, (x, y, h) in enumerate([(-0.35, 0.1, 3.0), (0.25, -0.2, 2.6), (0.05, 0.35, 3.3)]):
        stem = add_cylinder(f"Stem_{i}", 0.07, h, location=(x, y, h * 0.5), vertices=8)
        apply_all_transforms(stem)
        assign_mat(stem, mat_stem)
        parts.append(stem)
        for j in range(4):
            ang = j * (math.pi * 0.5) + i * 0.3
            leaf = add_cube(f"Leaf_{i}_{j}", (0.08, 1.4, 0.04), location=(x + math.sin(ang) * 0.25, y + math.cos(ang) * 0.25, h * 0.75))
            leaf.rotation_euler = (math.radians(40), 0, ang)
            apply_all_transforms(leaf)
            assign_mat(leaf, mat_leaf)
            parts.append(leaf)

    obj = join_objects(parts, "Veg_BananaClump")
    set_origin_world(obj, Vector((0, 0, 0)))
    export_fbx(OUT / "Veg_BananaClump.fbx", [obj])
    print("OK Veg_BananaClump", len(obj.data.polygons), "faces")
    return obj


def build_bank_plant():
    """Veg_BankPlant_Low — H0.3–0.6m scatter plant, no collider."""
    clear_scene()
    mat = ensure_material("M_Herb_Green", (0.18, 0.48, 0.16, 1), roughness=0.7)
    parts = []
    for i in range(7):
        ang = i * (math.pi * 2 / 7)
        r = 0.12 + (i % 3) * 0.04
        h = 0.35 + (i % 4) * 0.06
        blade = add_cube(f"Blade_{i}", (0.03, 0.08, h), location=(math.sin(ang) * r, math.cos(ang) * r, h * 0.5))
        blade.rotation_euler = (math.radians(12), 0, ang)
        apply_all_transforms(blade)
        assign_mat(blade, mat)
        parts.append(blade)

    obj = join_objects(parts, "Veg_BankPlant_Low")
    set_origin_world(obj, Vector((0, 0, 0)))
    export_fbx(OUT / "Veg_BankPlant_Low.fbx", [obj])
    print("OK Veg_BankPlant_Low", len(obj.data.polygons), "faces")
    return obj


def main():
    print("=== Pit Striker Phase 2 P1 generate ===")
    print("Output:", OUT)
    build_house_b("")
    # two slight variants for density (same mesh, separate exports for placement clarity)
    build_house_b("_B")
    build_house_b("_C")
    build_banana_clump()
    build_bank_plant()
    print("=== ALL P1 DONE ===")
    for p in sorted(OUT.glob("*.fbx")):
        print(" ", p.name, p.stat().st_size, "bytes")


if __name__ == "__main__":
    main()
