from PIL import Image, ImageDraw
import os
out = r"C:\Users\Tisan\Documents\PitStriker-Working\pitstricker\Assets\_Project\GameplayKit\UI\Textures"
os.makedirs(out, exist_ok=True)

def save(img, name):
    p = os.path.join(out, name)
    img.save(p, optimize=True)
    print("OK", name, os.path.getsize(p))

# Rounded rect helper
def round_rect(draw, box, r, fill, outline=None, width=2):
    draw.rounded_rectangle(box, radius=r, fill=fill, outline=outline, width=width)

# --- Toggle track (inactive/back) ---
W,H = 280, 72
img = Image.new("RGBA", (W,H), (0,0,0,0))
d = ImageDraw.Draw(img)
round_rect(d, (2,2,W-3,H-3), 36, (20,24,28,200), (255,255,255,40), 2)
save(img, "UI_ShotToggle_Track.png")

# --- Toggle pill ACTIVE (warm amber) ---
pw, ph = 140, 64
img = Image.new("RGBA", (pw,ph), (0,0,0,0))
d = ImageDraw.Draw(img)
round_rect(d, (2,2,pw-3,ph-3), 32, (232,158,48,255), (255,220,140,220), 2)
save(img, "UI_ShotToggle_PillActive.png")

# --- Toggle pill INACTIVE (cool slate) ---
img = Image.new("RGBA", (pw,ph), (0,0,0,0))
d = ImageDraw.Draw(img)
round_rect(d, (2,2,pw-3,ph-3), 32, (48,56,64,160), (180,190,200,80), 2)
save(img, "UI_ShotToggle_PillInactive.png")

# --- Power area frame (right-side dedicated zone) ---
fw, fh = 220, 520
img = Image.new("RGBA", (fw,fh), (0,0,0,0))
d = ImageDraw.Draw(img)
# translucent panel
round_rect(d, (4,4,fw-5,fh-5), 24, (12,16,22,120), (90,200,255,180), 3)
# inner dashed-like ticks
for y in range(40, fh-40, 36):
    d.line((18, y, fw-18, y), fill=(90,200,255,50), width=1)
# chevron hint (swipe back = up on portrait? for landscape right: swipe toward bottom of screen)
# draw upward arrow stack meaning pull-back
cx = fw//2
for i,y in enumerate([fh-90, fh-130, fh-170]):
    a = 180 - i*40
    d.polygon([(cx, y-18),(cx-22, y+8),(cx+22, y+8)], fill=(90,200,255,a))
save(img, "UI_PowerArea_Frame.png")

# --- Power fill bar vertical ---
bw, bh = 28, 400
img = Image.new("RGBA", (bw,bh), (0,0,0,0))
d = ImageDraw.Draw(img)
round_rect(d, (2,2,bw-3,bh-3), 12, (255,255,255,40), (255,255,255,90), 1)
save(img, "UI_PowerBar_Track.png")
img = Image.new("RGBA", (bw,bh), (0,0,0,0))
d = ImageDraw.Draw(img)
# gradient-ish solid warm fill
for y in range(bh):
    t = 1.0 - y/float(bh)
    r = int(0 + 255*t); g = int(200 - 120*t); b = int(255 - 200*t)
    d.line((4, y, bw-5, y), fill=(r,g,b,230))
save(img, "UI_PowerBar_Fill.png")

# --- Finger icon for tutorial ---
iw, ih = 128, 160
img = Image.new("RGBA", (iw,ih), (0,0,0,0))
d = ImageDraw.Draw(img)
# simple finger silhouette
d.ellipse((36, 8, 92, 70), fill=(255,224,196,255), outline=(60,40,30,200), width=2)
d.rounded_rectangle((44, 55, 84, 150), radius=18, fill=(255,224,196,255), outline=(60,40,30,200), width=2)
# knuckle line
d.arc((40, 70, 88, 110), 200, 340, fill=(60,40,30,120), width=2)
save(img, "UI_Finger_Tutorial.png")

# --- Finger swipe ghost trail ---
img = Image.new("RGBA", (64, 200), (0,0,0,0))
d = ImageDraw.Draw(img)
for i,y in enumerate([20,60,100,140,180]):
    a = 40 + i*40
    d.ellipse((16, y-12, 48, y+12), fill=(255,255,255,a))
save(img, "UI_Finger_Trail.png")

print("DONE")
