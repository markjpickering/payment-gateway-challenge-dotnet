# Api layer

The HTTP surface.

- `Endpoints/` – the two Minimal API endpoints (thin: bind, call a service, map the result to HTTP). **Start here.**
- The request and response DTOs the endpoints use are in `ApiInterface/Dtos/`, the API's contract.
- `Security/` – token scopes and who the caller is. `ErrorHandling/` – the global error answers. `Documentation/` – OpenAPI and Scalar. `Options/` – request limits.

May depend on: `BusinessLogic`, `ApiInterface`, `Shared` and the enums in `DataInterface`. Never on `DataLayer`.
