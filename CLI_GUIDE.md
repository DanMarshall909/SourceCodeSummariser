# SourceCode Analyzer CLI Guide

A high-quality command-line interface built with Spectre.Console for the SourceCode Analyzer. This CLI provides a clean, intuitive way to analyze code, search semantically, and manage your code documentation database.

## Architecture

The CLI follows a **client-server architecture**:

- **Analyzer Service**: Runs as a background service (HTTP API) that handles the heavy lifting of code analysis, AI summarization, and semantic search
- **CLI Client**: Lightweight commands that invoke features on the service via HTTP requests
- **Local Processing**: Also supports direct local processing without requiring the service

This design allows the CLI to be used both by humans and other tools/scripts.

## Installation

```bash
# Build the project
dotnet build

# Optionally, install as a global tool
dotnet pack
dotnet tool install --global --add-source ./nupkg SourceCodeSummariser
```

## Quick Start

1. **Initialize the tool**:
   ```bash
   analyze init
   ```
   This creates the configuration file and database.

2. **Edit configuration**:
   ```bash
   # Edit appsettings.json and add your API key
   nano appsettings.json
   ```

3. **Process your code**:
   ```bash
   analyze process /path/to/your/code
   ```

4. **Start the analyzer service**:
   ```bash
   analyze serve
   ```

5. **Search your code**:
   ```bash
   analyze search "authentication logic"
   ```

## Commands

### `init` - Initialize the Analyzer

Creates configuration files and sets up the database.

**Usage:**
```bash
analyze init [options]
```

**Options:**
- `-d, --directory <path>`: Target directory for the database (default: current directory)
- `-f, --force`: Force overwrite of existing configuration

**Examples:**
```bash
# Initialize with default settings
analyze init

# Initialize with specific directory
analyze init --directory /path/to/project

# Force reinitialize (overwrite config)
analyze init --force
```

---

### `serve` - Start the Analyzer Service

Starts the HTTP API service that handles code analysis and search operations.

**Usage:**
```bash
analyze serve [options]
```

**Options:**
- `-p, --port <port>`: Port to run the service on (default: 5000)
- `-c, --config <path>`: Configuration file path (default: appsettings.json)

**Examples:**
```bash
# Start service on default port (5000)
analyze serve

# Start on custom port
analyze serve --port 8080

# Use custom configuration
analyze serve --config myconfig.json
```

**What it does:**
- Loads LLM provider configuration
- Initializes the database connection
- Starts HTTP API server
- Provides endpoints for search, analysis, and data retrieval

**Press Ctrl+C** to stop the service.

---

### `process` - Analyze Code Files

Processes code files or directories to generate AI summaries and embeddings.

**Usage:**
```bash
analyze process <path> [options]
```

