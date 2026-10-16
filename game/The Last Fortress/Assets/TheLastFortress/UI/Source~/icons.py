"""
The Last Fortress — HUD icons rasterized from the line-icon style in design/icons/ (64-unit grid, 3.5 stroke, round
caps and joins, white on transparent; the HUD tints them). Wall, Archer Tower and Gold follow the existing SVG paths;
House, Farm and Spike Trap are new drawings in the same style; Cannon, Watchtower and Repair Station were added for plan 008. Pillow only, no SVG dependency.

    python icons.py <output directory>
"""
import os
import sys

from PIL import Image, ImageDraw

SIZE = 128          # output pixels
SS = 4              # supersampling
SCALE = SIZE * SS / 64.0
STROKE = 3.5 * SCALE


def P(x, y):
    return (x * SCALE, y * SCALE)


def polyline(d, pts, closed=False):
    pts = [P(*p) for p in pts]
    if closed:
        pts = pts + [pts[0]]
    d.line(pts, fill=(255, 255, 255, 255), width=int(round(STROKE)), joint="curve")
    r = STROKE / 2
    for x, y in pts:  # round caps and joins
        d.ellipse((x - r, y - r, x + r, y + r), fill=(255, 255, 255, 255))


def circle(d, cx, cy, radius):
    x, y = P(cx, cy)
    rr = radius * SCALE
    d.ellipse((x - rr, y - rr, x + rr, y + rr), outline=(255, 255, 255, 255), width=int(round(STROKE)))


def wall(d):
    for x in (10, 22, 34, 46):
        polyline(d, [(x, 54), (x, 20), (x + 6, 11), (x + 12, 20), (x + 12, 54)])
    for y in (54, 32, 44):
        polyline(d, [(10, y), (58, y)])


def tower(d):
    polyline(d, [(9, 20), (32, 7), (55, 20)], closed=True)
    polyline(d, [(16, 20), (16, 36), (48, 36), (48, 20)])
    polyline(d, [(12, 36), (52, 36)])
    polyline(d, [(20, 36), (16, 56)])
    polyline(d, [(44, 36), (48, 56)])
    polyline(d, [(20, 40), (44, 56)])
    polyline(d, [(44, 40), (20, 56)])
    polyline(d, [(24, 26), (24, 36)])
    polyline(d, [(40, 26), (40, 36)])


def house(d):
    polyline(d, [(42, 18), (42, 9), (49, 9), (49, 24)])            # chimney
    polyline(d, [(7, 31), (32, 10), (57, 31)])                     # roof
    polyline(d, [(14, 26), (14, 55), (50, 55), (50, 26)])          # walls
    polyline(d, [(27, 55), (27, 42), (37, 42), (37, 55)])          # door
    polyline(d, [(19, 35), (23, 35), (23, 39), (19, 39)], closed=True)  # window


def farm(d):
    polyline(d, [(8, 30), (56, 30), (56, 56), (8, 56)], closed=True)   # plot
    for y in (38.5, 47):
        polyline(d, [(8, y), (56, y)])                                 # furrows
    for x in (20, 32, 44):
        polyline(d, [(x, 30), (x, 22)])                                # sprouts
        polyline(d, [(x, 24), (x - 5, 18)])
        polyline(d, [(x, 24), (x + 5, 18)])


def trap(d):
    polyline(d, [(8, 48), (56, 48), (56, 56), (8, 56)], closed=True)   # plate
    for x in (10, 22, 34, 46):
        polyline(d, [(x, 48), (x + 4, 18), (x + 8, 48)])               # spikes


def cannon(d):
    circle(d, 18, 46, 9)                                           # wheel
    circle(d, 18, 46, 2)
    polyline(d, [(12, 40), (50, 22), (54, 30), (20, 50)], closed=True)  # barrel
    polyline(d, [(50, 22), (56, 19)])                              # muzzle lip
    polyline(d, [(28, 52), (44, 56)])                              # carriage trail


