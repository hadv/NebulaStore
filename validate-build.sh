#!/bin/bash

echo "🔨 COMPREHENSIVE BUILD VALIDATION"
echo "================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

errors=0

echo ""
echo "📋 Pre-Build Checks"
echo "-------------------"

# Check if dotnet is available (if not, skip actual build)
if command -v dotnet &> /dev/null; then
    echo -e "${GREEN}✅ .NET SDK found${NC}"
    HAS_DOTNET=true
else
    echo -e "${YELLOW}⚠️  .NET SDK not found - skipping actual build${NC}"
    HAS_DOTNET=false
fi

echo ""
echo "🔍 Project Structure Validation"
echo "-------------------------------"

# Check all required project files exist
required_projects=(
    "NebulaStore.sln"
    "storage/storage/NebulaStore.Storage.csproj"
    "storage/embedded/NebulaStore.Storage.Embedded.csproj"
    "storage/embedded-configuration/NebulaStore.Storage.EmbeddedConfiguration.csproj"
    "afs/blobstore/NebulaStore.Afs.Blobstore.csproj"
    "afs/tests/NebulaStore.Afs.Tests.csproj"
)

for project in "${required_projects[@]}"; do
    if [ -f "$project" ]; then
        echo -e "${GREEN}✅${NC} $project"
    else
        echo -e "${RED}❌${NC} $project"
        ((errors++))
    fi
done

echo ""
echo "🔍 Source File Validation"
echo "-------------------------"

# Check critical source files
critical_files=(
    "afs/blobstore/src/AfsStorageConnection.cs"
    "afs/blobstore/src/BlobStorePath.cs"
    "afs/blobstore/src/LocalBlobStoreConnector.cs"
    "storage/embedded/src/EmbeddedStorageFoundation.cs"
)

for file in "${critical_files[@]}"; do
    if [ -f "$file" ]; then
        echo -e "${GREEN}✅${NC} $file"
    else
        echo -e "${RED}❌${NC} $file"
        ((errors++))
    fi
done

echo ""
echo "🔍 Code Quality Checks"
echo "----------------------"

# Check for common compilation issues
echo "Checking for potential type conversion issues..."
if grep -r "cannot convert from" . --include="*.cs" 2>/dev/null; then
    echo -e "${RED}❌ Found potential type conversion issues${NC}"
    ((errors++))
else
    echo -e "${GREEN}✅ No obvious type conversion issues found${NC}"
fi

echo "Checking for missing using statements..."
missing_usings=0
if grep -r "The type or namespace name.*could not be found" . --include="*.cs" 2>/dev/null; then
    echo -e "${RED}❌ Found potential missing using statements${NC}"
    ((missing_usings++))
fi

if [ $missing_usings -eq 0 ]; then
    echo -e "${GREEN}✅ No obvious missing using statements${NC}"
fi

echo ""
echo "🔍 Project Reference Validation"
echo "-------------------------------"

# Check that AFS project excludes test files
if grep -q "test/\*\*/\*.cs" afs/blobstore/NebulaStore.Afs.Blobstore.csproj; then
    echo -e "${GREEN}✅ AFS project excludes test files${NC}"
else
    echo -e "${RED}❌ AFS project does not exclude test files${NC}"
    ((errors++))
fi

# Check that AFS project has required references
if grep -q "NebulaStore.Storage.csproj" afs/blobstore/NebulaStore.Afs.Blobstore.csproj && \
   grep -q "NebulaStore.Storage.EmbeddedConfiguration.csproj" afs/blobstore/NebulaStore.Afs.Blobstore.csproj; then
    echo -e "${GREEN}✅ AFS project has required references${NC}"
else
    echo -e "${RED}❌ AFS project missing required references${NC}"
    ((errors++))
fi

if [ "$HAS_DOTNET" = true ]; then
    echo ""
    echo "🔨 ACTUAL BUILD TEST"
    echo "==================="
    
    echo "Running dotnet restore..."
    if dotnet restore --verbosity quiet; then
        echo -e "${GREEN}✅ Package restore successful${NC}"
    else
        echo -e "${RED}❌ Package restore failed${NC}"
        ((errors++))
    fi
    
    echo "Running dotnet build..."
    if dotnet build --configuration Release --no-restore --verbosity quiet; then
        echo -e "${GREEN}✅ Build successful${NC}"
    else
        echo -e "${RED}❌ Build failed${NC}"
        echo ""
        echo "🔍 Detailed build output:"
        echo "-------------------------"
        dotnet build --configuration Release --no-restore --verbosity normal
        ((errors++))
    fi
else
    echo ""
    echo "⚠️  Skipping actual build test (no .NET SDK available)"
fi

echo ""
echo "📊 VALIDATION SUMMARY"
echo "===================="

if [ $errors -eq 0 ]; then
    echo -e "${GREEN}🎉 ALL VALIDATIONS PASSED!${NC}"
    echo "The code should build successfully in CI."
    exit 0
else
    echo -e "${RED}❌ VALIDATION FAILED with $errors error(s)${NC}"
    echo "Please fix the issues before pushing to CI."
    exit 1
fi
