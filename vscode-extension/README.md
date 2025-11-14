# SourceCode Summariser - VSCode Extension

AI-powered code documentation and semantic search for C# projects right in your editor.

## Features

### 🤖 AI-Powered Code Analysis
Automatically generate intelligent summaries of your C# codebase using OpenAI GPT, Anthropic Claude, or local LLMs.

### 🔍 Semantic Search
Find code by meaning, not just keywords. Search for "methods that handle authentication" or "classes for data validation" and get relevant results ranked by similarity.

### 📊 Code Summaries Tree View
Browse all code summaries in a convenient tree view organized by file. Click any member to see detailed information.

### 💡 Hover Summaries
See AI-generated summaries when you hover over classes, methods, properties, and other code members.

### 👁️ Watch Mode
Automatically monitor your codebase and update summaries when files change.

### 🎯 Context Menu Integration
Right-click on C# files to analyze them directly from the explorer or editor.

## Commands

Access these commands from the Command Palette (`Ctrl+Shift+P` or `Cmd+Shift+P`):

- **SourceCode Summariser: Analyze Workspace** - Analyze all C# files in your workspace
- **SourceCode Summariser: Analyze Current File** - Analyze the currently open file
- **SourceCode Summariser: Semantic Search** - Search your codebase semantically
- **SourceCode Summariser: Show Summaries** - Open the summaries tree view
- **SourceCode Summariser: Toggle Watch Mode** - Enable/disable automatic monitoring
- **SourceCode Summariser: Initialize Project** - Initialize a new project with configuration

## Getting Started

### Prerequisites

1. The SourceCodeSummariser .NET tool must be built and available
2. An API key for your chosen LLM provider (OpenAI, Anthropic, or local Ollama)

### Installation

1. Install the extension from the VSCode marketplace or from VSIX
2. Open a C# workspace
3. Configure your LLM provider settings (see Configuration below)
4. Run "SourceCode Summariser: Initialize Project" to set up configuration
5. Run "SourceCode Summariser: Analyze Workspace" to generate summaries

### Quick Start

1. Open a C# project in VSCode
2. Press `Ctrl+Shift+P` and run "SourceCode Summariser: Analyze Workspace"
3. Wait for analysis to complete
4. Click the book icon in the activity bar to view summaries
5. Hover over any code member to see its AI-generated summary

## Configuration

Configure the extension in VSCode settings (`File > Preferences > Settings` or `Code > Preferences > Settings`):

### LLM Provider Settings

```json
{
  "sourcecodeSummariser.llmProvider": "OpenAI",  // OpenAI, Anthropic, or Local
  "sourcecodeSummariser.model": "gpt-3.5-turbo",
  "sourcecodeSummariser.maxTokens": 150,
  "sourcecodeSummariser.enableEmbeddings": true,
  "sourcecodeSummariser.embeddingModel": "text-embedding-ada-002"
}
```

### Path Settings

```json
{
  "sourcecodeSummariser.toolPath": "",  // Leave empty to auto-detect
  "sourcecodeSummariser.databasePath": "summaries.db"
}
```

### Analysis Settings

```json
{
  "sourcecodeSummariser.excludedFolders": ["bin", "obj", ".git", ".vs", "node_modules"],
  "sourcecodeSummariser.autoWatch": false,
  "sourcecodeSummariser.showHoverSummaries": true
}
```

### Environment Variables

Set your API keys as environment variables:

```bash
# For OpenAI
export OPENAI_API_KEY="your-key-here"

# For Anthropic
export ANTHROPIC_API_KEY="your-key-here"
```

Or add them to your workspace settings (not recommended for shared repositories):

```json
{
  "terminal.integrated.env.linux": {
    "OPENAI_API_KEY": "your-key-here"
  },
  "terminal.integrated.env.osx": {
    "OPENAI_API_KEY": "your-key-here"
  },
  "terminal.integrated.env.windows": {
    "OPENAI_API_KEY": "your-key-here"
  }
}
```

## Usage

### Analyzing Code

**Analyze entire workspace:**
1. Open Command Palette (`Ctrl+Shift+P`)
2. Run "SourceCode Summariser: Analyze Workspace"
3. Wait for analysis to complete

**Analyze single file:**
1. Open a C# file
2. Right-click in the editor
3. Select "SourceCode Summariser: Analyze Current File"

### Viewing Summaries

**Tree View:**
1. Click the book icon in the activity bar
2. Expand files to see their members
3. Click any member to see detailed summary

**Hover:**
1. Hover your mouse over any class, method, or property name
2. See the AI-generated summary in a tooltip

### Semantic Search

