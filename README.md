# ChatApp

LAN chat application with a WPF client, TCP server, SQLite persistence, authentication, message history, presence, attachments, encrypted transport, and edit/delete support.

## Build

```powershell
.\build.ps1
```

## Run for development

```powershell
.\run.ps1
```

## Create downloadable executables

```powershell
.\publish.ps1
```

The published files are created under:

- `dist\server\ChatServer.exe`
- `dist\client\ChatClient.Wpf.exe`

Run `ChatServer.exe` on one computer on the network. It listens on all network interfaces on port `13000` by default and prints the IP addresses that clients can use. If Windows Firewall asks for permission, allow private network access.

Copy `ChatClient.Wpf.exe` to each Windows device. In the login window, click `Find Server` or enter the server IP address shown by `ChatServer.exe` and port `13000`.

To use another port:

```powershell
.\ChatServer.exe 14000
```
