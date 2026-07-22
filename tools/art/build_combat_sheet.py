"""Pack a generated grid contact sheet into chronological transparent frames."""

import argparse
from pathlib import Path

from PIL import Image


def build_sheet(
    source: Path,
    rows: int,
    columns: int,
    frame_count: int,
    cell_size: int,
    output: Path,
) -> None:
    """Crop grid cells by integer boundaries and pack them left-to-right."""
    if rows < 1 or columns < 1 or cell_size < 3:
        raise ValueError("rows, columns, and cell_size must be positive")
    if frame_count < 1 or frame_count > rows * columns:
        raise ValueError("frame_count must fit inside the declared grid")

    with Image.open(source) as opened:
        contact_sheet = opened.convert("RGBA")
    width, height = contact_sheet.size
    output_image = Image.new("RGBA", (frame_count * cell_size, cell_size), (0, 0, 0, 0))
    inner_size = cell_size - 2

    for index in range(frame_count):
        row, column = divmod(index, columns)
        left = column * width // columns
        right = (column + 1) * width // columns
        top = row * height // rows
        bottom = (row + 1) * height // rows
        frame = contact_sheet.crop((left, top, right, bottom))
        frame = frame.resize((inner_size, inner_size), Image.Resampling.NEAREST)
        output_image.alpha_composite(frame, (index * cell_size + 1, 1))

    output.parent.mkdir(parents=True, exist_ok=True)
    output_image.save(output)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--rows", type=int, required=True)
    parser.add_argument("--columns", type=int, required=True)
    parser.add_argument("--frame-count", type=int, required=True)
    parser.add_argument("--cell-size", type=int, required=True)
    arguments = parser.parse_args()
    build_sheet(
        arguments.source,
        arguments.rows,
        arguments.columns,
        arguments.frame_count,
        arguments.cell_size,
        arguments.output,
    )


if __name__ == "__main__":
    main()
