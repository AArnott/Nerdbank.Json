# Copilot instructions for this repository

## High level guidance

- Review `CONTRIBUTING.md` before building or testing.
- Run `.github/Prime-ForCopilot.ps1` once before any `dotnet` or `msbuild` command.
  Run it again if a build reports missing git objects or problems caused by a shallow clone.

## Software design

- Design APIs to be highly testable, and thoroughly test all functionality.
- Practice TDD for new serializer functionality whenever practical: add focused failing tests first, or at minimum include focused tests in the same change that introduces the behavior.
- Treat performance as a first-class engineering principle. Prefer allocation-conscious low-level reader and writer paths, and justify complex optimizations with targeted measurements.
- Keep low-level serialization APIs streaming-friendly and independent from other serializer engines.
- Prefer immutable, instance-scoped configuration. Do not introduce mutable global serializer defaults.
- All shipping libraries and default code paths must be trimming-safe and NativeAOT-ready.
- Any useful feature that cannot be NativeAOT-safe must be disabled by default and activated only by an explicit method call. Not calling that method must leave the application NativeAOT-safe.
- Do not use `InternalsVisibleTo`. Test through public APIs or move reusable test support into an appropriate public test-support package.
- Optimize public APIs for clarity, performance, maintainability, and long-term compatibility.
- Avoid binary breaking changes in public APIs of projects under `src` unless their project files set `IsPackable` to `false`.
- New features require thorough tests, docfx documentation, and samples where a runnable example improves understanding.

## Testing

This repository uses xunit v3 with Microsoft.Testing.Platform (MTP v2). Traditional VSTest `--filter` expressions do not work.

- Build with `dotnet build -c Release`.
- Run all tests with `dotnet test --no-build -c Release`.
- Run one project with `dotnet test --project test/Library.Tests/Library.Tests.csproj --no-build -c Release`.
- Put runner options after `--`, for example `--filter-method`, `--filter-class`, `--filter-namespace`, `--filter-trait`, or `--filter-not-trait`.
- Skip unstable tests with `-- --filter-not-trait "FailsInCloudTest=true"` when applicable.

## Documentation

- Put API and conceptual documentation under `docfx/`.
- Put runnable examples under `samples/` and link to them from docfx when they improve understanding.
- Build documentation with `dotnet docfx docfx/docfx.json --warningsAsErrors --disableGitFeatures`.

## Coding style

- Honor StyleCop rules and fix build warnings after tests pass.
- Use namespace statements instead of namespace blocks in new C# files.
- Add API documentation comments to all new public and internal members.

