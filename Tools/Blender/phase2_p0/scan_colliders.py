from pathlib import Path
import re
p = Path(r"C:\Users\Tisan\Documents\PitStriker-Working\pitstricker\Assets\_Project\Scenes\SC_Village_Graphics_Test.unity")
print("mtime", p.stat().st_mtime, "size", p.stat().st_size)
text = p.read_text(encoding="utf-8", errors="replace")
print("BoxCollider:", text.count("BoxCollider:"))
print("CapsuleCollider:", text.count("CapsuleCollider:"))
print("MeshCollider:", text.count("MeshCollider:"))
print("SphereCollider:", text.count("SphereCollider:"))
# find Kerala house context for BoxCollider
for m in re.finditer(r"m_Name: P2_KeralaHouse_A", text):
    start = max(0, m.start()-500)
    end = min(len(text), m.end()+2500)
    chunk = text[start:end]
    print("--- Kerala chunk has BoxCollider", "BoxCollider:" in chunk)
    print(chunk[:800])
# palm names
palms = re.findall(r"m_Name: (P2_Palm_[^\n]+)", text)
print("palm names", len(palms), palms[:20])
# Capsule near palms - count CapsuleCollider components
print("done")
