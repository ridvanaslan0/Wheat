# Wheat

A tiny retro (Win9x-style) desktop mascot that chats with an AI model and can operate your PC.
A floppy-disk character lives on your desktop: click it, type in its speech bubble, and it answers
and does things for you. No chat window needed.

> **Heads-up:** Wheat runs with administrator rights and, by default, does not ask before
> each action. Read the [Security](#security) section before using it.

## Features

- **Type right in the speech bubble.** Click the mascot, type, press Enter. The reply appears in the bubble.
  The classic chat window is still there if you want it (right-click > Open chat window).
- **Many AI providers:** Claude, ChatGPT, Gemini, DeepSeek, Kimi, Groq, or any OpenAI-compatible server
  (OpenRouter, Ollama, LM Studio...).
- **Can operate your PC:** open URLs and programs, list folders, read and write text files,
  run `cmd` commands and PowerShell scripts.
- **Runs as administrator** automatically (one UAC prompt at launch).
- **Start with Windows** option, no UAC prompt at login (uses an elevated scheduled task).
- **One permission:** you accept once at first launch, then the assistant never asks again before acting
  (optional per-action confirmations in Settings).
- **Single exe:** one self-contained `Wheat.exe`, no .NET install required.
- API keys are stored per provider, encrypted with Windows DPAPI.

## Download

Get `Wheat.exe` from the [Releases](../../releases) page, run it, and accept the UAC prompt.

Windows SmartScreen may warn about an unsigned app. Click **More info > Run anyway**
if you trust the source (or build it yourself, see below).

## Quick start

1. Run `Wheat.exe` and accept the one-time permission dialog.
2. Right-click the mascot (or the tray icon) > **Settings...**, pick a provider and paste your API key.
3. Click the mascot, type a message, press **Enter**.

Examples: "open YouTube", "what's in my Downloads folder?", "create notes.txt on my desktop with a shopping list",
"how much free space do I have on C:?"

## Usage

| Action | How |
|---|---|
| Open the bubble and type | Left-click the mascot (click again to close) |
| Send | Enter |
| Stop a running request | Esc while it is thinking |
| Close the bubble | Esc |
| Move the mascot | Drag it |
| Menu | Right-click the mascot or the tray icon |
| Chat window | Right-click > Open chat window |
| Open the bubble from the tray | Double-click the tray icon |

Menu items: Type to assistant, Open chat window, Settings, Start with Windows, Hide/Show mascot, Exit.

## Settings

| Option | Description |
|---|---|
| Provider, API key, Model, URL | Per-provider; switching providers resets the conversation |
| Thinking | Default / Low / Off. Speeds up reasoning models (OpenAI-compatible providers only) |
| Start with Windows | Creates or removes an elevated logon task named `Wheat` |
| Ask for confirmation before every action | Off by default. Turn on to approve each tool call |

### Providers

| Provider | Get a key | Default model |
|---|---|---|
| Claude (Anthropic) | console.anthropic.com | claude-sonnet-5-5 |
| ChatGPT (OpenAI) | platform.openai.com/api-keys | gpt-6-luna |
| Gemini (Google) | aistudio.google.com/apikey | gemini-3.8-flash |
| DeepSeek | platform.deepseek.com | deepseek-flash |
| Kimi (Moonshot) | platform.kimi.ai | kimi-k3.1-pro |
| Groq (fast, free tier) | console.groq.com/keys | qwen/qwen3.8-27b |
| Custom (OpenAI-compatible / local) | OpenRouter, Ollama, LM Studio... | llama3.1 |

- Model names and URLs can be changed in Settings. If a model name is rejected, check the provider's
  current model list.
- For **Custom**, use either `.../v1` or the full `.../v1/chat/completions` URL. Local servers
  (Ollama, LM Studio) need no key.
- Using the Chinese Kimi platform? Change the URL to `https://api.moonshot.cn/v1`.
- Tool use (function calling) depends on the model. Models without it can chat but cannot operate the PC.

## Tools

| Tool | What it does |
|---|---|
| `open_url` | Opens an http/https address in the default browser |
| `open_program` | Opens a program or file |
| `list_directory` | Lists a folder (up to 200 entries) |
| `read_text_file` | Reads a text file (first 20000 characters; the content is sent to your AI provider) |
| `write_text_file` | Creates or overwrites a text file |
| `run_command` | Runs a `cmd.exe` command (60 s timeout) |
| `run_powershell` | Runs a PowerShell script (60 s timeout) |

To add a tool, add its definition to `Tools.Definitions()` and a `case` to `Tools.RunAsync()`.
Definitions use the Anthropic format; `AiClient` converts them for OpenAI-compatible providers.

## Build from source

Requirements: Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download).

Single self-contained exe:

```
build.bat
```

or

```
dotnet publish Wheat.csproj -c Release -o publish
```

Output: `publish\Wheat.exe`.

To run during development, use an **elevated** terminal (the app requires administrator rights):

```
dotnet run
```

### GitHub Actions

`.github/workflows/build.yml` builds the exe on every push and uploads it as an artifact.
Push a tag like `v1.0.0` and the exe is attached to a GitHub Release automatically:

```
git tag v1.0.0
git push origin v1.0.0
```

## Security

- The app runs **elevated** and the model can run commands and write files **without asking** (after your
  one-time permission). A wrong or malicious instruction can damage your system.
- Only use providers you trust. Your messages and any file the assistant reads are sent to that provider.
- Files, web pages and command output can contain hidden instructions (prompt injection). Be careful when
  asking the assistant to process content you do not trust.
- Prefer turning on **Ask for confirmation before every action** on shared or important machines.
- See [SECURITY.md](SECURITY.md) for details and how to report vulnerabilities.

## Troubleshooting

- **"Already running" message:** the mascot is on your desktop or in the system tray.
- **Autostart stopped working after moving the exe:** open Settings, untick and tick *Start with Windows* again.
- **Settings or keys seem lost when using a separate admin account:** settings live in the *elevated user's*
  `%APPDATA%\Wheat`, so enter them again under that account.
- **Model errors after changing the Thinking setting:** some models reject it; switch back to *Default*.

## License

[MIT](LICENSE)
