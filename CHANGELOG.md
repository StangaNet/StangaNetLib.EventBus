# Changelog

All notable changes to **StangaNetLib.EventBus** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]


## [1.0.1] - 2026-10-23

### Changed
- Add `net10.0` target framework alongside `net8.0` and `net9.0`.
- Require .NET SDK 10 (global.json) to multi-target `net8.0` / `net9.0` / `net10.0`; opt in Microsoft.Testing.Platform as the `dotnet test` runner.

## [1.0.0] - 2026-10-18

### Added
- Pub/sub integration event bus: `IEventBus`, `IEventHandler<TEvent>`, `IEventBusSubscriptionManager`.
- In-memory backend with optional `IKeyedLock` serialization per event type.
- RabbitMQ backend (`RabbitMqEventBus`) via RabbitMQ.Client 7.x async API + host lifecycle.
- Job integration: `AddEventTriggeredJob` bridging events to `StangaNetLib.Jobs`.
- `EventBusSettings` / `RabbitMqSettings` with options validation; `EventBusErrors`.
- Dual targeting `net8.0` / `net9.0`, XML docs, symbols (`snupkg`), package icon, and MSBuild consumer metadata stamp.
- Depends on `StangaNetLib.Core` 1.0.0, `StangaNetLib.Concurrency` 1.0.0, `StangaNetLib.Jobs` 1.0.0; Microsoft.Extensions.* pinned to 8.x.
- CI via shared StangaNet reusable workflow (push / PR / manual; publish outside PRs).

[Unreleased]: https://github.com/StangaNet/StangaNetLib.EventBus/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/StangaNet/StangaNetLib.EventBus/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/StangaNet/StangaNetLib.EventBus/releases/tag/v1.0.0
