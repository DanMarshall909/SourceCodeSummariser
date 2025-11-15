# Quick Start Guide (5 Minutes)

Get up and running with Source Code Summariser + Claude Desktop in 5 minutes.

## Prerequisites

✅ .NET 8.0 SDK - [Download](https://dotnet.microsoft.com/download)
✅ Node.js 18+ - [Download](https://nodejs.org/)
✅ Claude Desktop - [Download](https://claude.ai/download)
✅ OpenAI API Key - [Get one](https://platform.openai.com/api-keys)

## Setup (One Time)

### 1. Run Setup Script

**Linux/macOS:**
```bash
chmod +x setup.sh && ./setup.sh
```

**Windows (PowerShell):**
```powershell
.\setup.ps1
```

When prompted:
- Enter your OpenAI API key
- Enter path to your C# codebase (or press Enter for current directory)
- Select provider (1 for OpenAI)

### 2. Restart Claude Desktop

Close and reopen Claude Desktop completely.

## Daily Usage

### Start API Server

**Linux/macOS:**
```bash
./start-api.sh
```

**Windows:**
```cmd
start-api.bat
```

Keep this terminal open.

### Use in Claude Desktop

Ask Claude:

```
"Search for authentication logic in my codebase"
```

```
"Find all public async methods"
```

```
"Show me similar implementations to this code"
```

That's it! 🎉

---

## Common Commands

### Process New Codebase

**Linux/macOS:**
```bash
./process-code.sh /path/to/codebase
```

**Windows:**
```cmd
process-code.bat C:\path\to\codebase
```

### Watch Mode (Auto-Update)

**Linux/macOS:**
```bash
./watch-code.sh /path/to/codebase
```

**Windows:**
```cmd
watch-code.bat C:\path\to\codebase
```

### Run Tests

**Linux/macOS:**
```bash
./run-tests.sh
```

**Windows:**
```cmd
run-tests.bat
```

---

## Troubleshooting

### "MCP server not showing up in Claude"

1. Check API server is running (should see "Starting API server...")
2. Restart Claude Desktop completely
3. Check config file was created correctly:
   - macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`
   - Windows: `%APPDATA%\Claude\claude_desktop_config.json`
   - Linux: `~/.config/Claude/claude_desktop_config.json`

### "Failed to connect to Summariser API"

Make sure API server is running:
```bash
# Linux/macOS
./start-api.sh

# Windows
start-api.bat
```

### "No search results"

Process your codebase first:
```bash
# Linux/macOS
./process-code.sh /path/to/your/code

# Windows
process-code.bat C:\path\to\your\code
```

---

## What's Happening?

1. **API Server** runs on http://localhost:5000
2. **MCP Server** (Node.js) connects Claude Desktop to the API
3. **Claude** uses MCP tools to search your code semantically
4. **Database** (SQLite) stores summaries and embeddings

```
Claude Desktop ─MCP─> Node.js Server ─HTTP─> .NET API ─> SQLite DB
```

---

## Next Steps

- Read [SETUP.md](SETUP.md) for detailed configuration
- Read [FEATURES.md](FEATURES.md) for complete feature list
- Read [README.md](README.md) for full documentation

---

## Example Queries

### Search by Functionality
```
"Search for authentication logic"
"Find database connection code"
"Show me validation methods"
```

### Search by Signature
```
"Find all public async methods"
"Show me methods that return Task<User>"
"Find private static methods"
```

### Find Similar Code
```
"Find code similar to the UserService class"
"Show me duplicate implementations"
"Find related methods"
```

### Explore by File
```
"What does AuthService.cs do?"
"Show me all methods in UserController.cs"
```

---

## Need More Help?

- Full setup guide: [SETUP.md](SETUP.md)
- Feature list: [FEATURES.md](FEATURES.md)
- MCP server docs: [mcp-server/README.md](mcp-server/README.md)
- Test docs: [SourceCodeSummariser.Tests/README.md](SourceCodeSummariser.Tests/README.md)

Happy coding! 🚀
