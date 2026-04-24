$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildScript = Join-Path $root 'build.ps1'
$serverProject = Join-Path $root 'ChatServer\ChatServer.csproj'
$clientProject = Join-Path $root 'ChatClient.Wpf\ChatClient.Wpf.csproj'

& $buildScript

$serverCommand = "dotnet run --project `"$serverProject`""
Start-Process powershell -ArgumentList @(
    '-NoExit',
    '-ExecutionPolicy', 'Bypass',
    '-Command', $serverCommand
) -WorkingDirectory $root

Start-Sleep -Seconds 1

dotnet run --project $clientProject
