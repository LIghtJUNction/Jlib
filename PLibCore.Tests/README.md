# Mod assembly ownership host tests

Run with a .NET 8 SDK or newer:

```sh
dotnet run --project PLibCore.Tests/PLibCore.Tests.csproj -c Release
```

The executable links the production ownership helper with minimal game API data
doubles. It checks registration, owner lookup, selected content roots, repeated
registration, conflicts, null arguments, native DLL lists, and late registration.
It does not load game assemblies or verify Unity, Mono, or the options UI.
