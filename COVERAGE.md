# Test Coverage for NebulaStore

This document explains how to generate test coverage reports for the NebulaStore project.

## Quick Start

### Windows (PowerShell) - Recommended
```powershell
.\coverage.ps1
```

### Linux/macOS (Bash)
```bash
./generate-coverage.sh
```

## Manual Commands

If you prefer to run the commands manually:

```bash
# Run tests with coverage collection
dotnet test --collect:"XPlat Code Coverage"

# Install report generator (if not already installed)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:"Html;HtmlSummary;Badges;TextSummary"
```

## Script Options

### PowerShell Script (`coverage.ps1`)
- `-OpenReport $true` (default) - Opens the report in browser automatically
- `-OpenReport $false` - Generates report without opening browser

Examples:
```powershell
# Generate and open report
.\coverage.ps1

# Generate report only (don't open browser)
.\coverage.ps1 -OpenReport $false
```

### Bash Script (`generate-coverage.sh`)
- Default behavior - Opens the report in browser automatically
- `--no-open` or `--no-browser` - Generates report without opening browser
- `-h` or `--help` - Shows usage information

Examples:
```bash
# Generate and open report
./generate-coverage.sh

# Generate report only (don't open browser)
./generate-coverage.sh --no-open

# Show help
./generate-coverage.sh --help
```

## Fresh Generation Process

The scripts ensure coverage reports are **always generated from scratch** by:

1. **Cleaning old reports**: Removes `coverage-report/` directory
2. **Removing test results**: Deletes all `TestResults/` directories recursively
3. **Cleaning coverage files**: Removes any `*.cobertura.xml` files
4. **Cleaning build outputs**: Runs `dotnet clean` to ensure fresh compilation
5. **Fresh build**: Rebuilds the solution with latest code
6. **Fresh test run**: Runs tests with `--no-build` flag for consistency

## Output

The scripts will generate:

1. **HTML Report**: `coverage-report/index.html` - Interactive coverage report
2. **Summary Report**: `coverage-report/summary.html` - High-level summary
3. **Text Summary**: `coverage-report/Summary.txt` - Plain text summary
4. **Coverage Badges**: Various SVG badges for documentation

## Current Coverage Metrics

Based on the latest run:

- **Line Coverage: 70.3%** (619 of 880 lines)
- **Branch Coverage: 59.3%** (153 of 258 branches)  
- **Method Coverage: 78%** (146 of 187 methods)

### Coverage by Assembly

1. **NebulaStore.Storage.EmbeddedConfiguration: 88.2%** ✅
2. **NebulaStore.Storage.Embedded: 71.1%** ✅
3. **NebulaStore.Storage: 62.3%** ⚠️

## Files Ignored by Git

The following coverage-related files are automatically ignored by git:

- `coverage-report/` - Generated HTML reports
- `TestResults/` - Test result directories
- `*.cobertura.xml` - Coverage data files

## Requirements

- .NET 9.0 SDK
- `dotnet-reportgenerator-globaltool` (automatically installed by scripts)

## Troubleshooting

### "dotnet command not found"
Make sure .NET 9.0 SDK is installed and in your PATH. The PowerShell script automatically refreshes the PATH.

### "reportgenerator command not found"  
The scripts will automatically install the reportgenerator tool if it's not found.

### No coverage files found
Ensure tests are running successfully. The coverage collector only generates files when tests execute.

## Integration with CI/CD

You can integrate coverage reporting into your CI/CD pipeline:

```yaml
# Example GitHub Actions step
- name: Generate Coverage Report
  run: |
    dotnet test --collect:"XPlat Code Coverage"
    dotnet tool install -g dotnet-reportgenerator-globaltool
    reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:"Html;Cobertura"
```

## Coverage Goals

Consider these targets for improving coverage:

- **Line Coverage**: Target 80%+ 
- **Branch Coverage**: Target 70%+
- **Method Coverage**: Target 85%+

Focus on testing:
- Core business logic
- Error handling paths
- Edge cases and boundary conditions
