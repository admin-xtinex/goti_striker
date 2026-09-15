from pathlib import Path
import re
from collections import Counter
p = Path(r"C:\Users\Tisan\Documents\PitStriker-Working\pitstricker\Assets\_Project\Scenes\SC_Village_Graphics_Test.unity")
text = p.read_text(encoding="utf-8", errors="replace")
names = re.findall(r"m_Name: (P2_[^\n]+|Phase2[^\n]*)", text)
print("named count", len(names))
for k,v in Counter(names).most_common(100):
    print(f"{v:3} {k}")
metas = list(Path(r"C:\Users\Tisan\Documents\PitStriker-Working\pitstricker\Assets\_Project\Art\Models\Phase2_P0").glob("*.meta"))
print("--- guid hits ---")
for m in metas:
    t=m.read_text(encoding="utf-8", errors="replace")
    g=re.search(r"guid: ([a-f0-9]+)", t)
    if not g: continue
    print(m.name.replace('.meta',''), text.count(g.group(1)))
print("MeshCollider blocks", text.count("MeshCollider:"))
