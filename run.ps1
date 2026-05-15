$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildScript = Join-Path $root 'build.ps1'
$serverProject = Join-Path $root 'ChatServer\ChatServer.csproj'
$clientProject = Join-Path $root 'ChatClient.Wpf\ChatClient.Wpf.csproj'

try {
    $ruleExists = netsh advfirewall firewall show rule name="ChatApp Server (Port 13000)" 2>$null | Out-String
    if ($ruleExists -notmatch "Rule Name:") {
        Write-Host "Attempting to add Windows Firewall rule for ChatServer (may prompt for Admin privileges)..." -ForegroundColor Yellow
        Start-Process powershell -Verb RunAs -ArgumentList "-Command `"netsh advfirewall firewall add rule name='ChatApp Server (Port 13000)' dir=in action=allow protocol=TCP localport=13000; netsh advfirewall firewall add rule name='ChatApp Server (UDP 13000)' dir=in action=allow protocol=UDP localport=13000`"" -Wait
    }
} catch {
    Write-Host "Could not check or configure Windows Firewall. If clients cannot connect, please add port 13000 manually." -ForegroundColor Red
}

& $buildScript

$serverCommand = "dotnet run --project `"$serverProject`""
Start-Process powershell -ArgumentList @(
    '-NoExit',
    '-ExecutionPolicy', 'Bypass',
    '-Command', $serverCommand
) -WorkingDirectory $root

Start-Sleep -Seconds 1

dotnet run --project $clientProject
