"""Draw ZzPathShape's grids (D579): one PNG per grid, or several side by side.

    python -X utf8 -I tools/harness/pathshape.py OUT.png A.grid [B.grid ...]

~ water, # a building, p packed, w worn, , a field, . ground. Each grid is drawn at 8 px a tile with its file name
over it, left to right.
"""
import sys

from PIL import Image, ImageDraw

COLOURS = {
    '.': (43, 51, 50),
    '~': (47, 95, 122),
    '#': (185, 138, 82),
    ',': (74, 58, 43),
    'w': (150, 130, 95),
    'p': (225, 205, 160),
}
CELL = 8
LABEL = 16


def draw(path):
    rows = [line.rstrip('\n') for line in open(path, encoding='utf-8') if line.strip()]
    w = max(len(r) for r in rows)
    image = Image.new('RGB', (w * CELL, len(rows) * CELL + LABEL), (20, 24, 24))
    pen = ImageDraw.Draw(image)
    pen.text((4, 2), path.replace('\\', '/').split('/')[-1], fill=(230, 230, 230))
    for y, row in enumerate(rows):
        for x, c in enumerate(row):
            pen.rectangle([x * CELL, LABEL + y * CELL, x * CELL + CELL - 1, LABEL + y * CELL + CELL - 1],
                          fill=COLOURS.get(c, (255, 0, 255)))
    return image


def main():
    out, grids = sys.argv[1], sys.argv[2:]
    images = [draw(g) for g in grids]
    gap = 12
    sheet = Image.new('RGB', (sum(i.width for i in images) + gap * (len(images) - 1),
                              max(i.height for i in images)), (10, 10, 10))
    x = 0
    for i in images:
        sheet.paste(i, (x, 0))
        x += i.width + gap
    sheet.save(out)


if __name__ == '__main__':
    main()
