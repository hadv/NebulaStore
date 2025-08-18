# NebulaStore Test Coverage Generator
param([bool]$OpenReport = $true)

Write-Host "NebulaStore Test Coverage Generator" -ForegroundColor Blue
Write-Host "===================================" -ForegroundColor Blue

# Refresh PATH to ensure dotnet is available
$env:PATH = [System.Environment]::GetEnvironmentVariable("PATH","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH","User")

# Clean up old coverage reports and test results
Write-Host "Cleaning up old coverage reports and test results..." -ForegroundColor Yellow

# Remove coverage report directory
if (Test-Path "coverage-report") {
    Remove-Item "coverage-report" -Recurse -Force
    Write-Host "   Removed old coverage-report directory" -ForegroundColor Green
}

# Remove all TestResults directories recursively
Get-ChildItem -Path . -Recurse -Directory -Name "TestResults" -ErrorAction SilentlyContinue | ForEach-Object {
    $fullPath = Get-ChildItem -Path . -Recurse -Directory | Where-Object { $_.Name -eq "TestResults" }
    $fullPath | ForEach-Object {
        Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "   Removed TestResults: $($_.FullName)" -ForegroundColor Green
    }
}

# Remove any standalone coverage files
Get-ChildItem -Path . -Recurse -Filter "*.cobertura.xml" -ErrorAction SilentlyContinue | ForEach-Object {
    Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
    Write-Host "   Removed old coverage file: $($_.Name)" -ForegroundColor Green
}

# Clean build outputs to ensure fresh compilation
Write-Host "   Cleaning build outputs for fresh compilation..." -ForegroundColor Yellow
dotnet clean --verbosity quiet
Write-Host "   Build outputs cleaned" -ForegroundColor Green

# Build solution to ensure latest code
Write-Host "Building solution to ensure latest code..." -ForegroundColor Yellow
dotnet build --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed! Cannot proceed with testing." -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "   Build completed successfully" -ForegroundColor Green

# Run tests with coverage collection
Write-Host "Running tests with coverage collection..." -ForegroundColor Yellow
dotnet test --collect:"XPlat Code Coverage" --verbosity minimal --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Host "Tests failed! Coverage report generation aborted." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "   Tests completed successfully" -ForegroundColor Green

# Find the coverage file
Write-Host "Locating coverage files..." -ForegroundColor Yellow
$coverageFiles = Get-ChildItem -Path . -Recurse -Filter "coverage.cobertura.xml"
if ($coverageFiles.Count -eq 0) {
    Write-Host "No coverage files found!" -ForegroundColor Red
    exit 1
}

$coverageFile = $coverageFiles[0].FullName
Write-Host "   Found coverage file: $($coverageFiles[0].Name)" -ForegroundColor Green

# Install reportgenerator if needed
Write-Host "Checking reportgenerator tool..." -ForegroundColor Yellow
$command = Get-Command reportgenerator -ErrorAction SilentlyContinue
if (-not $command) {
    Write-Host "   reportgenerator not found, installing..." -ForegroundColor Yellow
    dotnet tool install -g dotnet-reportgenerator-globaltool
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to install reportgenerator" -ForegroundColor Red
        exit 1
    }
    Write-Host "   reportgenerator installed successfully" -ForegroundColor Green
} else {
    Write-Host "   reportgenerator is available" -ForegroundColor Green
}

# Generate coverage report
Write-Host "Generating coverage report..." -ForegroundColor Yellow
# Suppress warnings about MessagePack generated files that get cleaned up
$reportOutput = reportgenerator -reports:"$coverageFile" -targetdir:"coverage-report" -reporttypes:"Html;HtmlSummary;Badges;TextSummary" 2>&1
$reportOutput | Where-Object { $_ -notmatch "MessagePack\.SourceGenerator.*does not exist" } | Write-Host
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to generate coverage report!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "   Coverage report generated successfully" -ForegroundColor Green

# Display summary
if (Test-Path "coverage-report\Summary.txt") {
    Write-Host "Coverage Summary:" -ForegroundColor Blue
    Write-Host "=================" -ForegroundColor Blue
    $summary = Get-Content "coverage-report\Summary.txt" | Select-Object -Skip 1 -First 20
    $summary | ForEach-Object {
        if ($_ -match "Line coverage:|Branch coverage:|Method coverage:") {
            Write-Host "   $_" -ForegroundColor Green
        } elseif ($_ -notmatch "^\s*$") {
            Write-Host "   $_"
        }
    }
}

# Open report in browser
if ($OpenReport -and (Test-Path "coverage-report\index.html")) {
    Write-Host "Opening coverage report in browser..." -ForegroundColor Yellow
    $reportPath = (Get-Item "coverage-report\index.html").FullName
    Start-Process $reportPath
    Write-Host "   Report opened: $reportPath" -ForegroundColor Green
}

Write-Host "Coverage report generation completed!" -ForegroundColor Green
$reportLocation = Join-Path $PWD "coverage-report"
Write-Host "Report location: $reportLocation" -ForegroundColor Blue
