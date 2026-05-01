# Greenhouse Control

This project contains a WinUI desktop app and a Python TCP server for a smart greenhouse demo.

## Python server

The server listens on port 12345, logs incoming commands, updates `server/server.config`, and sends the current settings to any connected client.

```powershell
python -u server\main.py
```

## WinUI app

Open the solution in Rider or Visual Studio and run the WinUI project. The UI will stay disabled until the app connects to the server.

## Command protocol

The server expects one command per line and replies with a JSON settings payload:

- `LIGHT ON` / `LIGHT OFF`
- `AUTOWATER ON` / `AUTOWATER OFF`
- `FREQUENCY <1-48>`
- `DISPENSE <1-60>`
- `SHUTDOWN`
- `RESTART`

The server responds with:

- `SETTINGS {json}`

The JSON matches the fields in `server/server.config`.

