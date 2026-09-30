$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location -LiteralPath $projectRoot

& (Join-Path $projectRoot "build-release.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot "src\KeyFix.App\KeyFix.App.csproj")
$version = [string]$project.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not read the application version."
}

$artifactsDirectory = Join-Path $projectRoot "artifacts"
$publishedExecutable = Join-Path $artifactsDirectory "KeyFix-win-x64\KeyFix.App.exe"
$userArchive = Join-Path $artifactsDirectory "KeyFix-$version-win-x64.zip"
$sourceArchive = Join-Path $artifactsDirectory "KeyFix-$version-source.zip"
$checksumFile = Join-Path $artifactsDirectory "SHA256SUMS.txt"
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("KeyFix-packages-" + [Guid]::NewGuid().ToString("N"))
$userStage = Join-Path $temporaryRoot "KeyFix-$version-win-x64"
$sourceStage = Join-Path $temporaryRoot "KeyFix-$version-source"

try {
    New-Item -ItemType Directory -Force -Path $userStage, $sourceStage | Out-Null

    Copy-Item -LiteralPath $publishedExecutable -Destination (Join-Path $userStage "KeyFix.exe")
    Copy-Item -LiteralPath (Join-Path $projectRoot "distribution\README-AR.txt") -Destination $userStage
    Copy-Item -LiteralPath (Join-Path $projectRoot "docs\PRIVACY.md") -Destination $userStage
    Copy-Item -LiteralPath (Join-Path $projectRoot "CHANGELOG.md") -Destination $userStage

    $sourceDirectories = @("src", "tests", "tools", ".github", "docs", "distribution")
    foreach ($directory in $sourceDirectories) {
        $source = Join-Path $projectRoot $directory
        $destination = Join-Path $sourceStage $directory
        New-Item -ItemType Directory -Force -Path $destination | Out-Null
        & robocopy $source $destination /E /XD bin obj /XF *.user *.suo /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) {
            throw "Copying $directory failed with robocopy exit code $LASTEXITCODE."
        }
    }

    Get-ChildItem -LiteralPath $projectRoot -File -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $sourceStage
    }

    Compress-Archive -Path (Join-Path $userStage "*") -DestinationPath $userArchive -CompressionLevel Optimal -Force
    Compress-Archive -Path (Join-Path $sourceStage "*") -DestinationPath $sourceArchive -CompressionLevel Optimal -Force

    $hashes = @($userArchive, $sourceArchive) | ForEach-Object {
        $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
        "{0}  {1}" -f $hash.Hash.ToLowerInvariant(), (Split-Path -Leaf $_)
    }
    Set-Content -LiteralPath $checksumFile -Value $hashes -Encoding UTF8
}
finally {
    $resolvedTemporaryRoot = [IO.Path]::GetFullPath($temporaryRoot)
    $systemTemporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ($resolvedTemporaryRoot.StartsWith($systemTemporaryRoot, [StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedTemporaryRoot)) {
        Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force
    }
}

Write-Host ""
Write-Host "Packages are ready:" -ForegroundColor Green
Write-Host $userArchive
Write-Host $sourceArchive
Write-Host $checksumFile
