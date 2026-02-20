[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string[]]$RuntimeIdentifiers = @("win-x64"),

    [string]$Solution = "bTranslator.slnx",
    [string]$AppProject = "src/bTranslator.App/bTranslator.App.csproj",
    [string]$PackageDirectory = "Package",

    [switch]$SkipTests,
    [switch]$NoClean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Write-Host "dotnet $($Arguments -join ' ')" -ForegroundColor Cyan
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: dotnet $($Arguments -join ' ')"
    }
}

try {
    $repoRoot = Split-Path -Parent $PSCommandPath
    Set-Location $repoRoot

    if (-not (Test-Path $Solution)) {
        throw "Solution file not found: $Solution"
    }

    if (-not (Test-Path $AppProject)) {
        throw "App project file not found: $AppProject"
    }

    $resolvedPackageDirectory = Join-Path $repoRoot $PackageDirectory

    Write-Host "==> Restore" -ForegroundColor Yellow
    Invoke-DotNet -Arguments @("restore", $Solution)

    Write-Host "==> Build ($Configuration)" -ForegroundColor Yellow
    Invoke-DotNet -Arguments @("build", $Solution, "-c", $Configuration, "--no-restore")

    if (-not $SkipTests) {
        Write-Host "==> Test ($Configuration)" -ForegroundColor Yellow
        Invoke-DotNet -Arguments @("test", $Solution, "-c", $Configuration, "--no-build")
    }
    else {
        Write-Host "==> Test skipped" -ForegroundColor DarkYellow
    }

    if ((Test-Path $resolvedPackageDirectory) -and (-not $NoClean)) {
        Write-Host "==> Clean package directory: $resolvedPackageDirectory" -ForegroundColor Yellow
        Remove-Item -Path $resolvedPackageDirectory -Recurse -Force
    }

    New-Item -Path $resolvedPackageDirectory -ItemType Directory -Force | Out-Null

    foreach ($rid in $RuntimeIdentifiers) {
        if ([string]::IsNullOrWhiteSpace($rid)) {
            continue
        }

        $platform = switch ($rid.ToLowerInvariant()) {
            "win-x64" { "x64" }
            "win-x86" { "x86" }
            "win-arm64" { "ARM64" }
            default { "AnyCPU" }
        }

        $ridOutput = Join-Path $resolvedPackageDirectory $rid
        New-Item -Path $ridOutput -ItemType Directory -Force | Out-Null

        Write-Host "==> Publish $rid" -ForegroundColor Yellow
        $publishArgs = @(
            "publish", $AppProject,
            "-c", $Configuration,
            "-r", $rid,
            "--self-contained", "true",
            "--output", $ridOutput,
            "/p:WindowsPackageType=None",
            "/p:WindowsAppSDKSelfContained=true",
            "/p:PublishProfile=",
            "/p:Platform=$platform",
            "/p:PublishTrimmed=false"
        )

        if ($Configuration -eq "Release") {
            $publishArgs += "/p:PublishReadyToRun=true"
        }
        else {
            $publishArgs += "/p:PublishReadyToRun=false"
        }

        Invoke-DotNet -Arguments $publishArgs

        $appExe = Join-Path $ridOutput "bTranslator.App.exe"
        if (Test-Path $appExe) {
            Write-Host "Published executable: $appExe" -ForegroundColor Green
        }
        else {
            Write-Warning "Publish completed but executable not found at: $appExe"
        }
    }

    $readmePath = Join-Path $resolvedPackageDirectory "README.md"
    @(
        "bTranslator packaged output"
        "Build time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
        "Configuration: $Configuration"
        "RuntimeIdentifiers: $($RuntimeIdentifiers -join ', ')"
        ""
        "Run:"
        "  .\<rid>\bTranslator.App.exe"
        ""
        "Example:"
        "  .\win-x64\bTranslator.App.exe"
    ) | Set-Content -Path $readmePath -Encoding UTF8

    Write-Host "==> Done" -ForegroundColor Green
    Write-Host "Package output: $resolvedPackageDirectory" -ForegroundColor Green
}
catch {
    Write-Error $_
    exit 1
}

