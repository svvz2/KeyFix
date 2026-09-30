$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location -LiteralPath $projectRoot

dotnet restore KeyFix.slnx
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet test KeyFix.slnx --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet publish src\KeyFix.App\KeyFix.App.csproj --configuration Release -p:PublishProfile=WinX64
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$outputDirectory = Join-Path $projectRoot "artifacts\KeyFix-win-x64"
Get-ChildItem -LiteralPath $outputDirectory -Filter "*.pdb" -File | Remove-Item -Force

$executablePath = Join-Path $outputDirectory "KeyFix.App.exe"
if (-not (Test-Path -LiteralPath $executablePath)) {
    throw "Release executable was not created."
}

Write-Host ""
Write-Host "KeyFix is ready:" -ForegroundColor Green
Write-Host $executablePath
