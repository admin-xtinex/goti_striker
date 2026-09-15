from pathlib import Path
import re
text = Path(r"C:\Users\Tisan\Documents\PitStriker-Working\pitstricker\Assets\_Project\Scenes\SC_Village_Graphics_Test.unity").read_text(encoding="utf-8", errors="replace")
# map fileID -> name from GameObject blocks
# Unity YAML: --- !u!1 &ID then m_Name
id_to_name = {}
for m in re.finditer(r"--- !u!1 &(\d+)\n(?:.*\n)*?  m_Name: ([^\n]+)", text):
    id_to_name[m.group(1)] = m.group(2)
# BoxCollider GameObject refs
for m in re.finditer(r"BoxCollider:\n((?:  [^\n]*\n)+)", text):
    block = m.group(0)
    go = re.search(r"m_GameObject: \{fileID: (\d+)\}", block)
    en = re.search(r"m_Enabled: (\d)", block)
    size = re.search(r"m_Size: \{([^}]+)\}", block)
    gid = go.group(1) if go else "?"
    name = id_to_name.get(gid, "UNKNOWN")
    print(f"Box on '{name}' id={gid} enabled={en.group(1) if en else '?'} size={size.group(1) if size else '?'}")
print("--- capsules sample ---")
count=0
for m in re.finditer(r"CapsuleCollider:\n((?:  [^\n]*\n)+)", text):
    block=m.group(0)
    go = re.search(r"m_GameObject: \{fileID: (\d+)\}", block)
    height = re.search(r"m_Height: ([^\n]+)", block)
    radius = re.search(r"m_Radius: ([^\n]+)", block)
    gid = go.group(1) if go else "?"
    name = id_to_name.get(gid, "UNKNOWN")
    print(f"Cap on '{name}' h={height.group(1) if height else '?'} r={radius.group(1) if radius else '?'}")
    count += 1
print("total caps", count)
