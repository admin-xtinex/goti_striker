import bpy
from pathlib import Path
import mathutils
out = Path(r"C:\Users\Tisan\Documents\PitStriker-Working\pitstricker\Tools\Blender\phase2_p1\output")
for fbx in sorted(out.glob("*.fbx")):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx))
    objs=[o for o in bpy.context.scene.objects if o.type=="MESH"]
    minv=mathutils.Vector((1e9,1e9,1e9)); maxv=mathutils.Vector((-1e9,-1e9,-1e9)); faces=0; mats=set()
    for o in objs:
        for p in o.bound_box:
            wp=o.matrix_world @ mathutils.Vector(p)
            minv=mathutils.Vector((min(minv.x,wp.x),min(minv.y,wp.y),min(minv.z,wp.z)))
            maxv=mathutils.Vector((max(maxv.x,wp.x),max(maxv.y,wp.y),max(maxv.z,wp.z)))
        faces += len(o.data.polygons)
        for s in o.material_slots:
            if s.material: mats.add(s.material.name)
    size=maxv-minv
    print(f"OK {fbx.name}: meshes={len(objs)} faces={faces} size=({size.x:.2f},{size.y:.2f},{size.z:.2f}) zmin={minv.z:.3f} mats={sorted(mats)}")
