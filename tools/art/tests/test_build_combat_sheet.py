import tempfile
import unittest
from pathlib import Path

from PIL import Image, ImageDraw

from tools.art.build_combat_sheet import build_sheet


class BuildCombatSheetTests(unittest.TestCase):
    def test_build_packs_grid_in_chronological_order_with_transparent_margin(self):
        colors = [(255, 0, 0, 255), (0, 255, 0, 255), (0, 0, 255, 255), (255, 255, 0, 255)]
        source = Image.new("RGBA", (40, 40), (0, 0, 0, 0))
        draw = ImageDraw.Draw(source)
        for index, color in enumerate(colors):
            column, row = index % 2, index // 2
            left, top = column * 20, row * 20
            draw.rectangle((left + 5, top + 5, left + 14, top + 18), fill=color)

        with tempfile.TemporaryDirectory() as directory:
            source_path = Path(directory) / "contact.png"
            output_path = Path(directory) / "idle.png"
            source.save(source_path)
            build_sheet(source_path, rows=2, columns=2, frame_count=4, cell_size=16, output=output_path)
            output = Image.open(output_path)
            output.load()

        self.assertEqual(output.mode, "RGBA")
        self.assertEqual(output.size, (4 * 16, 16))
        self.assertEqual(output.getpixel((0, 0))[3], 0)
        self.assertEqual(output.getpixel((15, 15))[3], 0)
        self.assertEqual([output.getpixel((index * 16 + 8, 11)) for index in range(4)], colors)
        self.assertEqual([output.getpixel((index * 16 + 8, 15))[3] for index in range(4)], [0] * 4)

        frame_hash_count = len(
            {
                output.crop((index * 16, 0, (index + 1) * 16, 16)).tobytes()
                for index in range(4)
            }
        )
        self.assertGreater(frame_hash_count, 1)

    def test_build_rejects_frame_count_larger_than_grid(self):
        with tempfile.TemporaryDirectory() as directory:
            source_path = Path(directory) / "contact.png"
            Image.new("RGBA", (20, 20)).save(source_path)
            with self.assertRaisesRegex(ValueError, "frame_count"):
                build_sheet(source_path, rows=1, columns=1, frame_count=2, cell_size=16, output=Path(directory) / "out.png")


if __name__ == "__main__":
    unittest.main()
