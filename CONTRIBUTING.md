# Contributing

Thanks for your interest! Bug reports and pull requests are welcome.

## Build

Requirements: Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```
dotnet build
```

The app asks for administrator rights, so run `dotnet run` from an **elevated** terminal
(or start the built exe and accept the UAC prompt).

## Guidelines

- Keep everything in English (UI text, code comments, documentation).
- Keep the retro look: do not call `Application.EnableVisualStyles()`.
- To add a tool, add its definition to `Tools.Definitions()` and a `case` to `Tools.RunAsync()`.
- Keep pull requests small and focused.
