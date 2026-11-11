# Security

## What this app can do

Wheat gives an AI model tools to run commands, read and write files, and open programs on your PC.
The app runs **with administrator rights** and, by default, **does not ask before each action**.
Treat it like any other powerful automation tool.

- Only use AI providers you trust.
- Be careful when asking it to read files, web pages or command output from untrusted sources:
  such content can contain text that tries to trick the model into running harmful commands (prompt injection).
- Turn on **Settings > Ask for confirmation before every action** if you want to review each step.

## Data handling

- API keys are stored per provider, encrypted with Windows DPAPI (current user), in
  `%APPDATA%\Wheat`.
- Your messages, and the content of any file the assistant reads, are sent to the AI provider you selected.
- The app has no telemetry and talks only to the provider URL you configure.

## Reporting a vulnerability

Please open a private security advisory on the GitHub repository (Security tab > Report a vulnerability)
instead of a public issue.
