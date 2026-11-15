# Tests

Run with `dotnet test` (no Docker needed: the bank is substituted).

- `Unit/` – mirrors the source folders (`Unit/BusinessLogic/Services/…`), one test class per class under test.
  Where a class needs more than ten tests, a folder `[Class]Tests/` holds one `[Area]TestClass` per area.
  `Unit/Architecture/` holds the tests that keep the layers, the type names and the folder READMEs consistent (skippable).
- `Integration/` – by feature, through the real app with only the bank substituted. `Integration/Contract/` pins the
  brief's exact JSON. `Integration/Simulator/` needs the real bank simulator (`docker-compose up -d`) and skips itself otherwise.
- `TestSupport/` – shared helpers: the web app factory, test tokens, builders.

Every test marks `// Arrange`, `// Act` and `// Assert`.
