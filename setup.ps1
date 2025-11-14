# Source Code Summariser - Setup Script
# For Windows (PowerShell)
# This script sets up the complete environment for MCP integration

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Blue
}

function Write-Success {
    param([string]$Message)
    Write-Host "[SUCCESS] $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "[WARNING] $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Write-Header {
    param([string]$Message)
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Blue
    Write-Host $Message -ForegroundColor Blue
    Write-Host "========================================" -ForegroundColor Blue
    Write-Host ""
}

# Get script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

Write-Header "Source Code Summariser - Setup"

# Check prerequisites
Write-Info "Checking prerequisites..."

# Check .NET 8.0
try {
    $dotnetVersion = dotnet --version
    Write-Success ".NET SDK found: $dotnetVersion"
} catch {
    Write-Error ".NET SDK not found!"
    Write-Info "Please install .NET 8.0 SDK from: https://dotnet.microsoft.com/download"
    exit 1
}

# Check Node.js
try {
    $nodeVersion = node --version
    Write-Success "Node.js found: $nodeVersion"
} catch {
    Write-Error "Node.js not found!"
    Write-Info "Please install Node.js 18+ from: https://nodejs.org/"
    exit 1
}

# Check npm
try {
    $npmVersion = npm --version
    Write-Success "npm found: $npmVersion"
} catch {
    Write-Error "npm not found!"
    exit 1
}

# Configuration
Write-Header "Configuration"

# Ask for OpenAI API key
Write-Host ""
$OpenAIApiKey = Read-Host "Enter your OpenAI API Key (or press Enter to skip)"
Write-Host ""

# Ask for target directory
$TargetDir = Read-Host "Enter the path to your C# codebase to analyze (default: current directory)"
if ([string]::IsNullOrWhiteSpace($TargetDir)) {
    $TargetDir = $ScriptDir
}

# Ask for provider
Write-Host ""
Write-Host "Select LLM Provider:"
Write-Host "1) OpenAI (requires API key)"
Write-Host "2) Local (Ollama - free, requires Ollama installed)"
Write-Host "3) LangChain"
$ProviderChoice = Read-Host "Enter choice (1-3, default: 1)"
if ([string]::IsNullOrWhiteSpace($ProviderChoice)) {
    $ProviderChoice = "1"
}

switch ($ProviderChoice) {
    "1" {
        $Provider = "openai"
        if ([string]::IsNullOrWhiteSpace($OpenAIApiKey)) {
            Write-Error "OpenAI API key is required for OpenAI provider!"
            exit 1
        }
    }
    "2" {
        $Provider = "local"
        Write-Info "Using local Ollama provider"
        try {
            ollama --version | Out-Null
        } catch {
            Write-Warning "Ollama not found. Install from: https://ollama.ai/"
        }
    }
    "3" {
        $Provider = "langchain"
        if ([string]::IsNullOrWhiteSpace($OpenAIApiKey)) {
            Write-Error "API key is required for LangChain provider!"
            exit 1
        }
    }
    default {
        Write-Error "Invalid choice!"
        exit 1
    }
}

# Create appsettings.json
Write-Header "Creating Configuration Files"

$TargetDirJson = $TargetDir -replace '\\', '\\'

$appsettings = @"
{
  "TargetDirectory": "$TargetDirJson",
  "LlmProvider": {
    "Provider": "$Provider",
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
"@

$appsettings | Out-File -FilePath "appsettings.json" -Encoding UTF8
Write-Success "Created appsettings.json"

# Set environment variable for API key
if (-not [string]::IsNullOrWhiteSpace($OpenAIApiKey)) {
    $env:OPENAI_API_KEY = $OpenAIApiKey

    # Set system environment variable
    try {
        [Environment]::SetEnvironmentVariable("OPENAI_API_KEY", $OpenAIApiKey, "User")
        Write-Success "Set OPENAI_API_KEY environment variable"
    } catch {
        Write-Warning "Could not set system environment variable. Set manually if needed."
    }
}

# Build C# project
Write-Header "Building C# Project"

Write-Info "Restoring NuGet packages..."
dotnet restore

Write-Info "Building project..."
dotnet build --configuration Release

Write-Success "C# project built successfully"

# Setup MCP server
Write-Header "Setting up MCP Server"

Set-Location mcp-server

Write-Info "Installing npm dependencies..."
npm install

Write-Info "Building TypeScript..."
npm run build

Set-Location ..

Write-Success "MCP server setup complete"

# Initialize database
Write-Header "Initializing Database"

if ((Test-Path $TargetDir) -and ($TargetDir -ne $ScriptDir)) {
    Write-Info "Processing codebase at: $TargetDir"
    Write-Info "This may take a few minutes..."

    try {
        $processJob = Start-Job -ScriptBlock {
            param($dir, $scriptDir)
            Set-Location $scriptDir
            dotnet run --project SourceCodeSummariser.csproj -- $dir
        } -ArgumentList $TargetDir, $ScriptDir

        $processJob | Wait-Job -Timeout 300 | Out-Null

        if ($processJob.State -eq "Running") {
            Stop-Job $processJob
            Write-Warning "Initial processing timed out. You can run it manually later."
        }

        Remove-Job $processJob
    } catch {
        Write-Warning "Initial processing failed. Run manually: dotnet run <path>"
    }

    if (Test-Path "summaries.db") {
        Write-Success "Database created and populated"
    } else {
        Write-Warning "Database not created. Run 'dotnet run <path>' to process your codebase."
    }
} else {
    Write-Info "Skipping initial codebase processing"
    Write-Info "Run 'dotnet run <path-to-codebase>' to process your code"
}

# Configure Claude Desktop
Write-Header "Configuring Claude Desktop"

$ClaudeConfigDir = "$env:APPDATA\Claude"
$ClaudeConfigFile = "$ClaudeConfigDir\claude_desktop_config.json"

# Create directory if it doesn't exist
if (-not (Test-Path $ClaudeConfigDir)) {
    New-Item -ItemType Directory -Path $ClaudeConfigDir -Force | Out-Null
}

$McpServerPath = Join-Path $ScriptDir "mcp-server\dist\index.js"
$McpServerPathJson = $McpServerPath -replace '\\', '\\'

if (Test-Path $ClaudeConfigFile) {
    Write-Info "Claude Desktop config already exists"
    Write-Info "Please manually add the MCP server configuration:"
    Write-Host ""
    Write-Host @"
{
  "mcpServers": {
    "source-code-summariser": {
      "command": "node",
      "args": ["$McpServerPathJson"],
      "env": {
        "SUMMARISER_API_URL": "http://localhost:5000"
      }
    }
  }
}
"@
    Write-Host ""
} else {
    $claudeConfig = @"
{
  "mcpServers": {
    "source-code-summariser": {
      "command": "node",
      "args": ["$McpServerPathJson"],
      "env": {
        "SUMMARISER_API_URL": "http://localhost:5000"
      }
    }
  }
}
"@
    $claudeConfig | Out-File -FilePath $ClaudeConfigFile -Encoding UTF8
    Write-Success "Created Claude Desktop configuration"
}

Write-Info "Claude Desktop config location: $ClaudeConfigFile"

# Create convenience scripts
Write-Header "Creating Convenience Scripts"

# Start API server script
$startApiScript = @'
@echo off
cd /d "%~dp0"
echo Starting Source Code Summariser API Server...
echo API will be available at: http://localhost:5000
echo Press Ctrl+C to stop
echo.
dotnet run -- api
'@
$startApiScript | Out-File -FilePath "start-api.bat" -Encoding ASCII
Write-Success "Created start-api.bat"

# Process codebase script
$processScript = @'
@echo off
cd /d "%~dp0"

if "%~1"=="" (
    echo Usage: process-code.bat ^<path-to-codebase^>
    exit /b 1
)

echo Processing codebase at: %~1
dotnet run -- "%~1"
'@
$processScript | Out-File -FilePath "process-code.bat" -Encoding ASCII
Write-Success "Created process-code.bat"

# Watch mode script
$watchScript = @'
@echo off
cd /d "%~dp0"

if "%~1"=="" (
    echo Usage: watch-code.bat ^<path-to-codebase^>
    exit /b 1
)

echo Starting watch mode for: %~1
echo Press Ctrl+C to stop
dotnet run -- "%~1" --watch
'@
$watchScript | Out-File -FilePath "watch-code.bat" -Encoding ASCII
Write-Success "Created watch-code.bat"

# Run tests script
$testScript = @'
@echo off
cd /d "%~dp0"

echo Running C# tests...
dotnet test

echo.
echo Running TypeScript tests...
cd mcp-server
npm test
'@
$testScript | Out-File -FilePath "run-tests.bat" -Encoding ASCII
Write-Success "Created run-tests.bat"

# Complete setup summary
Write-Header "Setup Complete! 🎉"

Write-Host ""
Write-Success "All components have been set up successfully!"
Write-Host ""

Write-Info "Next Steps:"
Write-Host ""
Write-Host "1. Start the API server:" -ForegroundColor White
Write-Host "   start-api.bat" -ForegroundColor Green
Write-Host ""
Write-Host "2. In another terminal, open Claude Desktop" -ForegroundColor White
Write-Host "   The MCP tools should be automatically available" -ForegroundColor White
Write-Host ""
Write-Host "3. Try asking Claude:" -ForegroundColor White
Write-Host "   - 'Search for authentication logic in the codebase'" -ForegroundColor Gray
Write-Host "   - 'Find all public async methods'" -ForegroundColor Gray
Write-Host "   - 'Show me similar implementations'" -ForegroundColor Gray
Write-Host ""

Write-Info "Useful Commands:"
Write-Host ""
Write-Host "Process your codebase:" -ForegroundColor White
Write-Host "   process-code.bat C:\path\to\your\code" -ForegroundColor Green
Write-Host ""
Write-Host "Watch mode (auto-update on changes):" -ForegroundColor White
Write-Host "   watch-code.bat C:\path\to\your\code" -ForegroundColor Green
Write-Host ""
Write-Host "Run tests:" -ForegroundColor White
Write-Host "   run-tests.bat" -ForegroundColor Green
Write-Host ""

Write-Info "Configuration Files:"
Write-Host "   - appsettings.json (main config)"
Write-Host "   - $ClaudeConfigFile"
Write-Host ""

Write-Info "Documentation:"
Write-Host "   - README.md (main documentation)"
Write-Host "   - mcp-server\README.md (MCP server guide)"
Write-Host "   - FEATURES.md (complete feature list)"
Write-Host "   - SourceCodeSummariser.Tests\README.md (testing guide)"
Write-Host ""

if ($Provider -eq "local") {
    Write-Warning "Remember to start Ollama before using:"
    Write-Host "   ollama serve" -ForegroundColor Green
    Write-Host ""
}

Write-Info "Need help? Check the documentation or run with --help"
Write-Host ""

Write-Success "Happy coding! 🚀"
