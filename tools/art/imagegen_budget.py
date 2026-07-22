"""Reserve image-generation budget before an operation is submitted."""

import argparse
import json
import os
import time
from contextlib import contextmanager
from decimal import Decimal, InvalidOperation
from pathlib import Path


class BudgetError(ValueError):
    """Raised when a budget reservation is invalid or exceeds its cap."""


_LOCK_TIMEOUT_SECONDS = 10.0
_LOCK_RETRY_SECONDS = 0.02


def _money(value: object) -> Decimal:
    try:
        amount = Decimal(str(value))
        quantized = amount.quantize(Decimal("0.01"))
    except (InvalidOperation, ValueError) as error:
        raise BudgetError("amount must be a decimal value") from error
    if not amount.is_finite():
        raise BudgetError("amount must be a decimal value")
    if amount != quantized:
        raise BudgetError("amount must have at most two fractional digits")
    return quantized


def _money_text(value: Decimal) -> str:
    return format(value.quantize(Decimal("0.01")), ".2f")


def _read_ledger(ledger_path: Path) -> dict:
    return json.loads(ledger_path.read_text(encoding="utf-8"), parse_float=Decimal)


def _release_lock(lock_path: Path, deadline: float) -> None:
    while True:
        try:
            os.rmdir(lock_path)
        except PermissionError as error:
            if time.monotonic() >= deadline:
                raise BudgetError("timed out releasing budget lock") from error
            time.sleep(_LOCK_RETRY_SECONDS)
        else:
            return


@contextmanager
def _transaction_lock(ledger_path: Path):
    """Serialize the read-check-write budget transaction across processes."""
    lock_path = ledger_path.with_name(f"{ledger_path.name}.lock")
    deadline = time.monotonic() + _LOCK_TIMEOUT_SECONDS
    while True:
        try:
            os.mkdir(lock_path)
        except (FileExistsError, PermissionError):
            if time.monotonic() >= deadline:
                raise BudgetError("timed out waiting for budget lock")
            time.sleep(_LOCK_RETRY_SECONDS)
        else:
            break

    transaction_error = None
    try:
        yield
    except BaseException as error:
        transaction_error = error
        raise
    finally:
        try:
            _release_lock(lock_path, deadline)
        except BudgetError as error:
            if transaction_error is None:
                raise
            transaction_error.add_note(f"budget lock cleanup failed: {error}")


def reserve_budget(ledger_path: Path, operation_id: str, estimate_usd: Decimal) -> dict:
    """Atomically add one operation reservation to a JSON budget ledger."""
    if not operation_id:
        raise BudgetError("operation_id is required")

    estimate = _money(estimate_usd)
    if estimate <= 0:
        raise BudgetError("estimate_usd must be positive")

    with _transaction_lock(ledger_path):
        ledger = _read_ledger(ledger_path)
        reservations = ledger.setdefault("reservations", [])
        if any(reservation.get("operation_id") == operation_id for reservation in reservations):
            raise BudgetError("duplicate operation_id")

        hard_limit = _money(ledger["hard_limit_usd"])
        reserved = sum((_money(item["estimate_usd"]) for item in reservations), Decimal("0.00"))
        if reserved + estimate > hard_limit:
            raise BudgetError("hard limit exceeded")

        ledger["hard_limit_usd"] = _money_text(hard_limit)
        reservations.append({"operation_id": operation_id, "estimate_usd": _money_text(estimate)})
        temporary_path = ledger_path.with_name(f"{ledger_path.name}.tmp")
        temporary_path.write_text(json.dumps(ledger, indent=2) + "\n", encoding="utf-8")
        os.replace(temporary_path, ledger_path)
    return ledger


def budget_status(ledger_path: Path) -> dict[str, str]:
    """Return display-safe aggregate amounts for a budget ledger."""
    ledger = _read_ledger(ledger_path)
    limit = _money(ledger["hard_limit_usd"])
    reserved = sum(
        (_money(item["estimate_usd"]) for item in ledger.get("reservations", [])),
        Decimal("0.00"),
    )
    return {
        "reserved_usd": _money_text(reserved),
        "remaining_usd": _money_text(limit - reserved),
        "hard_limit_usd": _money_text(limit),
    }


def _default_ledger() -> Path:
    return Path(__file__).resolve().parents[2] / "SourceArt" / "Generated" / "budget.json"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    subcommands = parser.add_subparsers(dest="command", required=True)
    reserve = subcommands.add_parser("reserve")
    reserve.add_argument("--operation-id", required=True)
    reserve.add_argument("--estimate-usd", required=True)
    subcommands.add_parser("status")
    arguments = parser.parse_args()
    ledger = _default_ledger()

    if arguments.command == "reserve":
        reserve_budget(ledger, arguments.operation_id, Decimal(arguments.estimate_usd))
    print(json.dumps(budget_status(ledger), sort_keys=True))


if __name__ == "__main__":
    main()
