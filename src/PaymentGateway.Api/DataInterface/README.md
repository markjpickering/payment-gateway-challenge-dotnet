# DataInterface layer

Everything the business logic needs from outside itself, as interfaces, plus the types in their signatures.

- `IPaymentsRepository`, `IBankClient` – what the brief needs. `IPaymentMetrics` – the metrics the bank client records (a no-op on this branch).
- `Entities/` – what is stored, named `…Entity`. `Enums/` – shared enums. `Dtos/Requests`, `Dtos/Responses` – the bank's request and response messages (DTOs, marked by the suffix).
- `Telemetry/` – the small enum `IPaymentMetrics` uses.

May depend on: `Shared` only. It is the lowest layer.
