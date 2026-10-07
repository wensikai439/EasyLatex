"""Regenerate the simple EasyLatex mark (requires Pillow)."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent.parent
target = root / "src" / "EasyLatex" / "Resources"
size = 256
image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
mask = Image.new("L", (size, size), 0)
ImageDraw.Draw(mask).rounded_rectangle((8, 8, 248, 248), radius=56, fill=255)
for y in range(size):
    color = tuple(round(a + (b - a) * y / size) for a, b in zip((122, 98, 218), (87, 68, 186)))
    ImageDraw.Draw(image).line((0, y, size, y), fill=(*color, 255))
image.putalpha(mask)
font = ImageFont.truetype("C:/Windows/Fonts/georgiab.ttf", 170)
draw = ImageDraw.Draw(image)
box = draw.textbbox((0, 0), "E", font=font)
draw.text(((size - (box[2] - box[0])) / 2 - box[0], (size - (box[3] - box[1])) / 2 - box[1] - 3), "E", font=font, fill="white")
image.save(target / "EasyLatex.png")
image.save(target / "EasyLatex.ico", sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
