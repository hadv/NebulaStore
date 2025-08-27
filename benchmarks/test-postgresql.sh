#!/bin/bash

# PostgreSQL Connection Test Script
# This script helps test and setup PostgreSQL for the benchmark

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

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

# Default connection parameters
HOST="localhost"
PORT="5432"
DATABASE="benchmark"
USERNAME="postchain"
PASSWORD=""

# Function to show help
show_help() {
    echo "PostgreSQL Connection Test Script"
    echo ""
    echo "Usage: $0 [options]"
    echo ""
    echo "Options:"
    echo "  -h, --host <host>         PostgreSQL host (default: localhost)"
    echo "  -p, --port <port>         PostgreSQL port (default: 5432)"
    echo "  -d, --database <db>       Database name (default: benchmark)"
    echo "  -u, --username <user>     Username (default: postchain)"
    echo "  -w, --password <pass>     Password (will prompt if not provided)"
    echo "  --help                    Show this help"
    echo ""
    echo "Examples:"
    echo "  $0                                    # Test with defaults"
    echo "  $0 -u postgres -w mypassword         # Test with postgres user"
    echo "  $0 --host myserver --port 5433       # Test remote server"
    echo ""
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -h|--host)
            HOST="$2"
            shift 2
            ;;
        -p|--port)
            PORT="$2"
            shift 2
            ;;
        -d|--database)
            DATABASE="$2"
            shift 2
            ;;
        -u|--username)
            USERNAME="$2"
            shift 2
            ;;
        -w|--password)
            PASSWORD="$2"
            shift 2
            ;;
        --help)
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

# Prompt for password if not provided
if [ -z "$PASSWORD" ]; then
    echo -n "Enter password for user '$USERNAME': "
    read -s PASSWORD
    echo
fi

# Test basic connection
print_info "Testing PostgreSQL connection..."
print_info "Host: $HOST:$PORT"
print_info "Username: $USERNAME"
print_info "Database: $DATABASE"
echo

# Check if psql is available
if ! command -v psql &> /dev/null; then
    print_error "psql command not found. Please install PostgreSQL client tools."
    exit 1
fi

# Test connection to PostgreSQL server
print_info "Testing server connection..."
if PGPASSWORD="$PASSWORD" psql -h "$HOST" -p "$PORT" -U "$USERNAME" -d postgres -c "SELECT version();" &>/dev/null; then
    print_success "Successfully connected to PostgreSQL server"
else
    print_error "Failed to connect to PostgreSQL server"
    print_info "Please check your connection parameters and ensure PostgreSQL is running"
    exit 1
fi

# Check if database exists
print_info "Checking if database '$DATABASE' exists..."
DB_EXISTS=$(PGPASSWORD="$PASSWORD" psql -h "$HOST" -p "$PORT" -U "$USERNAME" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$DATABASE';" 2>/dev/null || echo "")

if [ "$DB_EXISTS" = "1" ]; then
    print_success "Database '$DATABASE' already exists"
    
    # Test connection to the database
    print_info "Testing connection to database '$DATABASE'..."
    if PGPASSWORD="$PASSWORD" psql -h "$HOST" -p "$PORT" -U "$USERNAME" -d "$DATABASE" -c "SELECT 1;" &>/dev/null; then
        print_success "Successfully connected to database '$DATABASE'"
    else
        print_error "Failed to connect to database '$DATABASE'"
        exit 1
    fi
else
    print_warning "Database '$DATABASE' does not exist"
    
    # Try to create the database
    print_info "Attempting to create database '$DATABASE'..."
    if PGPASSWORD="$PASSWORD" psql -h "$HOST" -p "$PORT" -U "$USERNAME" -d postgres -c "CREATE DATABASE $DATABASE;" &>/dev/null; then
        print_success "Successfully created database '$DATABASE'"
    else
        print_error "Failed to create database '$DATABASE'"
        print_info "You may need to:"
        print_info "1. Grant CREATEDB privilege: ALTER USER $USERNAME CREATEDB;"
        print_info "2. Create the database as a superuser: CREATE DATABASE $DATABASE;"
        print_info "3. Grant permissions: GRANT ALL PRIVILEGES ON DATABASE $DATABASE TO $USERNAME;"
        exit 1
    fi
fi

# Test table creation permissions
print_info "Testing table creation permissions..."
TEST_TABLE="benchmark_test_$(date +%s)"
if PGPASSWORD="$PASSWORD" psql -h "$HOST" -p "$PORT" -U "$USERNAME" -d "$DATABASE" -c "CREATE TABLE $TEST_TABLE (id SERIAL PRIMARY KEY, test_col TEXT); DROP TABLE $TEST_TABLE;" &>/dev/null; then
    print_success "Table creation permissions verified"
else
    print_error "Failed to create tables in database '$DATABASE'"
    print_info "You may need to grant permissions: GRANT ALL PRIVILEGES ON DATABASE $DATABASE TO $USERNAME;"
    exit 1
fi

# Generate connection string
CONNECTION_STRING="Host=$HOST;Port=$PORT;Database=$DATABASE;Username=$USERNAME;Password=$PASSWORD"

print_success "PostgreSQL setup verification completed!"
echo
print_info "Connection string for benchmark:"
echo "\"$CONNECTION_STRING\""
echo
print_info "To run the benchmark with PostgreSQL:"
echo "./run-benchmark.sh --quick --verbose --postgresql \"$CONNECTION_STRING\""
echo
print_info "Or for a full benchmark:"
echo "./run-benchmark.sh --full --postgresql \"$CONNECTION_STRING\""
