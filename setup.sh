#!/bin/bash

# Source Code Summariser - Setup Script
# For Linux/macOS
# This script sets up the complete environment for MCP integration

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Print colored output
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_header() {
    echo ""
    echo -e "${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}"
    echo ""
}

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

print_header "Source Code Summariser - Setup"

# Check prerequisites
print_info "Checking prerequisites..."

# Check .NET 8.0
if ! command -v dotnet &> /dev/null; then
    print_error ".NET SDK not found!"
    print_info "Please install .NET 8.0 SDK from: https://dotnet.microsoft.com/download"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
print_success ".NET SDK found: $DOTNET_VERSION"

# Check Node.js
if ! command -v node &> /dev/null; then
    print_error "Node.js not found!"
    print_info "Please install Node.js 18+ from: https://nodejs.org/"
    exit 1
fi

NODE_VERSION=$(node --version)
print_success "Node.js found: $NODE_VERSION"

# Check npm
if ! command -v npm &> /dev/null; then
    print_error "npm not found!"
    exit 1
fi

NPM_VERSION=$(npm --version)
print_success "npm found: $NPM_VERSION"

# Configuration
print_header "Configuration"

# Ask for OpenAI API key
echo ""
read -p "Enter your OpenAI API Key (or press Enter to skip): " OPENAI_API_KEY
echo ""

# Ask for target directory
read -p "Enter the path to your C# codebase to analyze (default: current directory): " TARGET_DIR
TARGET_DIR=${TARGET_DIR:-$SCRIPT_DIR}

# Ask for provider
echo ""
echo "Select LLM Provider:"
echo "1) OpenAI (requires API key)"
echo "2) Local (Ollama - free, requires Ollama installed)"
echo "3) LangChain"
read -p "Enter choice (1-3, default: 1): " PROVIDER_CHOICE
PROVIDER_CHOICE=${PROVIDER_CHOICE:-1}

case $PROVIDER_CHOICE in
    1)
        PROVIDER="openai"
        if [ -z "$OPENAI_API_KEY" ]; then
            print_error "OpenAI API key is required for OpenAI provider!"
            exit 1
        fi
        ;;
    2)
        PROVIDER="local"
        print_info "Using local Ollama provider"
        if ! command -v ollama &> /dev/null; then
            print_warning "Ollama not found. Install from: https://ollama.ai/"
        fi
        ;;
    3)
        PROVIDER="langchain"
        if [ -z "$OPENAI_API_KEY" ]; then
            print_error "API key is required for LangChain provider!"
            exit 1
        fi
        ;;
    *)
        print_error "Invalid choice!"
        exit 1
        ;;
esac

# Create appsettings.json
print_header "Creating Configuration Files"

cat > appsettings.json <<EOF
{
  "TargetDirectory": "$TARGET_DIR",
  "LlmProvider": {
    "Provider": "$PROVIDER",
    "Model": "gpt-3.5-turbo",
    "MaxTokens": 150,
    "TimeoutSeconds": 30,
    "EnableEmbeddings": true,
    "LocalEndpoint": "http://localhost:11434"
  },
  "Database": {
    "ConnectionString": "Data Source=summaries.db"
  },
  "Processing": {
    "FilePattern": "*.cs",
    "ExcludedFolders": ["bin", "obj", ".git", ".vs", "node_modules", "packages"],
    "MaxRetries": 3,
    "RetryDelayMilliseconds": 1000
  }
}
EOF

print_success "Created appsettings.json"

# Set environment variable for API key
if [ -n "$OPENAI_API_KEY" ]; then
    export OPENAI_API_KEY="$OPENAI_API_KEY"

    # Add to shell profile for persistence
    SHELL_PROFILE=""
    if [ -f "$HOME/.bashrc" ]; then
        SHELL_PROFILE="$HOME/.bashrc"
    elif [ -f "$HOME/.zshrc" ]; then
        SHELL_PROFILE="$HOME/.zshrc"
    fi

    if [ -n "$SHELL_PROFILE" ]; then
        if ! grep -q "OPENAI_API_KEY" "$SHELL_PROFILE"; then
            echo "" >> "$SHELL_PROFILE"
            echo "# Source Code Summariser" >> "$SHELL_PROFILE"
            echo "export OPENAI_API_KEY=\"$OPENAI_API_KEY\"" >> "$SHELL_PROFILE"
            print_success "Added OPENAI_API_KEY to $SHELL_PROFILE"
        fi
    fi
fi

# Build C# project
print_header "Building C# Project"

print_info "Restoring NuGet packages..."
dotnet restore

print_info "Building project..."
dotnet build --configuration Release

print_success "C# project built successfully"

# Setup MCP server
print_header "Setting up MCP Server"

cd mcp-server

print_info "Installing npm dependencies..."
npm install

print_info "Building TypeScript..."
npm run build

cd ..

print_success "MCP server setup complete"

# Initialize database
print_header "Initializing Database"

if [ -d "$TARGET_DIR" ] && [ "$TARGET_DIR" != "$SCRIPT_DIR" ]; then
    print_info "Processing codebase at: $TARGET_DIR"
    print_info "This may take a few minutes..."

    # Run with timeout to prevent hanging
    timeout 300 dotnet run --project SourceCodeSummariser.csproj -- "$TARGET_DIR" || {
        print_warning "Initial processing timed out or failed. You can run it manually later."
    }

    if [ -f "summaries.db" ]; then
        print_success "Database created and populated"
    else
        print_warning "Database not created. Run 'dotnet run <path>' to process your codebase."
    fi
