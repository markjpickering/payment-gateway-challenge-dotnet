# Shared

The tiny vocabulary every layer uses: `Outcomes/` (`Result<T>`, `Error`, `ErrorKind`, `FieldError`), so expected failures are
returned as values instead of thrown.

Depends on nothing of ours, which is why every layer may use it.
