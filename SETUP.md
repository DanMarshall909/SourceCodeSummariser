# Source Code Summariser - Complete Setup Guide

This guide covers setup for using the Source Code Summariser with AI coding assistants via MCP (Model Context Protocol).

## Table of Contents

1. [Quick Setup (Automated)](#quick-setup-automated)
2. [Manual Setup](#manual-setup)
3. [Configuration](#configuration)
4. [Claude Desktop Setup](#claude-desktop-setup)
5. [Verification](#verification)
6. [Troubleshooting](#troubleshooting)
7. [Advanced Configuration](#advanced-configuration)

---

## Quick Setup (Automated)

### Linux/macOS

```bash
# Make setup script executable
chmod +x setup.sh

# Run setup
./setup.sh
```

### Windows

```powershell
# Run in PowerShell (as Administrator recommended)
.\setup.ps1
```

The script will:
- Check prerequisites (.NET 8.0, Node.js 18+)
- Prompt for configuration (API key, codebase path, LLM provider)
- Build C# project and MCP server
- Create configuration files
- Configure Claude Desktop
- Create convenience scripts

**After setup:**
1. Run `./start-api.sh` (or `start-api.bat` on Windows)
2. Open Claude Desktop
3. Try asking: "Search for authentication logic in my codebase"

---

## Manual Setup

Follow these steps if you prefer manual setup or need more control.

### 1. Prerequisites

**Required:**
- **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download)
- **Node.js 18+** - [Download](https://nodejs.org/)
- **npm** (comes with Node.js)

**Optional:**
- **Ollama** - For free local LLM (no API key needed) - [Download](https://ollama.ai/)
- **Claude Desktop** - For MCP integration - [Download](https://claude.ai/download)

**Verify installation:**

```bash
# Check .NET
dotnet --version
# Should output: 8.0.x or higher

# Check Node.js
node --version
# Should output: v18.x or higher

# Check npm
npm --version
# Should output: 9.x or higher
```

### 2. Clone or Download Repository

```bash
git clone <repository-url>
cd SourceCodeSummariser
```

### 3. Configure Environment

#### Option A: Environment Variables (Recommended for API keys)

**Linux/macOS:**
```bash
export OPENAI_API_KEY="sk-your-api-key-here"

# Add to ~/.bashrc or ~/.zshrc for persistence
echo 'export OPENAI_API_KEY="sk-your-api-key-here"' >> ~/.bashrc
source ~/.bashrc
```

**Windows (PowerShell):**
```powershell
$env:OPENAI_API_KEY="sk-your-api-key-here"

# Set permanently
[Environment]::SetEnvironmentVariable("OPENAI_API_KEY", "sk-your-api-key-here", "User")
```

**Windows (CMD):**
```cmd
set OPENAI_API_KEY=sk-your-api-key-here

# Set permanently
setx OPENAI_API_KEY "sk-your-api-key-here"
```

#### Option B: Configuration File

Create or edit `appsettings.json`:

```json
{
  "TargetDirectory": "/path/to/your/codebase",
  "LlmProvider": {
    "Provider": "openai",
    "Model": "gpt-3.5-turbo",
    "MaxTokens": 150,
    "TimeoutSeconds": 30,
    "EnableEmbeddings": true
  },
  "Database": {
    "ConnectionString": "Data Source=summaries.db"
  },
  "Processing": {
    "FilePattern": "*.cs",
    "ExcludedFolders": ["bin", "obj", ".git", ".vs", "node_modules"],
    "MaxRetries": 3,
    "RetryDelayMilliseconds": 1000
  }
}
```

**Provider Options:**

**OpenAI** (requires API key):
```json
{
  "LlmProvider": {
    "Provider": "openai",
    "Model": "gpt-3.5-turbo"
  }
}
```

**Local (Ollama)** (free, no API key):
```json
{
  "LlmProvider": {
    "Provider": "local",
    "Model": "llama2",
    "LocalEndpoint": "http://localhost:11434"
  }
}
```

**LangChain** (supports multiple providers):
```json
{
  "LlmProvider": {
    "Provider": "langchain",
    "LangChainProvider": "openai",
    "Model": "gpt-3.5-turbo"
  }
}
```

### 4. Build C# Project

```bash
# Restore NuGet packages
dotnet restore

# Build the project
dotnet build --configuration Release

# Optional: Run tests
dotnet test
```

### 5. Setup MCP Server

```bash
# Navigate to MCP server directory
cd mcp-server

# Install dependencies
npm install

# Build TypeScript
npm run build

# Optional: Run tests
npm test

# Return to root directory
cd ..
```

### 6. Initialize Database

Process your codebase to create and populate the database:

```bash
# One-time processing
dotnet run -- /path/to/your/codebase

# Or watch mode (auto-updates on file changes)
dotnet run -- /path/to/your/codebase --watch
```

**Expected output:**
```
===========================================
  Source Code Summariser
  AI-powered C# code documentation tool
===========================================

Processing folder: /path/to/your/codebase
Using provider: OpenAI
Database: Data Source=summaries.db

Found 50 C# files to process.

  [PROCESSING] Program.cs... ✓ (3 changes detected)
  [PROCESSING] UserService.cs... ✓ (no changes)
  ...

✓ Processing completed. Summaries saved to the database.
```

This creates `summaries.db` in the current directory.

### 7. Configure Claude Desktop

#### Locate Config File

**macOS:**
```
~/Library/Application Support/Claude/claude_desktop_config.json
```

**Windows:**
```
%APPDATA%\Claude\claude_desktop_config.json
```

**Linux:**
```
~/.config/Claude/claude_desktop_config.json
```

#### Create/Edit Config File

Create the directory if it doesn't exist, then create or edit the config file:

```json
{
  "mcpServers": {
    "source-code-summariser": {
      "command": "node",
      "args": [
        "/absolute/path/to/SourceCodeSummariser/mcp-server/dist/index.js"
      ],
      "env": {
        "SUMMARISER_API_URL": "http://localhost:5000"
      }
    }
  }
}
```

**Important:**
- Use **absolute paths** (not relative)
- On Windows, use double backslashes: `"C:\\Users\\YourName\\..."`
- If you already have other MCP servers, add this one to the existing `mcpServers` object

**Example with multiple servers:**
```json
{
  "mcpServers": {
    "source-code-summariser": {
      "command": "node",
      "args": ["/path/to/SourceCodeSummariser/mcp-server/dist/index.js"],
      "env": {
        "SUMMARISER_API_URL": "http://localhost:5000"
      }
    },
    "other-mcp-server": {
      "command": "node",
      "args": ["/path/to/other/server.js"]
    }
  }
}
```

### 8. Start API Server

The MCP server requires the HTTP API to be running:

**Linux/macOS:**
```bash
# Start API server
dotnet run -- api

# Or use convenience script
./start-api.sh
```

**Windows:**
```cmd
REM Start API server
dotnet run -- api

REM Or use convenience script
start-api.bat
```

**Expected output:**
```
Starting API server on http://localhost:5000
Available endpoints:
  GET /api/search?query=...&topK=10&minSimilarity=0.7
  GET /api/search/tags?query=...&tags=tag1,tag2&topK=10&minSimilarity=0.7
  GET /api/search/similar?memberId=1&topK=10&minSimilarity=0.7
  GET /api/members/{id}
  GET /api/tags
  GET /api/files/summary?filePath=...
  GET /api/files/process?filePath=...
  GET /api/health
```

**Keep this terminal open** - the API server must be running for MCP to work.

### 9. Start Claude Desktop

1. Close Claude Desktop if it's running
2. Start Claude Desktop
3. The MCP server should automatically connect

---

## Configuration

### LLM Provider Selection

#### OpenAI (Recommended for best results)

**Pros:**
- High-quality summaries
- Fast embeddings
- Reliable API

**Cons:**
- Requires API key ($)
- Internet connection required

**Cost:** ~$0.002 per file (gpt-3.5-turbo)

**Setup:**
```json
{
  "LlmProvider": {
    "Provider": "openai",
    "Model": "gpt-3.5-turbo",
    "MaxTokens": 150,
    "EnableEmbeddings": true
  }
}
```

#### Local (Ollama) - Free

**Pros:**
- Completely free
- No API key needed
- Works offline
- Privacy (data stays local)

**Cons:**
- Requires local installation
- Slower than cloud APIs
- Lower quality summaries

**Setup:**
```bash
# Install Ollama
# macOS: brew install ollama
# Linux: curl https://ollama.ai/install.sh | sh
# Windows: Download from https://ollama.ai/

# Pull a model
ollama pull llama2

# Start Ollama server
ollama serve
```

**Config:**
```json
{
  "LlmProvider": {
    "Provider": "local",
    "Model": "llama2",
    "LocalEndpoint": "http://localhost:11434",
    "EnableEmbeddings": true
  }
}
```

#### LangChain (Multi-provider)

**Supports:**
- OpenAI
- Anthropic (Claude)
- Ollama

**Config:**
```json
{
  "LlmProvider": {
    "Provider": "langchain",
    "LangChainProvider": "openai",
    "Model": "gpt-3.5-turbo"
  }
}
```

### Embedding Configuration

Embeddings enable semantic search:

```json
{
  "LlmProvider": {
    "EnableEmbeddings": true,
    "EmbeddingModel": "text-embedding-ada-002"
  }
}
```

**Embedding models:**
- `text-embedding-ada-002` (OpenAI, 1536 dims)
- `text-embedding-3-small` (OpenAI, faster)
- `text-embedding-3-large` (OpenAI, better quality)
- `mxbai-embed-large` (Ollama, 1024 dims)
- `nomic-embed-text` (Ollama, 768 dims)

### Database Configuration

```json
{
  "Database": {
    "ConnectionString": "Data Source=summaries.db"
  }
}
```

Change location:
```json
{
  "Database": {
    "ConnectionString": "Data Source=/custom/path/summaries.db"
  }
}
```

### Processing Configuration

```json
{
  "Processing": {
    "FilePattern": "*.cs",
    "ExcludedFolders": ["bin", "obj", ".git", ".vs", "node_modules"],
    "MaxRetries": 3,
    "RetryDelayMilliseconds": 1000
  }
}
```

---

## Claude Desktop Setup

### Step-by-Step

1. **Locate config file** (see paths above)

2. **Create directory if needed:**
   ```bash
   # macOS
   mkdir -p ~/Library/Application\ Support/Claude

   # Linux
   mkdir -p ~/.config/Claude

   # Windows (PowerShell)
   New-Item -ItemType Directory -Path "$env:APPDATA\Claude" -Force
   ```

3. **Get absolute path to MCP server:**
   ```bash
   # Linux/macOS
   realpath mcp-server/dist/index.js

   # Windows (PowerShell)
   Resolve-Path mcp-server\dist\index.js
   ```

4. **Create/edit config file** with the path from step 3

5. **Restart Claude Desktop completely** (close all windows)

6. **Verify connection:**
   - Open Claude Desktop
   - Look for MCP indicator (usually in status bar)
   - Try: "List available MCP tools"

### Troubleshooting Claude Desktop

**MCP server not showing up:**
1. Check config file path is correct
2. Ensure absolute path (not relative)
3. Check Node.js is in PATH: `node --version`
4. Check MCP server is built: `ls mcp-server/dist/index.js`
5. Restart Claude Desktop completely

**Connection errors:**
1. Ensure API server is running: `curl http://localhost:5000/api/health`
2. Check firewall settings
3. Check Claude Desktop logs (varies by OS)

---

## Verification

### 1. Test API Server

```bash
# Health check
curl http://localhost:5000/api/health

# Expected: {"status":"healthy","service":"SourceCodeSummariser API","version":"1.0.0"}

# Test search
curl "http://localhost:5000/api/search?query=authentication&topK=5&minSimilarity=0.7"

# Test tags
curl http://localhost:5000/api/tags
```

### 2. Test MCP in Claude Desktop

Ask Claude:

```
"What MCP tools do you have available?"
```

Expected tools:
- search_code
- search_code_with_tags
- find_similar_code
- get_member_details
- list_tags
- get_file_summary
- process_file

### 3. Test Semantic Search

Ask Claude:

```
"Search for authentication logic in my codebase"
```

Or:

```
"Find all public async methods"
```

Claude should use the `search_code` or `search_code_with_tags` tool and return results.

---

## Troubleshooting

### Common Issues

#### "OpenAI API key not configured"

**Solution:**
```bash
# Linux/macOS
export OPENAI_API_KEY="sk-your-key"

# Windows
$env:OPENAI_API_KEY="sk-your-key"
```

#### "Failed to connect to Summariser API"

**Check if API server is running:**
```bash
curl http://localhost:5000/api/health
```

**If not running, start it:**
```bash
dotnet run -- api
```

#### "Database not found"

**Run initial processing:**
```bash
dotnet run -- /path/to/your/codebase
```

#### "No search results"

**Possible causes:**
1. Database is empty → Process your codebase first
2. Embeddings not enabled → Check `EnableEmbeddings: true` in config
3. Similarity threshold too high → Try `minSimilarity: 0.5`

#### Port 5000 already in use

**Change port in ApiServer.cs:**
```csharp
await app.RunAsync("http://localhost:5001");  // Change to 5001
```

**Update MCP config:**
```json
{
  "env": {
    "SUMMARISER_API_URL": "http://localhost:5001"
  }
}
```

### Logs and Diagnostics

**Check API server logs:**
- Shown in terminal where you ran `dotnet run -- api`

**Check Claude Desktop logs:**
- macOS: `~/Library/Logs/Claude/`
- Windows: `%APPDATA%\Claude\logs\`
- Linux: `~/.config/Claude/logs/`

**Enable verbose logging (C#):**
Add to `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

---

## Advanced Configuration

### Running on Different Port

Change port in `ApiServer.cs`:
```csharp
await app.RunAsync("http://localhost:5001");
```

Update environment variable:
```bash
export SUMMARISER_API_URL="http://localhost:5001"
```

### Using HTTPS

Add to `ApiServer.cs`:
```csharp
builder.WebHost.UseUrls("https://localhost:5001");
```

Configure certificate in `appsettings.json`.

### Running API as Service

**Linux (systemd):**
Create `/etc/systemd/system/summariser-api.service`:
```ini
[Unit]
Description=Source Code Summariser API
After=network.target

[Service]
Type=simple
User=youruser
WorkingDirectory=/path/to/SourceCodeSummariser
ExecStart=/usr/bin/dotnet run -- api
Restart=on-failure

[Install]
WantedBy=multi-user.target
```

Start service:
```bash
sudo systemctl start summariser-api
sudo systemctl enable summariser-api
```

**Windows (NSSM):**
1. Download NSSM
2. Run: `nssm install SummariserAPI`
3. Set path to `dotnet.exe`
4. Set arguments: `run -- api`
5. Set working directory

### Docker Setup

Create `Dockerfile`:
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY . .
RUN dotnet restore
RUN dotnet build --configuration Release

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 5000
CMD ["dotnet", "run", "--", "api"]
```

Build and run:
```bash
docker build -t summariser-api .
docker run -p 5000:5000 -e OPENAI_API_KEY=your-key summariser-api
```

### CI/CD Integration

**GitHub Actions:**
```yaml
name: Process Codebase

on:
  push:
    branches: [main]

jobs:
  summarize:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 8.0.x
      - run: dotnet restore
      - run: dotnet build
      - run: dotnet run -- .
        env:
          OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}
```

---

## Next Steps

After setup:

1. **Process your codebase regularly**
   ```bash
   ./process-code.sh /path/to/code
   ```

2. **Use watch mode during development**
   ```bash
   ./watch-code.sh /path/to/code
   ```

3. **Explore MCP tools in Claude Desktop**
   - Search for code
   - Find similar implementations
   - Get file summaries

4. **Integrate with your workflow**
   - Add to CI/CD
   - Schedule regular updates
   - Share database with team

---

## Support

- **Documentation**: See README.md, FEATURES.md
- **Issues**: Report at GitHub repository
- **Community**: Join discussions

Happy coding! 🚀
