$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishRoot = Join-Path $root 'dist'
$clientOutput = Join-Path $publishRoot 'client'
$serverOutput = Join-Path $publishRoot 'server'

function Invoke-Dotnet {
    param([string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Invoke-Dotnet @(
    'publish', (Join-Path $root 'ChatClient.Wpf\ChatClient.Wpf.csproj'),
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-o', $clientOutput
)

Invoke-Dotnet @(
    'publish', (Join-Path $root 'ChatServer\ChatServer.csproj'),
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-o', $serverOutput
)

Get-ChildItem -Path $publishRoot -Recurse -Filter '*.pdb' | Remove-Item -Force

Write-Host ''
Write-Host 'Standalone executables created:'
Write-Host "  Client: $clientOutput\ChatClient.Wpf.exe"
Write-Host "  Server: $serverOutput\ChatServer.exe"
Write-Host ''
Write-Host 'Run ChatServer.exe on one computer. Allow it through Windows Firewall.'
Write-Host 'Run ChatClient.Wpf.exe on each device and enter the server IP shown by ChatServer.exe.'
