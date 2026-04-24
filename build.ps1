$ErrorActionPreference = 'Stop'

$projects = @(
    'ChatCore\ChatCore.csproj',
    'ChatServer\ChatServer.csproj',
    'ChatClient.Wpf\ChatClient.Wpf.csproj'
)

foreach ($project in $projects) {
    dotnet build $project
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