def watchtower(d):
    polyline(d, [(22, 14), (32, 6), (42, 14)], closed=True)        # roof
    polyline(d, [(24, 14), (24, 22), (40, 22), (40, 14)])          # lookout
    polyline(d, [(26, 22), (20, 56)])                              # legs
    polyline(d, [(38, 22), (44, 56)])
    polyline(d, [(24, 34), (40, 34)])                              # braces
    polyline(d, [(22, 46), (42, 46)])
    polyline(d, [(42, 8), (42, 2), (52, 5), (42, 8)])              # pennant


def repair(d):
    polyline(d, [(8, 56), (8, 30), (24, 30), (24, 56)])            # workshop
    polyline(d, [(5, 31), (16, 20), (27, 31)])
    polyline(d, [(34, 56), (46, 18)])                              # crane mast
    polyline(d, [(40, 22), (58, 22)])                              # jib
    polyline(d, [(56, 22), (56, 34)])                              # hook line
    polyline(d, [(52, 34), (56, 38), (60, 34)])                    # hook
    polyline(d, [(28, 56), (58, 56)])                              # ground


def gold(d):
    circle(d, 32, 32, 23)
    circle(d, 32, 32, 16)
    polyline(d, [(32, 22), (39, 32), (32, 42), (25, 32)], closed=True)


def pan_cursor(out):
    """32 x 32 hardware cursor for right-drag panning: four-way move arrows around a grip dot, ivory with a slate
    outline and a soft shadow, so it reads on day grass and night ground alike. Hotspot = centre."""
    from PIL import ImageFilter
    n, k = 32, 8
    big = n * k
    c = big / 2
    arm, head, shaft = 13.5 * k, 4.2 * k, 1.25 * k
    shape = Image.new("L", (big, big), 0)
    d = ImageDraw.Draw(shape)
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        px, py = -dy, dx  # perpendicular
        tip = (c + dx * arm, c + dy * arm)
        base = (c + dx * (arm - head), c + dy * (arm - head))
        d.polygon([tip, (base[0] + px * head, base[1] + py * head), (base[0] - px * head, base[1] - py * head)], fill=255)
        d.polygon([(c + px * shaft, c + py * shaft), (base[0] + px * shaft, base[1] + py * shaft),
                   (base[0] - px * shaft, base[1] - py * shaft), (c - px * shaft, c - py * shaft)], fill=255)
    r = 2.9 * k
    d.ellipse((c - r, c - r, c + r, c + r), fill=255)
    outline = shape.filter(ImageFilter.MaxFilter(int(1.6 * k) | 1))
    shadow = outline.filter(ImageFilter.GaussianBlur(1.2 * k))
    img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    img.paste((0, 0, 0, 110), (int(0.8 * k), int(1.0 * k)), shadow)
    img.paste((37, 50, 58, 255), (0, 0), outline)            # HUD surface #25323A
    img.paste((240, 231, 213, 255), (0, 0), shape)           # HUD text #F0E7D5
    dot = Image.new("L", (big, big), 0)
    ImageDraw.Draw(dot).ellipse((c - 1.3 * k, c - 1.3 * k, c + 1.3 * k, c + 1.3 * k), fill=255)
    img.paste((233, 180, 95, 255), (0, 0), dot)              # amber grip
    img.resize((n, n), Image.LANCZOS).save(os.path.join(out, "cursor_pan.png"))
    print("cursor_pan.png")


ICONS = {"icon_wall": wall, "icon_tower": tower, "icon_house": house, "icon_farm": farm, "icon_trap": trap, "icon_gold": gold,
         "icon_cannon": cannon, "icon_watchtower": watchtower, "icon_repair": repair}


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(out, exist_ok=True)
    for name, draw in ICONS.items():
        img = Image.new("RGBA", (SIZE * SS, SIZE * SS), (255, 255, 255, 0))
        draw(ImageDraw.Draw(img))
        img = img.resize((SIZE, SIZE), Image.LANCZOS)
        img.save(os.path.join(out, name + ".png"))
        print(name + ".png")
    pan_cursor(out)


if __name__ == "__main__":
    main()
