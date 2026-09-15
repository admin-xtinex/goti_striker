# Foundation fence module

## Output
`output/Prop_Fence_Module.fbx`

## Specs
- Span: 2.0 m
- Height: ~1.2 m (posts plant ~0.08 m below ground)
- Post diameter: 0.08 m
- Ground pivot at module centre (0,0,0)
- Scales applied, no stray meshes
- Materials: `M_Bamboo_Fence`, `M_Fence_Rope`

## Run
```
blender --background --python Tools/Blender/foundation_fence/generate_fence_module.py
```

## Unity
Import with Generate Colliders OFF. Tile along +X at 2.0 m centres for straight rows.
