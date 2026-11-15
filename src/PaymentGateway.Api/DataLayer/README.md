# DataLayer

The implementations of the `DataInterface` interfaces.

- `Repositories/PaymentsRepository.cs` – the in-memory, thread-safe store. **Start here.**
- `Bank/BankClient.cs` – the call to the bank simulator and how each failure is handled, with `BankOptions`.
- `Placeholders/NoOpPaymentMetrics.cs` – a do-nothing stand-in for metrics (skippable), marked `PLACEHOLDER`.

May depend on: `DataInterface` and `Shared`. It holds no business rules.
