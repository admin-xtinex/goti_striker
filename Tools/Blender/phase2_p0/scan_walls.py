from pathlib import Path
import re
text = Path(r"C:\Users\Tisan\Documents\PitStriker-Working\pitstricker\Assets\_Project\Scenes\SC_Village_Graphics_Test.unity").read_text(encoding="utf-8", errors="replace")
for wall in ["Wall_Front","Wall_Left","Wall_Back","Wall_Right"]:
    m = re.search(rf"--- !u!1 &(\d+)\n(?:.*\n)*?  m_Name: {wall}\n(?:.*\n)*?  m_IsActive: (\d)", text)
    if m:
        print(wall, "id", m.group(1), "active", m.group(2))
    else:
        # looser
        idx = text.find(f"m_Name: {wall}")
        print(wall, "idx", idx)
        print(text[idx-200:idx+400])
