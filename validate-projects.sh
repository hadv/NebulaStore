#!/bin/bash

echo "🔍 Validating NebulaStore Project Structure"
echo "=========================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

errors=0
warnings=0

# Function to check if file exists
check_file() {
    if [ -f "$1" ]; then
        echo -e "${GREEN}✅${NC} Found: $1"
    else
        echo -e "${RED}❌${NC} Missing: $1"
        ((errors++))
    fi
}

# Function to check project reference
check_project_ref() {
    local project_file="$1"
    local referenced_project="$2"
    
    if [ -f "$project_file" ]; then
        if grep -q "$referenced_project" "$project_file"; then
            echo -e "${GREEN}✅${NC} $project_file references $referenced_project"
        else
            echo -e "${YELLOW}⚠️${NC}  $project_file missing reference to $referenced_project"
            ((warnings++))
        fi
    fi
}

# Function to validate XML structure
validate_xml_structure() {
    local file="$1"
    if [ -f "$file" ]; then
        # Check for common XML issues
        if grep -q "<.*>.*<.*>" "$file"; then
            # Check for proper closing tags
            local opening_tags=$(grep -o "<[^/][^>]*>" "$file" | wc -l)
            local closing_tags=$(grep -o "</[^>]*>" "$file" | wc -l)
            local self_closing=$(grep -o "<[^>]*/>" "$file" | wc -l)
            
            # Self-closing tags count as both opening and closing
            local expected_closing=$((opening_tags - self_closing))
            
            if [ "$closing_tags" -eq "$expected_closing" ]; then
                echo -e "${GREEN}✅${NC} XML structure valid: $file"
            else
                echo -e "${RED}❌${NC} XML structure invalid: $file (opening: $opening_tags, closing: $closing_tags, self-closing: $self_closing)"
                ((errors++))
            fi
        fi
    fi
}

echo ""
echo "📁 Checking Core Project Files"
echo "------------------------------"

# Check main project files
check_file "NebulaStore.sln"
check_file "storage/storage/NebulaStore.Storage.csproj"
check_file "storage/embedded/NebulaStore.Storage.Embedded.csproj"
check_file "storage/embedded-configuration/NebulaStore.Storage.EmbeddedConfiguration.csproj"

echo ""
echo "📁 Checking AFS Project Files"
echo "-----------------------------"

# Check AFS project files
check_file "afs/blobstore/NebulaStore.Afs.Blobstore.csproj"
check_file "afs/blobstore/test/NebulaStore.Afs.Blobstore.Tests.csproj"
check_file "afs/tests/NebulaStore.Afs.Tests.csproj"

echo ""
echo "📁 Checking AFS Source Files"
echo "----------------------------"

# Check AFS source files
check_file "afs/blobstore/src/AfsStorageConnection.cs"
check_file "afs/blobstore/src/BlobStorePath.cs"
check_file "afs/blobstore/src/IBlobStoreConnector.cs"
check_file "afs/blobstore/src/LocalBlobStoreConnector.cs"
check_file "afs/blobstore/src/BlobStoreFileSystem.cs"
check_file "afs/blobstore/src/BlobStoreFile.cs"
check_file "afs/blobstore/src/types/IAfsPath.cs"

echo ""
echo "📁 Checking AFS Test Files"
echo "--------------------------"

# Check AFS test files
check_file "afs/blobstore/test/BlobStorePathTests.cs"
check_file "afs/blobstore/test/LocalBlobStoreConnectorTests.cs"
check_file "afs/tests/AfsIntegrationTests.cs"

echo ""
echo "🔗 Checking Project References"
echo "------------------------------"

# Check critical project references
check_project_ref "afs/blobstore/NebulaStore.Afs.Blobstore.csproj" "NebulaStore.Storage.csproj"
check_project_ref "afs/blobstore/NebulaStore.Afs.Blobstore.csproj" "NebulaStore.Storage.EmbeddedConfiguration.csproj"
check_project_ref "afs/blobstore/test/NebulaStore.Afs.Blobstore.Tests.csproj" "NebulaStore.Afs.Blobstore.csproj"
check_project_ref "afs/tests/NebulaStore.Afs.Tests.csproj" "NebulaStore.Afs.Blobstore.csproj"
check_project_ref "storage/embedded/NebulaStore.Storage.Embedded.csproj" "NebulaStore.Afs.Blobstore.csproj"

echo ""
echo "🔍 Validating XML Structure"
echo "---------------------------"

# Validate XML structure of project files
validate_xml_structure "afs/blobstore/NebulaStore.Afs.Blobstore.csproj"
validate_xml_structure "afs/blobstore/test/NebulaStore.Afs.Blobstore.Tests.csproj"
validate_xml_structure "afs/tests/NebulaStore.Afs.Tests.csproj"

echo ""
echo "📊 Validation Summary"
echo "===================="

if [ $errors -eq 0 ] && [ $warnings -eq 0 ]; then
    echo -e "${GREEN}🎉 All validations passed!${NC}"
    echo "The project structure is ready for CI/CD."
    exit 0
elif [ $errors -eq 0 ]; then
    echo -e "${YELLOW}⚠️  Validation completed with $warnings warning(s)${NC}"
    echo "The project should build, but there may be minor issues."
    exit 0
else
    echo -e "${RED}❌ Validation failed with $errors error(s) and $warnings warning(s)${NC}"
    echo "Please fix the errors before proceeding."
    exit 1
fi
