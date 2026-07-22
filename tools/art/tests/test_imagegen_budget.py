import json
import os
import tempfile
import unittest
from decimal import Decimal
from pathlib import Path
from unittest.mock import patch

from tools.art.imagegen_budget import BudgetError, reserve_budget


class ReserveBudgetTests(unittest.TestCase):
    def test_reserve_rejects_duplicate_and_hard_limit(self):
        with tempfile.TemporaryDirectory() as directory:
            ledger = Path(directory) / "budget.json"
            ledger.write_text(
                '{"hard_limit_usd":"20.00","reservations":[]}', encoding="utf-8"
            )

            reserve_budget(ledger, "player-idle-high", Decimal("0.28"))

            with self.assertRaisesRegex(BudgetError, "duplicate operation_id"):
                reserve_budget(ledger, "player-idle-high", Decimal("0.28"))
            with self.assertRaisesRegex(BudgetError, "hard limit"):
                reserve_budget(ledger, "overflow", Decimal("19.73"))

    def test_reserve_serializes_fixed_decimals_and_replaces_sibling_temp_file(self):
        with tempfile.TemporaryDirectory() as directory:
            ledger = Path(directory) / "budget.json"
            ledger.write_text(
                '{"hard_limit_usd":"20.00","reservations":[]}', encoding="utf-8"
            )
            original_replace = os.replace

            with patch("tools.art.imagegen_budget.os.replace", wraps=original_replace) as replace:
                reserve_budget(ledger, "player-idle-high", Decimal(".2"))

            replace.assert_called_once_with(ledger.with_name("budget.json.tmp"), ledger)
            stored = json.loads(ledger.read_text(encoding="utf-8"))
            self.assertEqual(stored["hard_limit_usd"], "20.00")
            self.assertEqual(stored["reservations"][0]["estimate_usd"], "0.20")
            self.assertFalse(ledger.with_name("budget.json.tmp").exists())

    def test_reserve_rejects_non_positive_amounts(self):
        with tempfile.TemporaryDirectory() as directory:
            ledger = Path(directory) / "budget.json"
            ledger.write_text(
                '{"hard_limit_usd":"20.00","reservations":[]}', encoding="utf-8"
            )

            with self.assertRaisesRegex(BudgetError, "positive"):
                reserve_budget(ledger, "invalid", Decimal("0"))

    def test_reserve_rejects_sub_cent_estimates_before_limit_comparison(self):
        with tempfile.TemporaryDirectory() as directory:
            ledger = Path(directory) / "budget.json"
            ledger.write_text(
                '{"hard_limit_usd":"20.00","reservations":[]}', encoding="utf-8"
            )

            with self.assertRaisesRegex(BudgetError, "fractional digits"):
                reserve_budget(ledger, "overflow", Decimal("20.004"))


if __name__ == "__main__":
    unittest.main()
