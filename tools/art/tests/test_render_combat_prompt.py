import unittest

from tools.art.render_combat_prompt import render_prompt


class RenderCombatPromptTests(unittest.TestCase):
    def test_render_includes_catalog_action_and_fixed_production_invariants(self):
        catalog = {
            "assets": [
                {
                    "asset_id": "player-idle",
                    "role": "Player",
                    "action": "Idle",
                    "frame_count": 6,
                    "rows": 2,
                    "columns": 3,
                    "source_size": "1536x1024",
                    "cell_size": 256,
                    "target": "Assets/Resources/CombatArt/Player/Idle.png",
                    "action_description": "subtle breathing cycle",
                }
            ]
        }

        prompt = render_prompt(catalog, "player-idle")

        self.assertIn("subtle breathing cycle", prompt)
        self.assertIn("2 rows by 3 columns", prompt)
        self.assertIn("perfectly flat solid #ff00ff chroma-key background", prompt)
        self.assertIn("no watermark", prompt)
        self.assertIn("complete redrawn body in every frame", prompt)

    def test_render_rejects_unknown_asset(self):
        with self.assertRaisesRegex(KeyError, "unknown asset_id"):
            render_prompt({"assets": []}, "missing")


if __name__ == "__main__":
    unittest.main()
