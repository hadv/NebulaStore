#!/bin/bash
# NebulaStore Test Coverage Generator (Bash version)
# This script runs tests with coverage and generates an HTML report

# Parse command line arguments
OPEN_REPORT=true
while [[ $# -gt 0 ]]; do
    case $1 in
        --no-open|--no-browser)
            OPEN_REPORT=false
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [--no-open|--no-browser]"
            echo "  --no-open, --no-browser    Don't open the report in browser"
            echo "  -h, --help                 Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use -h or --help for usage information"
            exit 1
            ;;
    esac
done

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}🧪 NebulaStore Test Coverage Generator${NC}"
echo -e "${BLUE}=====================================${NC}"

# Clean up old coverage reports and test results
echo -e "\n${YELLOW}🧹 Cleaning up old coverage reports and test results...${NC}"

# Remove coverage report directory
if [ -d "coverage-report" ]; then
    rm -rf "coverage-report"
    echo -e "   ${GREEN}✓ Removed old coverage-report directory${NC}"
fi

# Find and remove all TestResults directories recursively
find . -type d -name "TestResults" -exec rm -rf {} + 2>/dev/null
echo -e "   ${GREEN}✓ Cleaned up old TestResults directories${NC}"

# Remove any standalone coverage files
find . -name "*.cobertura.xml" -type f -delete 2>/dev/null
echo -e "   ${GREEN}✓ Removed old coverage files${NC}"

# Clean build outputs to ensure fresh compilation
echo -e "   ${YELLOW}Cleaning build outputs for fresh compilation...${NC}"
dotnet clean --verbosity quiet >/dev/null 2>&1
echo -e "   ${GREEN}✓ Build outputs cleaned${NC}"

# Build solution to ensure latest code
echo -e "\n${YELLOW}🔨 Building solution to ensure latest code...${NC}"
dotnet build --verbosity quiet >/dev/null 2>&1
if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Build failed! Cannot proceed with testing.${NC}"
    exit 1
fi
echo -e "   ${GREEN}✓ Build completed successfully${NC}"

# Run tests with coverage
echo -e "\n${YELLOW}🔬 Running tests with coverage collection...${NC}"
dotnet test --collect:"XPlat Code Coverage" --verbosity minimal --no-build
if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Tests failed! Coverage report generation aborted.${NC}"
    exit 1
fi
echo -e "   ${GREEN}✓ Tests completed successfully${NC}"

# Find coverage file
echo -e "\n${YELLOW}📊 Locating coverage files...${NC}"
COVERAGE_FILE=$(find . -name "coverage.cobertura.xml" | head -1)
if [ -z "$COVERAGE_FILE" ]; then
    echo -e "${RED}❌ No coverage files found!${NC}"
    echo -e "${YELLOW}Searching for TestResults directories...${NC}"
    find . -name "TestResults" -type d | while read dir; do
        echo -e "   Found TestResults: $dir"
        find "$dir" -name "*.xml" | while read file; do
            echo -e "     Found file: $file"
        done
    done
    echo -e "${RED}Please ensure tests ran successfully and coverage was collected.${NC}"
    exit 1
fi
echo -e "   ${GREEN}✓ Found coverage file: $(basename "$COVERAGE_FILE")${NC}"
echo -e "   ${BLUE}Full path: $COVERAGE_FILE${NC}"

# Check if reportgenerator is installed
echo -e "\n${YELLOW}🔧 Checking reportgenerator tool...${NC}"
if ! command -v reportgenerator &> /dev/null; then
    echo -e "   ${YELLOW}⚠️  reportgenerator not found, installing...${NC}"
    dotnet tool install -g dotnet-reportgenerator-globaltool
    if [ $? -eq 0 ]; then
        echo -e "   ${GREEN}✓ reportgenerator installed successfully${NC}"
        # Refresh PATH to ensure tool is available
        export PATH="$PATH:$HOME/.dotnet/tools"
    else
        echo -e "${RED}❌ Failed to install reportgenerator${NC}"
        echo -e "${YELLOW}Trying alternative installation method...${NC}"
        # Try with explicit path
        export PATH="$PATH:$HOME/.dotnet/tools"
        if command -v reportgenerator &> /dev/null; then
            echo -e "   ${GREEN}✓ reportgenerator found in PATH${NC}"
        else
            echo -e "${RED}❌ reportgenerator installation failed${NC}"
            exit 1
        fi
    fi
else
    echo -e "   ${GREEN}✓ reportgenerator is available${NC}"
fi

# Generate coverage report
echo -e "\n${YELLOW}📈 Generating coverage report...${NC}"
# Suppress warnings about MessagePack generated files that get cleaned up
reportgenerator -reports:"$COVERAGE_FILE" -targetdir:"coverage-report" -reporttypes:"Html;HtmlSummary;Badges;TextSummary" 2>&1 | grep -v "MessagePack\.SourceGenerator.*does not exist"
if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Failed to generate coverage report!${NC}"
    exit 1
fi
echo -e "   ${GREEN}✓ Coverage report generated successfully${NC}"

# Display summary
if [ -f "coverage-report/Summary.txt" ]; then
    echo -e "\n${BLUE}📋 Coverage Summary:${NC}"
    echo -e "${BLUE}===================${NC}"
    grep -E "Line coverage:|Branch coverage:|Method coverage:" "coverage-report/Summary.txt" | while read line; do
        echo -e "   ${GREEN}$line${NC}"
    done
fi

# Open report in browser
if [ -f "coverage-report/index.html" ]; then
    REPORT_PATH="$(pwd)/coverage-report/index.html"

    if [ "$OPEN_REPORT" = true ]; then
        echo -e "\n${YELLOW}🌐 Opening coverage report in browser...${NC}"

        # Try different browser opening commands based on platform
        OPENED=false

        # Linux
        if command -v xdg-open &> /dev/null; then
            xdg-open "file://$REPORT_PATH" 2>/dev/null &
            OPENED=true
        # macOS
        elif command -v open &> /dev/null; then
            open "file://$REPORT_PATH" 2>/dev/null &
            OPENED=true
        # Windows with WSL
        elif command -v cmd.exe &> /dev/null; then
            cmd.exe /c start "file://$REPORT_PATH" 2>/dev/null &
            OPENED=true
        # Try common browsers directly
        elif command -v firefox &> /dev/null; then
            firefox "file://$REPORT_PATH" 2>/dev/null &
            OPENED=true
        elif command -v google-chrome &> /dev/null; then
            google-chrome "file://$REPORT_PATH" 2>/dev/null &
            OPENED=true
        elif command -v chromium &> /dev/null; then
            chromium "file://$REPORT_PATH" 2>/dev/null &
            OPENED=true
        fi

        if [ "$OPENED" = true ]; then
            echo -e "   ${GREEN}✓ Report opened: $REPORT_PATH${NC}"
        else
            echo -e "   ${YELLOW}⚠️  Could not auto-open browser${NC}"
            echo -e "   ${YELLOW}💡 Please open this file manually: file://$REPORT_PATH${NC}"
        fi
    else
        echo -e "\n${YELLOW}🌐 Coverage report ready!${NC}"
        echo -e "   ${GREEN}✓ Report location: $REPORT_PATH${NC}"
        echo -e "   ${BLUE}💡 Open in browser: file://$REPORT_PATH${NC}"
    fi
fi

echo -e "\n${GREEN}✅ Coverage report generation completed!${NC}"
echo -e "${BLUE}📁 Report location: $(pwd)/coverage-report${NC}"
