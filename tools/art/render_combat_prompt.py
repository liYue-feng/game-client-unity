"""Render a fixed production prompt from a combat-art catalog entry."""

import argparse
import json
from pathlib import Path


_INVARIANTS = """Use case: stylized-concept
Asset type: production 2D side-scrolling game animation contact sheet
Primary request: create the exact action described by this catalog entry for the same character as Image 1
Style/medium: original Q-version Chinese ink-wash game sprite art, full raster redraw per frame
Composition/framing: exact equal grid declared by the catalog, chronological left-to-right then top-to-bottom, one complete full body centered in every cell, feet on one shared baseline, default facing right
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for local removal, no grid lines
Constraints: preserve face, proportions, costume, palette and weapon from Image 1; complete redrawn body in every frame; visible center-of-mass, limb, clothing and ink-trail changes; generous cell padding; no text; no watermark; no logo; no signature; no cast shadow; no paper texture; no checkerboard; do not use #ff00ff in the subject
Avoid: paper-doll motion, duplicated frames, cropped weapons, extra limbs, merged hands, camera movement, perspective changes, multiple characters in one cell"""


def _entries(catalog: dict) -> list[dict]:
    return catalog.get("assets", catalog if isinstance(catalog, list) else [])


def render_prompt(catalog: dict, asset_id: str) -> str:
    """Return the invariant prompt augmented by one catalog asset's metadata."""
    entry = next((item for item in _entries(catalog) if item.get("asset_id") == asset_id), None)
    if entry is None:
        raise KeyError(f"unknown asset_id: {asset_id}")

    return "\n".join(
        [
            f"Catalog asset: {entry['asset_id']} ({entry['role']} / {entry['action']})",
            f"Grid: {entry['rows']} rows by {entry['columns']} columns, {entry['frame_count']} frames, source {entry['source_size']}",
            f"Action: {entry['action_description']}",
            _INVARIANTS,
        ]
    )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("asset_id")
    parser.add_argument(
        "--catalog",
        type=Path,
        default=Path(__file__).resolve().parents[2] / "SourceArt" / "Generated" / "prompt-catalog.json",
    )
    arguments = parser.parse_args()
    catalog = json.loads(arguments.catalog.read_text(encoding="utf-8"))
    print(render_prompt(catalog, arguments.asset_id))


if __name__ == "__main__":
    main()
