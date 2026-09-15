from pathlib import Path
import re
p = Path(r"C:\Users\Tisan\Documents\PitStriker-Working\pitstricker\Assets\_Project\Scenes\SC_Village_Graphics_Test.unity")
text = p.read_text(encoding="utf-8", errors="replace")
for key in ["Kerala", "Palm", "CapsuleCollider", "P2_"]:
    print(key, text.count(key))
# all m_Name containing P2 or Palm or House
names = re.findall(r"^\s*m_Name: (.+)$", text, re.M)
interesting = [n for n in names if any(x in n for x in ("P2_", "Palm", "Kerala", "House", "Phase2"))]
from collections import Counter
print("--- interesting names ---")
for k,v in Counter(interesting).most_common(50):
    print(f"{v:3} {k}")
# extract each CapsuleCollider block briefly
caps = list(re.finditer(r"CapsuleCollider:\n((?:  [^\n]*\n)+)", text))
print("capsule blocks", len(caps))
for i,c in enumerate(caps[:3]):
    print("CAP", i, c.group(0)[:300])
# BoxCollider blocks - see sizes
boxes = list(re.finditer(r"BoxCollider:\n((?:  [^\n]*\n)+)", text))
print("box blocks", len(boxes))
for i,b in enumerate(boxes):
    block=b.group(0)
    if "m_Enabled: 1" in block or "m_Enabled: 0" in block:
        en = re.search(r"m_Enabled: (\d)", block)
        size = re.search(r"m_Size: \{([^}]+)\}", block)
        center = re.search(r"m_Center: \{([^}]+)\}", block)
        print(f"box{i} enabled={en.group(1) if en else '?'} size={size.group(1) if size else '?'} center={center.group(1) if center else '?'}")
