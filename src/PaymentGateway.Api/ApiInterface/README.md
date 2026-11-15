# ApiInterface layer

The API's contract: the request and response DTOs the endpoints accept and return, in `Dtos/Requests` and `Dtos/Responses`
(named `…Request` / `…Response`). It is the counterpart of `DataInterface`, which holds the data side's contract.
Records only, no behaviour; the exact JSON they produce is pinned by `Integration/Contract/ApiContractTests`. **Start here** to see what a merchant sends and receives.

May depend on: `Shared` and the enums in `DataInterface`. Never on `Api`, `BusinessLogic` or `DataLayer`.
