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

## Build outputs

### Runnable EXE (unpackaged)

This produces a self-contained EXE, but the target machine still needs the Windows App Runtime.

```powershell
# x64 example

dotnet publish "C:\Users\Sartaj Singh\RiderProjects\greenhousecontrol\Greenhouse Control.csproj" -c Release -r win-x64 --self-contained true
```

Output goes to `bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\`.

## Runtime requirements

- Windows 10 version 19041 or higher
- .NET 8.0 Runtime
- Windows App Runtime (for EXE output)
