# Source Code Summariser MCP Server

Model Context Protocol (MCP) server that enables AI coding bots to interact with the Source Code Summariser for semantic code search and analysis.

## What is MCP?

The Model Context Protocol (MCP) is a standard protocol that enables AI assistants to interact with external tools and data sources. This MCP server exposes the Source Code Summariser's semantic search capabilities to AI coding bots like Claude Desktop, making it easy to find and understand code in large codebases.

## Features

This MCP server provides the following tools to AI coding bots:

- **search_code** - Perform semantic search across the codebase
- **search_code_with_tags** - Search with tag filtering
- **find_similar_code** - Find similar code members
- **get_member_details** - Get detailed information about a code member
- **list_tags** - List all available tags
- **get_file_summary** - Get comprehensive file documentation
- **process_file** - Process and summarize new/updated files

## Prerequisites

1. **Node.js 18+** - Required to run the MCP server
2. **.NET 8.0** - Required for the C# API backend
3. **Populated database** - Run the summarizer first to analyze your codebase

## Installation

### 1. Build the MCP Server

```bash
cd mcp-server
npm install
npm run build
```

### 2. Configure Your Environment

Create an `appsettings.json` in the root directory:

```json
{
  "TargetDirectory": "/path/to/your/codebase",
  "LlmProvider": {
    "Provider": "openai",
    "ApiKey": "your-api-key-here",
    "Model": "gpt-4",
    "MaxTokens": 150,
    "EnableEmbeddings": true
  }
}
```

Or use environment variables:
```bash
export OPENAI_API_KEY=your-api-key-here
```

### 3. Initial Setup - Process Your Codebase

Before using the MCP server, you need to analyze your codebase:

```bash
# One-time processing
dotnet run -- /path/to/your/codebase

# Or watch mode (continuous monitoring)
dotnet run -- /path/to/your/codebase --watch
```

This creates a SQLite database with AI summaries and embeddings.

## Usage

### Running the API Server

The MCP server requires the C# HTTP API to be running:

```bash
# In terminal 1 - Start the API server
dotnet run -- api
```

The API server will start on `http://localhost:5000`.

### Configure Claude Desktop

Add this to your Claude Desktop configuration file:

**macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
**Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
**Linux**: `~/.config/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "source-code-summariser": {
      "command": "node",
      "args": ["/absolute/path/to/SourceCodeSummariser/mcp-server/dist/index.js"],
      "env": {
        "SUMMARISER_API_URL": "http://localhost:5000"
      }
    }
  }
}
```

**Important**: Replace `/absolute/path/to/SourceCodeSummariser` with the actual path.

### Using with Other MCP Clients

Any MCP-compatible client can use this server. Configure it with:

- **Command**: `node`
- **Args**: `["/path/to/mcp-server/dist/index.js"]`
- **Environment**: `SUMMARISER_API_URL=http://localhost:5000`

## Example Usage in Claude Desktop

Once configured, you can ask Claude:

```
"Search for authentication logic in the codebase"
"Find all public async methods"
"Show me code similar to the UserService class"
"What does member ID 42 do?"
"List all available tags"
```

Claude will automatically use the MCP tools to search your codebase semantically.

## Tool Descriptions

### search_code

Perform semantic search to find relevant code:

```json
{
  "query": "authentication logic",
  "topK": 10,
  "minSimilarity": 0.7
}
```

### search_code_with_tags

Combine semantic search with tag filtering:

```json
{
  "query": "validation",
  "tags": ["public", "async"],
  "topK": 10,
  "minSimilarity": 0.7
}
```

### find_similar_code

Find code similar to a specific member:

```json
{
  "memberId": 42,
  "topK": 10,
  "minSimilarity": 0.7
}
```

### get_member_details

Get detailed information about a code member:

```json
{
  "memberId": 42
}
```

### list_tags

List all available tags (no parameters required).

### get_file_summary

Get all summaries for a file:

```json
{
  "filePath": "Services/AuthService.cs"
}
```

### process_file

Process and summarize a new file:

```json
{
  "filePath": "/absolute/path/to/NewFile.cs"
}
```

## API Endpoints

The C# API server exposes these REST endpoints:

- `GET /api/search?query=...&topK=10&minSimilarity=0.7`
- `GET /api/search/tags?query=...&tags=tag1,tag2&topK=10&minSimilarity=0.7`
- `GET /api/search/similar?memberId=1&topK=10&minSimilarity=0.7`
- `GET /api/members/{id}`
- `GET /api/tags`
- `GET /api/files/summary?filePath=...`
- `GET /api/files/process?filePath=...`
- `GET /api/health`

## Development

### Build and Watch

```bash
npm run watch
```

### Testing the API Server

```bash
# Health check
curl http://localhost:5000/api/health

# Search example
curl "http://localhost:5000/api/search?query=authentication&topK=5&minSimilarity=0.7"

# List tags
curl http://localhost:5000/api/tags
```

## Troubleshooting

### "Failed to connect to Summariser API"

Ensure the API server is running:
```bash
dotnet run -- api
```

### "Database not found"

Run the summarizer first to create the database:
```bash
dotnet run -- /path/to/your/codebase
```

### MCP Server Not Appearing in Claude Desktop

1. Check that the path in `claude_desktop_config.json` is absolute and correct
2. Ensure the MCP server is built (`npm run build`)
3. Restart Claude Desktop
4. Check Claude Desktop logs for errors

### No Search Results

1. Verify embeddings are enabled in `appsettings.json`
2. Ensure the database is populated with summaries
3. Try lowering the `minSimilarity` threshold (e.g., 0.5)

## Architecture

```
┌─────────────────┐
│  Claude Desktop │
│   (MCP Client)  │
└────────┬────────┘
         │ MCP Protocol (stdio)
         │
┌────────▼────────┐
│  MCP Server     │
│  (TypeScript)   │
└────────┬────────┘
         │ HTTP REST
         │
┌────────▼────────┐
│  C# API Server  │
│  (ASP.NET Core) │
└────────┬────────┘
         │
┌────────▼────────┐
│ SQLite Database │
│  (Embeddings)   │
└─────────────────┘
```

## License

MIT
