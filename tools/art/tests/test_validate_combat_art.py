import tempfile
import unittest
from pathlib import Path

from PIL import Image

from tools.art.validate_combat_art import ValidationError, validate_asset


def write_sheet(path: Path, frames: list[Image.Image]) -> None:
    sheet = Image.new("RGBA", (len(frames) * 16, 16), (0, 0, 0, 0))
    for index, frame in enumerate(frames):
        sheet.alpha_composite(frame, (index * 16, 0))
    sheet.save(path)


def valid_frame(color: tuple[int, int, int, int]) -> Image.Image:
    frame = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    for x in range(4, 12):
        for y in range(3, 14):
            frame.putpixel((x, y), color)
    return frame


class ValidateCombatArtTests(unittest.TestCase):
    def test_validate_accepts_distinct_transparent_rgba_frames(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "idle.png"
            write_sheet(path, [valid_frame((255, 0, 0, 255)), valid_frame((0, 255, 0, 255))])
            validate_asset({"path": path, "frame_count": 2, "cell_size": 16})

    def test_validate_rejects_wrong_dimensions_and_missing_alpha(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "idle.jpg"
            Image.new("RGB", (32, 16), (255, 0, 0)).save(path)
            with self.assertRaisesRegex(ValidationError, "alpha"):
                validate_asset({"path": path, "frame_count": 2, "cell_size": 16})
            with self.assertRaisesRegex(ValidationError, "dimensions"):
                validate_asset({"path": path, "frame_count": 1, "cell_size": 16})

    def test_validate_rejects_opaque_corners_identical_frames_and_edge_touching_alpha(self):
        with tempfile.TemporaryDirectory() as directory:
            directory_path = Path(directory)
            corner = directory_path / "corner.png"
            frame = valid_frame((255, 0, 0, 255))
            frame.putpixel((0, 0), (255, 0, 0, 255))
            write_sheet(corner, [frame, valid_frame((0, 255, 0, 255))])
            with self.assertRaisesRegex(ValidationError, "opaque corners"):
                validate_asset({"path": corner, "frame_count": 2, "cell_size": 16})

            identical = directory_path / "identical.png"
            same = valid_frame((255, 0, 0, 255))
            write_sheet(identical, [same, same])
            with self.assertRaisesRegex(ValidationError, "identical consecutive"):
                validate_asset({"path": identical, "frame_count": 2, "cell_size": 16})

            edge = directory_path / "edge.png"
            touching = valid_frame((255, 0, 0, 255))
            touching.putpixel((0, 5), (255, 0, 0, 255))
            write_sheet(edge, [touching, valid_frame((0, 255, 0, 255))])
            with self.assertRaisesRegex(ValidationError, "edge-touching alpha"):
                validate_asset({"path": edge, "frame_count": 2, "cell_size": 16})

    def test_validate_rejects_forbidden_filename_tokens(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "idle-preview.png"
            write_sheet(path, [valid_frame((255, 0, 0, 255)), valid_frame((0, 255, 0, 255))])
            with self.assertRaisesRegex(ValidationError, "forbidden filename"):
                validate_asset({"path": path, "frame_count": 2, "cell_size": 16})


if __name__ == "__main__":
    unittest.main()