**Options:**
- `-w, --watch`: Watch mode - continuously monitor for changes
- `--remote`: Use remote service instead of local processing
- `--service-url <url>`: Service URL when using --remote (default: http://localhost:5000)
- `-c, --config <path>`: Configuration file path

**Examples:**
```bash
# Process a single file (local)
analyze process Program.cs

# Process entire directory (local)
analyze process /path/to/project

# Watch mode - continuous monitoring
analyze process /path/to/project --watch

# Process via remote service
analyze process Program.cs --remote

# Use custom service URL
analyze process Program.cs --remote --service-url http://myserver:5000
```

**Processing Modes:**

1. **Local Processing**: Directly processes files using configured LLM provider
   - No service required
   - Suitable for batch processing
   - Can use watch mode

2. **Remote Processing**: Sends requests to the analyzer service
   - Service must be running (`analyze serve`)
   - Suitable for individual files
   - Integrates with automation tools

---

### `search` - Semantic Code Search

Performs semantic search across your codebase using AI embeddings.

**Usage:**
```bash
analyze search <query> [options]
```

**Options:**
- `-k, --top <n>`: Number of results to return (default: 10)
- `-s, --min-similarity <0-1>`: Minimum similarity threshold (default: 0.0)
- `-t, --tags <tags>`: Filter by tags (comma-separated)
- `-m, --similar-to <id>`: Find similar code to member ID
- `-f, --format <format>`: Output format: table, json, markdown (default: table)
- `--service-url <url>`: Service URL (default: http://localhost:5000)
- `--full`: Show full summaries and tags

**Examples:**
```bash
# Basic semantic search
analyze search "authentication logic"

# Limit results and set similarity threshold
analyze search "error handling" --top 5 --min-similarity 0.7

# Search with tag filtering
analyze search "async methods" --tags public,async

# Find code similar to a specific member
analyze search --similar-to 123

# Output as JSON
analyze search "database queries" --format json

# Show full summaries
analyze search "API endpoints" --full
```

**Search Modes:**

1. **Semantic Search**: Natural language query matching
   ```bash
   analyze search "how is user authentication handled?"
   ```

2. **Tag-Based Search**: Filter results by code attributes
   ```bash
   analyze search "validation" --tags public,static
   ```

3. **Similarity Search**: Find code similar to a known member
   ```bash
   analyze search --similar-to 456
   ```

**Output Formats:**

- **table**: Pretty-printed table (default, best for humans)
- **json**: JSON format (best for tools/scripts)
- **markdown**: Markdown format (best for documentation)

---

### `member` - Get Member Details

Retrieves detailed information about a specific code member.

**Usage:**
```bash
analyze member <id> [options]
```

**Options:**
- `-f, --format <format>`: Output format: table, json, markdown (default: table)
- `--service-url <url>`: Service URL (default: http://localhost:5000)

**Examples:**
```bash
# Get member details
analyze member 123

# Output as JSON
analyze member 456 --format json

# Output as Markdown
analyze member 789 --format markdown
```

**What it shows:**
- Member ID
- Name and type (class, method, property, etc.)
- File location
- Hash (for change detection)
- Tags
- AI-generated summary

---

### `tags` - List and Manage Tags

Lists all tags in the database with usage statistics.

**Usage:**
```bash
analyze tags [options]
```

**Options:**
- `-c, --category <category>`: Filter by category
- `-m, --min-count <n>`: Minimum usage count (default: 1)
- `-f, --format <format>`: Output format: table, json, list (default: table)
- `--service-url <url>`: Service URL (default: http://localhost:5000)

**Examples:**
```bash
# List all tags
analyze tags

# Filter by category
analyze tags --category visibility

# Show only frequently used tags
analyze tags --min-count 10

# List format (grouped by category)
analyze tags --format list

# JSON output
analyze tags --format json
```

**Tag Categories:**
- **visibility**: public, private, protected, internal
- **modifier**: static, async, virtual, abstract, sealed
- **type**: class, interface, struct, enum
- **return**: void, string, int, Task, etc.

---

### `files` - File Summaries

Gets summary information about a specific file.

**Usage:**
```bash
analyze files <file-path> [options]
```

**Options:**
- `-f, --format <format>`: Output format: table, json, markdown (default: table)
- `--service-url <url>`: Service URL (default: http://localhost:5000)

**Examples:**
```bash
# Get file summary
analyze files Program.cs

# Full path
analyze files /path/to/MyClass.cs

# Markdown output
analyze files MyService.cs --format markdown
```

**What it shows:**
- File name and path
- Total member count
- Member types present
- AI-generated file summary

---

### `stats` - Database Statistics

Shows comprehensive statistics about the analyzer database.

**Usage:**
```bash
analyze stats [options]
```

**Options:**
- `-d, --database <path>`: Database path (default: ./summaries.db)
- `-f, --format <format>`: Output format: table, json (default: table)

**Examples:**
```bash
# Show statistics for default database
analyze stats

# Use custom database
analyze stats --database /path/to/summaries.db

# JSON output
analyze stats --format json
```

**Statistics Shown:**
- Total files processed
- Total code members
- Total tags
- Members with embeddings
- Database size
- Member type breakdown
- Top files by member count

---

## Configuration

The CLI uses `appsettings.json` for configuration. Run `analyze init` to create a default configuration.

**Key Settings:**

```json
{
  "LlmProvider": {
    "Provider": "openai",           // openai, local, langchain
    "ApiKey": "your-key-here",      // API key for cloud providers
    "Model": "gpt-3.5-turbo",       // Model to use
    "MaxTokens": 50,                // Max tokens for summaries
    "EnableEmbeddings": true,       // Generate embeddings for search
    "EmbeddingModel": "text-embedding-ada-002"
  },
  "Processing": {
    "FilePattern": "*.cs",          // File pattern to process
    "ExcludedFolders": ["bin", "obj", "node_modules"]
  },
  "TargetDirectory": "/path/to/code" // Default directory for analysis
}
```

**Environment Variables:**

You can also use environment variables (they override appsettings.json):

```bash
export LlmProvider__ApiKey="your-key-here"
export LlmProvider__Model="gpt-4"
export LlmProvider__Provider="openai"
```

---

## Common Workflows

### Initial Setup and Analysis

```bash
# 1. Initialize
analyze init

# 2. Edit configuration (add API key)
nano appsettings.json

# 3. Process your codebase
analyze process /path/to/your/project

# 4. View statistics
analyze stats
```

### Daily Development with Watch Mode

```bash
# Terminal 1: Start watch mode
analyze process /path/to/project --watch

# Terminal 2: Search as you code
analyze search "new feature I'm working on"
```

### Service-Based Usage (for Tools/Automation)

```bash
# Terminal 1: Start service
analyze serve

# Terminal 2: Use the CLI
analyze search "authentication"
analyze member 123
analyze tags --category visibility

# Or use HTTP API directly
curl http://localhost:5000/api/search?query=authentication&topK=5
```

### CI/CD Integration

```bash
#!/bin/bash
# Process code in CI pipeline

# Initialize
analyze init --directory ./src

# Process all code
analyze process ./src

# Generate statistics report
analyze stats --format json > code-stats.json

# Search for potential issues
analyze search "TODO" --format json > todos.json
analyze search "FIXME" --format json > fixmes.json
```

---

## Scripting and Automation

The CLI is designed to work well with scripts and other tools:

### Bash Script Example

```bash
#!/bin/bash

# Find all authentication-related code
analyze search "authentication" --format json > auth-code.json

# Get details for each result
cat auth-code.json | jq -r '.[].Id' | while read id; do
    analyze member "$id" --format markdown >> auth-docs.md
done
```

### PowerShell Example

```powershell
# Search for async methods
$results = analyze search "async" --tags async --format json | ConvertFrom-Json

# Process results
foreach ($result in $results) {
    Write-Host "Found: $($result.Name) in $($result.FileName)"
    Write-Host "Similarity: $($result.Similarity)"
}
```

### Python Integration

```python
import subprocess
import json

def search_code(query):
    result = subprocess.run(
        ['analyze', 'search', query, '--format', 'json'],
        capture_output=True,
        text=True
    )
    return json.loads(result.stdout)

# Use it
results = search_code("error handling")
for item in results:
    print(f"{item['Name']}: {item['Summary']}")
```

---

## Output Formats

All commands support multiple output formats optimized for different use cases:

### Table Format (Default)
- Human-readable
- Pretty-printed with colors
- Best for terminal usage

### JSON Format
- Machine-readable
- Fully structured data
- Best for scripting/automation
- Can be piped to `jq` for processing

### Markdown Format
- Documentation-friendly
- Can be directly used in docs
- Best for generating reports

---

## Tips and Best Practices

1. **Use Watch Mode During Development**
   ```bash
   analyze process . --watch
   ```
   This keeps your database up-to-date as you code.

2. **Filter Search Results with Tags**
   ```bash
   analyze search "validation" --tags public --min-similarity 0.7
   ```
   Improves search precision by combining semantic search with tag filtering.

3. **Export Data for Reporting**
   ```bash
   analyze stats --format json > stats.json
   analyze search "API" --format markdown > api-docs.md
   ```

4. **Run Service in Background**
   ```bash
   # Linux/macOS
   nohup analyze serve > server.log 2>&1 &

   # Or use systemd, docker, etc.
   ```

5. **Use Remote Mode for CI/CD**
   - Run service once: `analyze serve`
   - Process files remotely: `analyze process file.cs --remote`
   - Faster for individual files, service handles state

---

## Troubleshooting

### Service Not Running

```bash
Error: Analyzer service is not running
Start the service with: analyze serve
```

**Solution**: Start the service in another terminal:
```bash
analyze serve
```

### Configuration Not Found

```bash
Error: Configuration file not found: appsettings.json
Run 'analyze init' to create a default configuration.
```

**Solution**: Initialize the tool:
```bash
analyze init
```

### API Key Not Configured

```bash
Error: OpenAI API key not configured
```

**Solution**: Edit `appsettings.json` or set environment variable:
```bash
export LlmProvider__ApiKey="your-key-here"
```

### Database Not Found

```bash
Warning: Database not found
Run 'analyze process <path>' to populate the database.
```

**Solution**: Process some code first:
```bash
analyze process /path/to/code
```

---

## Advanced Usage

### Custom Service URL

If running the service on a different machine:

```bash
# On server
analyze serve --port 5000

# On client
analyze search "query" --service-url http://server:5000
analyze member 123 --service-url http://server:5000
```

### Multiple Databases

```bash
# Process different projects to different databases
analyze process /project1 --config project1.json
analyze process /project2 --config project2.json

# Query specific database
analyze stats --database /project1/summaries.db
analyze stats --database /project2/summaries.db
```

### Custom LLM Providers

```json
{
  "LlmProvider": {
    "Provider": "local",              // Use local Ollama
    "LocalEndpoint": "http://localhost:11434",
    "Model": "codellama",
    "EnableEmbeddings": true,
    "EmbeddingModel": "mxbai-embed-large"
  }
}
```

---

## Integration Examples

### VSCode Task

Add to `.vscode/tasks.json`:

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "Analyze Code",
      "type": "shell",
      "command": "analyze process ${workspaceFolder}",
      "problemMatcher": []
    },
    {
      "label": "Start Analyzer Service",
      "type": "shell",
      "command": "analyze serve",
      "isBackground": true
    }
  ]
}
```

### Git Hook (Pre-commit)

```bash
#!/bin/sh
# .git/hooks/pre-commit

# Update analysis for changed files
git diff --cached --name-only --diff-filter=ACM | grep '\.cs$' | while read file; do
    analyze process "$file" --remote 2>/dev/null || true
done
```

---

## Performance Tips

1. **Enable Embeddings Only When Needed**
   - Embeddings enable semantic search but slow down processing
   - Disable if you only need summaries: `"EnableEmbeddings": false`

2. **Use Appropriate Models**
   - Faster: `gpt-3.5-turbo` or local models
   - Better quality: `gpt-4` or `gpt-4-turbo`

3. **Exclude Unnecessary Folders**
   ```json
   "ExcludedFolders": ["bin", "obj", "node_modules", ".git", "packages"]
   ```

4. **Process Incrementally**
   - Use watch mode during development
   - Only process changed files in CI/CD

---

## Contributing

The CLI is built with Spectre.Console and follows these patterns:

- Commands in `CLI/Commands/`
- Each command is a separate class inheriting from `AsyncCommand<TSettings>`
- ApiClient handles all HTTP communication
- Consistent error handling and user feedback
- Support for multiple output formats

To add a new command:

1. Create a new class in `CLI/Commands/`
2. Inherit from `AsyncCommand<YourCommand.Settings>`
3. Implement `ExecuteAsync` method
4. Register in `Program.cs` configuration

---

## License

See LICENSE file in the repository.

---

## Support

For issues, questions, or feature requests, please open an issue on GitHub.