else
    print_info "Skipping initial codebase processing"
    print_info "Run 'dotnet run <path-to-codebase>' to process your code"
fi

# Configure Claude Desktop
print_header "Configuring Claude Desktop"

# Detect OS
if [[ "$OSTYPE" == "darwin"* ]]; then
    CLAUDE_CONFIG_DIR="$HOME/Library/Application Support/Claude"
elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
    CLAUDE_CONFIG_DIR="$HOME/.config/Claude"
else
    print_warning "Could not detect OS for Claude Desktop config"
    CLAUDE_CONFIG_DIR=""
fi

if [ -n "$CLAUDE_CONFIG_DIR" ]; then
    CLAUDE_CONFIG_FILE="$CLAUDE_CONFIG_DIR/claude_desktop_config.json"

    # Create directory if it doesn't exist
    mkdir -p "$CLAUDE_CONFIG_DIR"

    # Create or update config
    MCP_SERVER_PATH="$SCRIPT_DIR/mcp-server/dist/index.js"

    if [ -f "$CLAUDE_CONFIG_FILE" ]; then
        print_info "Claude Desktop config already exists"
        print_info "Please manually add the MCP server configuration:"
        echo ""
        cat <<EOF
{
  "mcpServers": {
    "source-code-summariser": {
      "command": "node",
      "args": ["$MCP_SERVER_PATH"],
      "env": {
        "SUMMARISER_API_URL": "http://localhost:5000"
      }
    }
  }
}
EOF
        echo ""
    else
        cat > "$CLAUDE_CONFIG_FILE" <<EOF
{
  "mcpServers": {
    "source-code-summariser": {
      "command": "node",
      "args": ["$MCP_SERVER_PATH"],
      "env": {
        "SUMMARISER_API_URL": "http://localhost:5000"
      }
    }
  }
}
EOF
        print_success "Created Claude Desktop configuration"
    fi

    print_info "Claude Desktop config location: $CLAUDE_CONFIG_FILE"
else
    print_warning "Could not configure Claude Desktop automatically"
    print_info "Please create the config file manually. See mcp-server/README.md"
fi

# Create convenience scripts
print_header "Creating Convenience Scripts"

# Start API server script
cat > start-api.sh <<'EOF'
#!/bin/bash
cd "$(dirname "$0")"
echo "Starting Source Code Summariser API Server..."
echo "API will be available at: http://localhost:5000"
echo "Press Ctrl+C to stop"
echo ""
dotnet run -- api
EOF
chmod +x start-api.sh
print_success "Created start-api.sh"

# Process codebase script
cat > process-code.sh <<'EOF'
#!/bin/bash
cd "$(dirname "$0")"

if [ -z "$1" ]; then
    echo "Usage: ./process-code.sh <path-to-codebase>"
    exit 1
fi

echo "Processing codebase at: $1"
dotnet run -- "$1"
EOF
chmod +x process-code.sh
print_success "Created process-code.sh"

# Watch mode script
cat > watch-code.sh <<'EOF'
#!/bin/bash
cd "$(dirname "$0")"

if [ -z "$1" ]; then
    echo "Usage: ./watch-code.sh <path-to-codebase>"
    exit 1
fi

echo "Starting watch mode for: $1"
echo "Press Ctrl+C to stop"
dotnet run -- "$1" --watch
EOF
chmod +x watch-code.sh
print_success "Created watch-code.sh"

# Run tests script
cat > run-tests.sh <<'EOF'
#!/bin/bash
cd "$(dirname "$0")"

echo "Running C# tests..."
dotnet test

echo ""
echo "Running TypeScript tests..."
cd mcp-server
npm test
EOF
chmod +x run-tests.sh
print_success "Created run-tests.sh"

# Complete setup summary
print_header "Setup Complete! 🎉"

echo ""
print_success "All components have been set up successfully!"
echo ""

print_info "Next Steps:"
echo ""
echo "1. Start the API server:"
echo "   ${GREEN}./start-api.sh${NC}"
echo ""
echo "2. In another terminal, open Claude Desktop"
echo "   The MCP tools should be automatically available"
echo ""
echo "3. Try asking Claude:"
echo "   - 'Search for authentication logic in the codebase'"
echo "   - 'Find all public async methods'"
echo "   - 'Show me similar implementations'"
echo ""

print_info "Useful Commands:"
echo ""
echo "Process your codebase:"
echo "   ${GREEN}./process-code.sh /path/to/your/code${NC}"
echo ""
echo "Watch mode (auto-update on changes):"
echo "   ${GREEN}./watch-code.sh /path/to/your/code${NC}"
echo ""
echo "Run tests:"
echo "   ${GREEN}./run-tests.sh${NC}"
echo ""

print_info "Configuration Files:"
echo "   - appsettings.json (main config)"
echo "   - $CLAUDE_CONFIG_FILE"
echo ""

print_info "Documentation:"
echo "   - README.md (main documentation)"
echo "   - mcp-server/README.md (MCP server guide)"
echo "   - FEATURES.md (complete feature list)"
echo "   - SourceCodeSummariser.Tests/README.md (testing guide)"
echo ""

if [ "$PROVIDER" == "local" ]; then
    print_warning "Remember to start Ollama before using:"
    echo "   ${GREEN}ollama serve${NC}"
    echo ""
fi

print_info "Need help? Check the documentation or run with --help"
echo ""

print_success "Happy coding! 🚀"
