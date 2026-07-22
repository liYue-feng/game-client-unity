"""Validate packed combat sprite sheets before Unity import."""

import argparse
import json
import re
from pathlib import Path

from PIL import Image


class ValidationError(ValueError):
    """Raised when a combat-art file violates its manifest contract."""


_FORBIDDEN_NAME = re.compile(r"placeholder|temp|preview|rejected", re.IGNORECASE)


def validate_asset(manifest_entry: dict) -> None:
    """Raise ValidationError unless the sheet meets dimensions and alpha rules."""
    path = Path(manifest_entry["path"])
    frame_count = int(manifest_entry["frame_count"])
    cell_size = int(manifest_entry["cell_size"])
    if _FORBIDDEN_NAME.search(path.name):
        raise ValidationError("forbidden filename token")

    with Image.open(path) as image:
        if image.size != (frame_count * cell_size, cell_size):
            raise ValidationError("wrong dimensions")
        if image.mode != "RGBA":
            raise ValidationError("missing alpha")
        sheet = image.copy()

    for index in range(frame_count):
        left = index * cell_size
        frame = sheet.crop((left, 0, left + cell_size, cell_size))
        alpha = frame.getchannel("A")
        if any(alpha.getpixel(point) for point in ((0, 0), (cell_size - 1, 0), (0, cell_size - 1), (cell_size - 1, cell_size - 1))):
            raise ValidationError("opaque corners")
        if any(alpha.getpixel((x, 0)) or alpha.getpixel((x, cell_size - 1)) for x in range(cell_size)) or any(
            alpha.getpixel((0, y)) or alpha.getpixel((cell_size - 1, y)) for y in range(cell_size)
        ):
            raise ValidationError("edge-touching alpha")
        if index and frame.tobytes() == previous.tobytes():
            raise ValidationError("identical consecutive frames")
        previous = frame


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    arguments = parser.parse_args()
    manifest = json.loads(arguments.manifest.read_text(encoding="utf-8"))
    entries = manifest.get("assets", manifest if isinstance(manifest, list) else [])
    for entry in entries:
        validate_asset(entry)
    print(f"validated {len(entries)} assets")


if __name__ == "__main__":
    main()