1. Open Command Palette (`Ctrl+Shift+P`)
2. Run "SourceCode Summariser: Semantic Search"
3. Enter your query (e.g., "authentication methods")
4. Specify number of results (default: 10)
5. View results in a new panel with similarity scores

### Watch Mode

**Enable watch mode** to automatically update summaries when files change:

1. Open Command Palette (`Ctrl+Shift+P`)
2. Run "SourceCode Summariser: Toggle Watch Mode"
3. Make changes to your C# files
4. Summaries will update automatically

To stop watching, run the toggle command again.

## Features in Detail

### Tree View

The summaries tree view shows:
- Statistics (total files and members)
- Files organized hierarchically
- Code members grouped by file
- Icons indicating member types (method, class, property, etc.)

Click any member to open a detailed view with:
- Full member name and type
- File location
- Complete AI-generated summary
- Associated tags (if any)

### Semantic Search

Unlike traditional text search, semantic search understands meaning:
- **Traditional**: Search for "authenticate" finds only files containing that word
- **Semantic**: Search for "user login" finds authentication, validation, session management, etc.

Results are ranked by similarity percentage, showing the most relevant matches first.

### Hover Provider

Hover over any code member to see:
- Member type (class, method, property, etc.)
- AI-generated summary
- Visual icons for different member types

## Troubleshooting

### Extension Not Working

1. Check that the SourceCodeSummariser tool is built:
   ```bash
   cd /path/to/SourceCodeSummariser
   dotnet build
   ```

2. Verify your API key is set:
   ```bash
   echo $OPENAI_API_KEY  # Should print your key
   ```

3. Check the extension output:
   - Open Output panel (`View > Output`)
   - Select "SourceCode Summariser" from dropdown

### No Summaries Showing

1. Ensure you've run "Analyze Workspace" at least once
2. Check that `summaries.db` exists in your workspace root
3. Verify excluded folders don't contain your C# files
4. Check for errors in the Output panel

### Hover Not Working

1. Ensure `showHoverSummaries` is enabled in settings
2. Verify you're hovering over a C# file
3. Check that summaries exist for the hovered member
4. Try refreshing summaries

### Search Not Working

1. Ensure embeddings are enabled (`enableEmbeddings: true`)
2. Verify the database has embeddings (run analysis after enabling)
3. Check that you have an embedding model configured
4. Ensure the database path is correct

## Performance Tips

1. **Exclude large folders**: Add build outputs and dependencies to `excludedFolders`
2. **Use GPT-3.5**: Faster and cheaper than GPT-4 for most summaries
3. **Adjust token limits**: Lower `maxTokens` for faster processing
4. **Selective analysis**: Use "Analyze Current File" instead of full workspace
5. **Local LLMs**: Use Ollama for offline operation and unlimited requests

## Requirements

- Visual Studio Code 1.85.0 or higher
- .NET 8.0 SDK
- SourceCodeSummariser tool (included in parent directory)
- API key for chosen LLM provider

## Extension Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `sourcecodeSummariser.toolPath` | string | "" | Path to executable (auto-detected if empty) |
| `sourcecodeSummariser.llmProvider` | enum | "OpenAI" | LLM provider (OpenAI, Anthropic, Local) |
| `sourcecodeSummariser.model` | string | "gpt-3.5-turbo" | Model name to use |
| `sourcecodeSummariser.maxTokens` | number | 150 | Maximum tokens per summary |
| `sourcecodeSummariser.enableEmbeddings` | boolean | true | Enable semantic search |
| `sourcecodeSummariser.embeddingModel` | string | "text-embedding-ada-002" | Embedding model |
| `sourcecodeSummariser.databasePath` | string | "summaries.db" | SQLite database path |
| `sourcecodeSummariser.excludedFolders` | array | ["bin", "obj", ...] | Folders to exclude |
| `sourcecodeSummariser.autoWatch` | boolean | false | Auto-enable watch mode |
| `sourcecodeSummariser.showHoverSummaries` | boolean | true | Show hover tooltips |

## Known Issues

- Semantic search requires the SearchProgram.cs to be available
- Watch mode may have delays on large codebases
- Hover matching is fuzzy and may occasionally show wrong member

## Release Notes

### 1.0.0

Initial release with features:
- Workspace and file analysis
- Semantic search
- Tree view for summaries
- Hover provider
- Watch mode
- Multi-provider LLM support

## Contributing

This extension is part of the SourceCodeSummariser project. To contribute:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## License

See the main SourceCodeSummariser project for license information.

## Support

For issues, feature requests, or questions:
- Open an issue on GitHub
- Check the documentation in the parent project
- Review the troubleshooting section above

---

**Enjoy AI-powered code documentation!**
