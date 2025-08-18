#!/bin/bash
# NebulaStore Test Coverage Generator (Bash version)
# This script runs tests with coverage and generates an HTML report

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
    exit 1
fi
echo -e "   ${GREEN}✓ Found coverage file: $(basename "$COVERAGE_FILE")${NC}"

# Check if reportgenerator is installed
echo -e "\n${YELLOW}🔧 Checking reportgenerator tool...${NC}"
if ! command -v reportgenerator &> /dev/null; then
    echo -e "   ${YELLOW}⚠️  reportgenerator not found, installing...${NC}"
    dotnet tool install -g dotnet-reportgenerator-globaltool
    if [ $? -eq 0 ]; then
        echo -e "   ${GREEN}✓ reportgenerator installed successfully${NC}"
    else
        echo -e "${RED}❌ Failed to install reportgenerator${NC}"
        exit 1
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

# Open report (platform-specific)
if [ -f "coverage-report/index.html" ]; then
    echo -e "\n${YELLOW}🌐 Coverage report ready!${NC}"
    REPORT_PATH="$(pwd)/coverage-report/index.html"
    echo -e "   ${GREEN}✓ Report location: $REPORT_PATH${NC}"
    
    # Try to open in browser (platform-specific)
    if command -v xdg-open &> /dev/null; then
        xdg-open "$REPORT_PATH" 2>/dev/null &
    elif command -v open &> /dev/null; then
        open "$REPORT_PATH" 2>/dev/null &
    else
        echo -e "   ${YELLOW}💡 Open this file in your browser: file://$REPORT_PATH${NC}"
    fi
fi

echo -e "\n${GREEN}✅ Coverage report generation completed!${NC}"
echo -e "${BLUE}📁 Report location: $(pwd)/coverage-report${NC}"
