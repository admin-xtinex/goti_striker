import bpy
from pathlib import Path
out = Path(r"C:\Users\Tisan\Documents\PitStriker-Working\pitstricker\Tools\Blender\phase2_p0\output")
for fbx in sorted(out.glob("*.fbx")):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx))
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not objs:
        print(f"FAIL {fbx.name}: no meshes")
        continue
    # world bounds
    import mathutils
    minv = mathutils.Vector((1e9,1e9,1e9))
    maxv = mathutils.Vector((-1e9,-1e9,-1e9))
    mats=set()
    faces=0
    for o in objs:
        for p in o.bound_box:
            wp = o.matrix_world @ mathutils.Vector(p)
            minv = mathutils.Vector((min(minv.x,wp.x), min(minv.y,wp.y), min(minv.z,wp.z)))
            maxv = mathutils.Vector((max(maxv.x,wp.x), max(maxv.y,wp.y), max(maxv.z,wp.z)))
        faces += len(o.data.polygons)
        for s in o.material_slots:
            if s.material: mats.add(s.material.name)
        print(f"  obj {o.name} loc={tuple(round(c,3) for c in o.location)} scale={tuple(round(c,3) for c in o.scale)}")
    size = maxv-minv
    print(f"OK {fbx.name}: faces={faces} size=({size.x:.2f},{size.y:.2f},{size.z:.2f}) zmin={minv.z:.3f} mats={sorted(mats)}")
