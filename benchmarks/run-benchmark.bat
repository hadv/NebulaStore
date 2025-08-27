@echo off
setlocal enabledelayedexpansion

REM NebulaStore vs MySQL Performance Benchmark Runner for Windows
REM This script helps you run the benchmark with common configurations

set RECORDS=3000000
set BATCH_SIZE=10000
set QUERY_COUNT=1000
set MYSQL_CONNECTION=
set STORAGE_DIR=benchmark-storage
set VERBOSE=false
set DOCKER_MYSQL=false

:parse_args
if "%~1"=="" goto :check_prerequisites
if "%~1"=="-r" (
    set RECORDS=%~2
    shift
    shift
    goto :parse_args
)
if "%~1"=="--records" (
    set RECORDS=%~2
    shift
    shift
    goto :parse_args
)
if "%~1"=="-b" (
    set BATCH_SIZE=%~2
    shift
    shift
    goto :parse_args
)
if "%~1"=="--batch-size" (
    set BATCH_SIZE=%~2
    shift
    shift
    goto :parse_args
)
if "%~1"=="-q" (
    set QUERY_COUNT=%~2
    shift
    shift
    goto :parse_args
)
if "%~1"=="--query-count" (
    set QUERY_COUNT=%~2
    shift
    shift
    goto :parse_args
)
if "%~1"=="-m" (
    set MYSQL_CONNECTION=%~2
    shift
    shift
    goto :parse_args
)
if "%~1"=="--mysql" (
    set MYSQL_CONNECTION=%~2
    shift
    shift
    goto :parse_args
)
if "%~1"=="-s" (
    set STORAGE_DIR=%~2
    shift
    shift
    goto :parse_args
)
if "%~1"=="--storage-dir" (
    set STORAGE_DIR=%~2
    shift
    shift
    goto :parse_args
)
if "%~1"=="-v" (
    set VERBOSE=true
    shift
    goto :parse_args
)
if "%~1"=="--verbose" (
    set VERBOSE=true
    shift
    goto :parse_args
)
if "%~1"=="--quick" (
    set RECORDS=100000
    set BATCH_SIZE=5000
    set QUERY_COUNT=500
    shift
    goto :parse_args
)
if "%~1"=="--medium" (
    set RECORDS=1000000
    set BATCH_SIZE=10000
    set QUERY_COUNT=1000
    shift
    goto :parse_args
)
if "%~1"=="--full" (
    set RECORDS=3000000
    set BATCH_SIZE=10000
    set QUERY_COUNT=1000
    shift
    goto :parse_args
)
if "%~1"=="--docker-mysql" (
    set DOCKER_MYSQL=true
    shift
    goto :parse_args
)
if "%~1"=="-h" goto :show_help
if "%~1"=="--help" goto :show_help

echo Unknown option: %~1
goto :show_help

:show_help
echo NebulaStore vs MySQL Performance Benchmark Runner
echo.
echo Usage: %~nx0 [options]
echo.
echo Options:
echo   -r, --records ^<number^>        Number of records (default: 3,000,000)
echo   -b, --batch-size ^<number^>     Batch size (default: 10,000)
echo   -q, --query-count ^<number^>    Query count (default: 1,000)
echo   -m, --mysql ^<connection^>      MySQL connection string
echo   -s, --storage-dir ^<path^>      Storage directory (default: benchmark-storage)
echo   -v, --verbose                 Enable verbose output
echo   --quick                       Quick test with 100K records
echo   --medium                      Medium test with 1M records
echo   --full                        Full test with 3M records (default)
echo   --docker-mysql                Use Docker MySQL (starts container)
echo   -h, --help                    Show this help
echo.
echo Examples:
echo   %~nx0 --quick --verbose
echo   %~nx0 --medium --docker-mysql
echo   %~nx0 --full --mysql "Server=localhost;Database=benchmark;Uid=root;Pwd=password;"
echo.
exit /b 0

:check_prerequisites
echo 🚀 NebulaStore vs MySQL Performance Benchmark Runner
echo ====================================================
echo.

echo ℹ️  Checking prerequisites...

REM Check .NET
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ❌ .NET SDK is not installed
    echo Please install .NET 9 SDK from https://dotnet.microsoft.com/download
    exit /b 1
)

echo ✅ Prerequisites check completed
echo.

:start_docker_mysql
if "%DOCKER_MYSQL%"=="true" (
    echo ℹ️  Starting Docker MySQL container...
    
    REM Check if Docker is available
    docker --version >nul 2>&1
    if errorlevel 1 (
        echo ❌ Docker is not installed or not in PATH
        exit /b 1
    )
    
    REM Stop existing container if running
    docker stop mysql-benchmark >nul 2>&1
    docker rm mysql-benchmark >nul 2>&1
    
    REM Start new container
    docker run --name mysql-benchmark -e MYSQL_ROOT_PASSWORD=password -e MYSQL_DATABASE=benchmark -p 3306:3306 -d mysql:8.0
    
    echo ℹ️  Waiting for MySQL to be ready...
    timeout /t 30 /nobreak >nul
    
    REM Test connection (simplified for Windows)
    echo ✅ MySQL container started (please wait a moment for it to be fully ready)
    set MYSQL_CONNECTION=Server=localhost;Port=3306;Database=benchmark;Uid=root;Pwd=password;
)

:build_project
echo ℹ️  Building benchmark project...

cd /d "%~dp0NebulaStore.Benchmarks"

dotnet build --configuration Release
if errorlevel 1 (
    echo ❌ Build failed
    exit /b 1
)

echo ✅ Build completed successfully
echo.

:run_benchmark
echo ℹ️  Starting benchmark with configuration:
echo   Records: %RECORDS%
echo   Batch Size: %BATCH_SIZE%
echo   Query Count: %QUERY_COUNT%
echo   Storage Directory: %STORAGE_DIR%
if defined MYSQL_CONNECTION (
    echo   MySQL: Enabled
) else (
    echo   MySQL: Disabled
)
echo   Verbose: %VERBOSE%
echo.

REM Build command arguments
set ARGS=--records %RECORDS% --batch-size %BATCH_SIZE% --query-count %QUERY_COUNT% --storage-dir %STORAGE_DIR%

if defined MYSQL_CONNECTION (
    set ARGS=%ARGS% --mysql-connection "%MYSQL_CONNECTION%"
)

if "%VERBOSE%"=="true" (
    set ARGS=%ARGS% --verbose
)

REM Run the benchmark
dotnet run --configuration Release -- %ARGS%

echo.
echo ✅ Benchmark completed!

if "%DOCKER_MYSQL%"=="true" (
    echo ℹ️  To stop Docker MySQL: docker stop mysql-benchmark
)

exit /b 0
