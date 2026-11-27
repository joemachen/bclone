"""The "Kith" wordmark lockup (A301, `specs/logo.md §6`): the seal with the name beside it, for store pages and trailers.

    python -X utf8 -I press/make_wordmark.py

Reads the game's own seal (`textures/ui/screens/seal-k.png`, 512 px) and the title's font (Grenze Gotisch, the variable font at
weight 400, its default — the K pressed into the seal is the same face) and writes, beside this script:

- `kith-wordmark.png`        the name in the title's ink (#2a2018), for light pages;
- `kith-wordmark-light.png`  the name in the paper's light (#efe3c8), for dark pages and trailers;
- `kith-wordmark-sheet.jpg`  both, on the valley's paper and on a dark ground, to look at.

Transparent, the seal at its own size (never scaled up). The name's capitals stand centred on the wax, their height half the
wax's width; the gap between the wax and the K a fifth of it. Nothing here is drawn by the game: it is a picture for others.
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
GAME = HERE.parent / "src" / "Kith.Game"
SEAL = GAME / "textures" / "ui" / "screens" / "seal-k.png"
FONT = GAME / "fonts" / "GrenzeGotisch" / "GrenzeGotisch-VariableFont_wght.ttf"

INK = (0x2A, 0x20, 0x18)
LIGHT = (0xEF, 0xE3, 0xC8)
PAPER = (0xE6, 0xD6, 0xB0)   # the valley's ground_srgb (0.902, 0.839, 0.690)
DARK = (0x1C, 0x17, 0x12)

CAPS_OF_WAX = 0.5    # the capitals' height over the wax's width
GAP_OF_WAX = 0.2     # the gap, wax to K, over the wax's width
MARGIN = 48


def font_at(size):
    font = ImageFont.truetype(str(FONT), size)
    try:
        font.set_variation_by_axes([400])
    except OSError:
        pass   # a static build of the font: already 400
    return font


def main():
    seal = Image.open(SEAL).convert("RGBA")
    wl, wt, wr, wb = seal.getchannel("A").point(lambda a: 255 if a > 128 else 0).getbbox()
    wax = wr - wl
    centre_y = (wt + wb) / 2

    # Size the face so the K's ink is CAPS_OF_WAX of the wax.
    probe = font_at(400)
    k = probe.getbbox("K")
    size = round(400 * (CAPS_OF_WAX * wax) / (k[3] - k[1]))
    font = font_at(size)
    word = font.getbbox("Kith")          # relative to the text's origin
    k = font.getbbox("K")
    caps_mid = (k[1] + k[3]) / 2

    gap = round(GAP_OF_WAX * wax)
    left = MARGIN - wl
    text_x = left + wr + gap - word[0]
    seal_top = MARGIN - min(wt, 0)
    text_y = round(seal_top + centre_y - caps_mid)
    width = text_x + word[2] + MARGIN
    top = min(seal_top + wt, text_y + word[1])
    bottom = max(seal_top + wb, text_y + word[3])
    height = round(bottom - top) + 2 * MARGIN
    shift = MARGIN - round(top)

    made = {}
    for name, colour in (("kith-wordmark.png", INK), ("kith-wordmark-light.png", LIGHT)):
        canvas = Image.new("RGBA", (round(width), height), (0, 0, 0, 0))
        canvas.alpha_composite(seal, (round(left), round(seal_top + shift)))
        ImageDraw.Draw(canvas).text((text_x, text_y + shift), "Kith", font=font, fill=colour + (255,))
        canvas.save(HERE / name)
        made[name] = canvas
        print(name, canvas.size, "face", size, "px; wax", wax, "px; capitals", k[3] - k[1], "px")

    a, b = made["kith-wordmark.png"], made["kith-wordmark-light.png"]
    sheet = Image.new("RGB", (a.width, a.height * 2), PAPER)
    sheet.paste(DARK, (0, a.height, a.width, a.height * 2))
    sheet.paste(a, (0, 0), a)
    sheet.paste(b, (0, a.height), b)
    sheet.save(HERE / "kith-wordmark-sheet.jpg", quality=92)
    print("kith-wordmark-sheet.jpg", sheet.size)


if __name__ == "__main__":
    main()
