# StangaNetLib.EventBus — Copilot instructions

- Pub/sub event bus: in-memory + RabbitMQ backends; job-trigger integration with `StangaNetLib.Jobs`.
- Prefer `Result` / `Result<T>` and `Guard` from `StangaNetLib.Core` — do not reintroduce local Result/Guard types.
- Optional keyed locks: `IKeyedLock` from `StangaNetLib.Concurrency.Locking` (never `Concurrency.Abstractions`).
- Keep public APIs documented (`///`) and options-driven via `EventBusSettings` / `RabbitMqSettings`.
- DI: `AddStangaNetLibEventBus` / `AddStangaNetLibEventBusWithRabbitMq` / `AddEventTriggeredJob`.
- Pin Core/Concurrency/Jobs 1.0.0; never reintroduce Microsoft.Extensions.* 10.x.
- Match existing naming, file-scoped namespaces, and EditorConfig / Roslyn conventions.
- Tests use xUnit v3 + FluentAssertions; cover both `net8.0` and `net9.0` when changing shared behavior.
- Do not add broker-specific tests that require a live RabbitMQ unless the CI matrix provides one.
