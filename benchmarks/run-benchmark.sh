#!/bin/bash

# NebulaStore vs MySQL Performance Benchmark Runner
# This script helps you run the benchmark with common configurations

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default values
RECORDS=3000000
BATCH_SIZE=10000
QUERY_COUNT=1000
POSTGRESQL_CONNECTION=""
STORAGE_DIR="benchmark-storage"
VERBOSE=false

# Function to print colored output
print_info() {
    echo -e "${BLUE}ℹ️  $1${NC}"
}

print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

# Function to display help
show_help() {
    echo "NebulaStore vs MySQL Performance Benchmark Runner"
    echo ""
    echo "Usage: $0 [options]"
    echo ""
    echo "Options:"
    echo "  -r, --records <number>        Number of records (default: 3,000,000)"
    echo "  -b, --batch-size <number>     Batch size (default: 10,000)"
    echo "  -q, --query-count <number>    Query count (default: 1,000)"
    echo "  -p, --postgresql <connection> PostgreSQL connection string"
    echo "  -s, --storage-dir <path>      Storage directory (default: benchmark-storage)"
    echo "  -v, --verbose                 Enable verbose output"
    echo "  --quick                       Quick test with 100K records"
    echo "  --medium                      Medium test with 1M records"
    echo "  --full                        Full test with 3M records (default)"
    echo "  --docker-postgresql           Use Docker PostgreSQL (starts container)"
    echo "  -h, --help                    Show this help"
    echo ""
    echo "Examples:"
    echo "  $0 --quick --verbose                    # Quick test with verbose output"
    echo "  $0 --medium --docker-postgresql         # Medium test with Docker PostgreSQL"
    echo "  $0 --full --postgresql \"Host=localhost;Database=benchmark;Username=postchain;Password=yourpassword\""
    echo ""
}

# Function to start Docker PostgreSQL
start_docker_postgresql() {
    print_info "Starting Docker PostgreSQL container..."

    # Check if Docker is available
    if ! command -v docker &> /dev/null; then
        print_error "Docker is not installed or not in PATH"
        exit 1
    fi

    # Stop existing container if running
    docker stop postgresql-benchmark 2>/dev/null || true
    docker rm postgresql-benchmark 2>/dev/null || true

    # Start new container
    docker run --name postgresql-benchmark \
        -e POSTGRES_PASSWORD=password \
        -e POSTGRES_USER=postchain \
        -e POSTGRES_DB=benchmark \
        -p 5432:5432 \
        -d postgres:16

    print_info "Waiting for PostgreSQL to be ready..."
    sleep 15

    # Test connection
    for i in {1..30}; do
        if docker exec postgresql-benchmark pg_isready -U postchain -d benchmark &>/dev/null; then
            print_success "PostgreSQL is ready!"
            POSTGRESQL_CONNECTION="Host=localhost;Port=5432;Database=benchmark;Username=postchain;Password=password;"
            return 0
        fi
        print_info "Waiting for PostgreSQL... ($i/30)"
        sleep 2
    done

    print_error "PostgreSQL failed to start properly"
    exit 1
}

# Function to check prerequisites
check_prerequisites() {
    print_info "Checking prerequisites..."
    
    # Check .NET 8
    if ! command -v dotnet &> /dev/null; then
        print_error ".NET SDK is not installed"
        print_info "Please install .NET 9 SDK from https://dotnet.microsoft.com/download"
        exit 1
    fi
    
    # Check .NET version
    DOTNET_VERSION=$(dotnet --version)
    if [[ ! $DOTNET_VERSION =~ ^9\. ]]; then
        print_warning ".NET version is $DOTNET_VERSION, .NET 9 is recommended"
    fi
    
    # Check available disk space (rough estimate)
    AVAILABLE_SPACE=$(df . | tail -1 | awk '{print $4}')
    REQUIRED_SPACE=$((RECORDS * 500 / 1024)) # Rough estimate in KB
    
    if [ $AVAILABLE_SPACE -lt $REQUIRED_SPACE ]; then
        print_warning "Low disk space. Available: ${AVAILABLE_SPACE}KB, Estimated needed: ${REQUIRED_SPACE}KB"
    fi
    
    print_success "Prerequisites check completed"
}

# Function to build the project
build_project() {
    print_info "Building benchmark project..."

    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    cd "$SCRIPT_DIR/NebulaStore.Benchmarks"
    
    if dotnet build --configuration Release; then
        print_success "Build completed successfully"
    else
        print_error "Build failed"
        exit 1
    fi
}

# Function to run the benchmark
run_benchmark() {
    print_info "Starting benchmark with configuration:"
    echo "  Records: $RECORDS"
    echo "  Batch Size: $BATCH_SIZE"
    echo "  Query Count: $QUERY_COUNT"
    echo "  Storage Directory: $STORAGE_DIR"
    echo "  PostgreSQL: $([ -n "$POSTGRESQL_CONNECTION" ] && echo "Enabled" || echo "Disabled")"
    echo "  Verbose: $VERBOSE"
    echo ""
    
    # Build command arguments
    ARGS="--records $RECORDS --batch-size $BATCH_SIZE --query-count $QUERY_COUNT --storage-dir $STORAGE_DIR"
    
    if [ -n "$POSTGRESQL_CONNECTION" ]; then
        ARGS="$ARGS --postgresql-connection \"$POSTGRESQL_CONNECTION\""
    fi
    
    if [ "$VERBOSE" = true ]; then
        ARGS="$ARGS --verbose"
    fi
    
    # Run the benchmark
    cd "$SCRIPT_DIR/NebulaStore.Benchmarks"
    eval "dotnet run --configuration Release -- $ARGS"
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -r|--records)
            RECORDS="$2"
            shift 2
            ;;
        -b|--batch-size)
            BATCH_SIZE="$2"
            shift 2
            ;;
        -q|--query-count)
            QUERY_COUNT="$2"
            shift 2
            ;;
        -p|--postgresql|--postgres)
            POSTGRESQL_CONNECTION="$2"
            shift 2
            ;;
        -s|--storage-dir)
            STORAGE_DIR="$2"
            shift 2
            ;;
        -v|--verbose)
            VERBOSE=true
            shift
            ;;
        --quick)
            RECORDS=100000
            BATCH_SIZE=5000
            QUERY_COUNT=500
            shift
            ;;
        --medium)
            RECORDS=1000000
            BATCH_SIZE=10000
            QUERY_COUNT=1000
            shift
            ;;
        --full)
            RECORDS=3000000
            BATCH_SIZE=10000
            QUERY_COUNT=1000
            shift
            ;;
        --docker-postgresql)
            start_docker_postgresql
            shift
            ;;
        -h|--help)
            show_help
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            show_help
            exit 1
            ;;
    esac
done

# Main execution
main() {
    echo "🚀 NebulaStore vs MySQL Performance Benchmark Runner"
    echo "===================================================="
    echo ""
    
    check_prerequisites
    build_project
    run_benchmark
    
    print_success "Benchmark completed!"
    
    if [ -n "$POSTGRESQL_CONNECTION" ] && [[ $POSTGRESQL_CONNECTION == *"localhost"* ]]; then
        print_info "To stop Docker PostgreSQL: docker stop postgresql-benchmark"
    fi
}

# Run main function
main
