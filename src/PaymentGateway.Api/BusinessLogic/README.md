# BusinessLogic layer

The rules of the gateway.

- `Services/PaymentService.cs` – the payment flow: validate, call the bank, store; ownership on read. **Start here.**
- `Validation/` – the brief's validation rules.
- `Mapping/` – hand-written mappings between DTOs, in-flight models and entities.
- `Interfaces/` – what the endpoints and services depend on.
- `Models/` – in-flight types, named `…Model`.

May depend on: `ApiInterface` (the DTOs it accepts and returns), `DataInterface` and `Shared`. Never on `DataLayer` or `Api`.
