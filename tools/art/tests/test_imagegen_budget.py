import json
import multiprocessing
import os
import tempfile
import unittest
from decimal import Decimal
from pathlib import Path
from unittest.mock import patch

from tools.art.imagegen_budget import BudgetError, reserve_budget


def reserve_in_process(ledger_path: str, operation_id: str, start, results) -> None:
    """Reserve one amount after every worker has been started."""
    start.wait(timeout=10)
    try:
        reserve_budget(Path(ledger_path), operation_id, Decimal("0.02"))
    except BudgetError:
        results.put((operation_id, "budget_error"))
    except Exception as error:  # pragma: no cover - asserted by the parent process.
        results.put((operation_id, f"unexpected:{type(error).__name__}"))
    else:
        results.put((operation_id, "success"))


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

    def test_concurrent_reservations_preserve_hard_limit_and_clean_transaction_files(self):
        with tempfile.TemporaryDirectory() as directory:
            ledger = Path(directory) / "budget.json"
            ledger.write_text(
                json.dumps(
                    {
                        "hard_limit_usd": "20.00",
                        "reservations": [{"operation_id": "seed", "estimate_usd": "19.90"}],
                    }
                ),
                encoding="utf-8",
            )
            context = multiprocessing.get_context("spawn")
            start = context.Event()
            results = context.Queue()
            operation_ids = [f"concurrent-{index}" for index in range(10)]
            workers = [
                context.Process(
                    target=reserve_in_process,
                    args=(str(ledger), operation_id, start, results),
                )
                for operation_id in operation_ids
            ]
            for worker in workers:
                worker.start()
            start.set()
            for worker in workers:
                worker.join(timeout=15)
                self.assertFalse(worker.is_alive(), "worker did not finish")
                self.assertEqual(worker.exitcode, 0)

            outcomes = [results.get(timeout=5) for _ in workers]
            self.assertEqual(sorted(operation_id for operation_id, _ in outcomes), operation_ids)
            self.assertEqual(sum(outcome == "success" for _, outcome in outcomes), 5)
            self.assertEqual(sum(outcome == "budget_error" for _, outcome in outcomes), 5)
            self.assertFalse(any(outcome.startswith("unexpected:") for _, outcome in outcomes))

            stored = json.loads(ledger.read_text(encoding="utf-8"))
            self.assertEqual(
                sum(Decimal(item["estimate_usd"]) for item in stored["reservations"]),
                Decimal("20.00"),
            )
            self.assertEqual(len({item["operation_id"] for item in stored["reservations"]}), 6)
            self.assertFalse(ledger.with_name("budget.json.tmp").exists())
            self.assertFalse(ledger.with_name("budget.json.lock").exists())


if __name__ == "__main__":
    unittest.main()
