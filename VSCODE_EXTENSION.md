# VSCode Extension for SourceCode Summariser

This document describes the VSCode extension integration for the SourceCode Summariser tool.

## Overview

The VSCode extension provides a seamless integration of the SourceCode Summariser tool directly into your Visual Studio Code editor. It allows you to analyze C# code, view summaries, perform semantic searches, and more - all without leaving your development environment.

## Architecture

The extension is built using TypeScript and consists of the following main components:

### Core Components

1. **extension.ts** - Main entry point and command registration
   - Activates the extension when C# files are opened
   - Registers all commands and UI components
   - Manages watch mode lifecycle
   - Handles user interactions and progress reporting

2. **summarizerService.ts** - Service layer for invoking the C# tool
   - Auto-detects or uses configured path to the SourceCodeSummariser executable
   - Spawns processes to run analysis, search, and watch operations
   - Parses tool output and reports progress
   - Handles both compiled executable and `dotnet run` fallback

3. **databaseService.ts** - SQLite database access layer
   - Reads summaries from the SQLite database
   - Provides query methods for members, files, and statistics
   - Manages database connection lifecycle
   - Handles database refresh on updates

4. **summariesTreeProvider.ts** - Tree view provider
   - Implements VSCode's TreeDataProvider interface
   - Displays hierarchical view of files and members
   - Shows statistics and member counts
   - Provides icons and tooltips for different member types

5. **hoverProvider.ts** - Hover tooltip provider
   - Implements VSCode's HoverProvider interface
   - Shows AI-generated summaries on hover
   - Performs fuzzy matching for member names
   - Displays formatted markdown tooltips

## Features

### Commands

All commands are accessible via the Command Palette (`Ctrl+Shift+P`):

| Command | Description |
|---------|-------------|
| `sourcecodeSummariser.analyzeWorkspace` | Analyze all C# files in workspace |
| `sourcecodeSummariser.analyzeFile` | Analyze current C# file |
| `sourcecodeSummariser.semanticSearch` | Search code semantically |
| `sourcecodeSummariser.showSummaries` | Open summaries tree view |
| `sourcecodeSummariser.toggleWatch` | Toggle watch mode |
| `sourcecodeSummariser.initialize` | Initialize project configuration |
| `sourcecodeSummariser.refreshSummaries` | Refresh tree view |
| `sourcecodeSummariser.showMemberDetails` | Show detailed member info |

### UI Components

#### Activity Bar Icon
- Book icon in the activity bar
- Opens the Code Summaries view
- Shows at-a-glance statistics

#### Tree View
- Hierarchical display of files and members
- File-level grouping with member counts
- Member type icons (method, class, property, etc.)
- Click to view detailed summaries
- Refresh button for manual updates

#### Search Results Panel
- WebView-based search results display
- Similarity scores for each result
- Clickable member names and file paths
- Styled to match VSCode theme

#### Member Details Panel
- Full member information display
- Summary, file location, and tags
- Theme-aware styling
- Dedicated view for in-depth analysis

#### Hover Tooltips
- Markdown-formatted hover information
- Shows on mouse hover over code members
- Type icons and summary text
- Non-intrusive and fast

### Context Menu Integration

Right-click context menus in:
- **Explorer**: Analyze C# files directly from file browser
- **Editor**: Analyze current file from editor context

### Configuration

Extension settings are accessible via VSCode settings:

```json
{
  "sourcecodeSummariser.toolPath": "",
  "sourcecodeSummariser.llmProvider": "OpenAI",
  "sourcecodeSummariser.model": "gpt-3.5-turbo",
  "sourcecodeSummariser.maxTokens": 150,
  "sourcecodeSummariser.enableEmbeddings": true,
  "sourcecodeSummariser.embeddingModel": "text-embedding-ada-002",
  "sourcecodeSummariser.databasePath": "summaries.db",
  "sourcecodeSummariser.excludedFolders": ["bin", "obj", ".git", ".vs", "node_modules"],
  "sourcecodeSummariser.autoWatch": false,
  "sourcecodeSummariser.showHoverSummaries": true
}
```

## Development

### Building the Extension

```bash
cd vscode-extension
npm install
npm run compile
```

This compiles TypeScript to JavaScript in the `out/` directory.

### Running in Development

1. Open the `vscode-extension` folder in VSCode
2. Press `F5` to launch Extension Development Host
3. Test extension functionality in the new window

### Debugging

- Set breakpoints in TypeScript files
- Use VSCode's debugger to step through code
- Check Debug Console for logs and errors
- Use Output panel for extension logs

### Project Structure

```
vscode-extension/
├── src/
│   ├── extension.ts              # Main entry point
│   ├── summarizerService.ts      # C# tool integration
│   ├── databaseService.ts        # SQLite access
│   ├── summariesTreeProvider.ts  # Tree view
│   └── hoverProvider.ts          # Hover tooltips
├── out/                           # Compiled JavaScript (gitignored)
├── node_modules/                  # Dependencies (gitignored)
├── .vscode/
│   ├── launch.json               # Debug configuration
│   └── tasks.json                # Build tasks
├── package.json                   # Extension manifest
├── tsconfig.json                  # TypeScript config
├── .eslintrc.json                # ESLint config
├── .vscodeignore                 # Files to exclude from package
├── .gitignore                    # Git ignore rules
├── README.md                      # User documentation
└── CHANGELOG.md                   # Version history
```

## Packaging

To create a `.vsix` package for distribution:

```bash
npm install -g @vscode/vsce
cd vscode-extension
vsce package
```

This creates a `sourcecode-summariser-1.0.0.vsix` file.

## Installation

### From VSIX

```bash
code --install-extension sourcecode-summariser-1.0.0.vsix
```

Or in VSCode:
1. Go to Extensions view
2. Click "..." menu
3. Select "Install from VSIX..."
4. Choose the `.vsix` file

### From Marketplace (Future)

Once published:
1. Open Extensions view
2. Search "SourceCode Summariser"
3. Click Install

## Usage Workflow

### Initial Setup

1. Open a C# project in VSCode
2. Extension activates automatically
3. Configure API key in environment or settings
4. Run "Initialize Project" command
5. Run "Analyze Workspace" command

### Daily Use

1. **Hover for Quick Info**: Hover over code members to see summaries
2. **Browse Summaries**: Click book icon to open tree view
3. **Search Code**: Use semantic search to find relevant code
4. **Auto-Update**: Enable watch mode for automatic updates
5. **Detailed View**: Click members in tree for full information

## Integration Points

### With SourceCode Summariser Tool

The extension integrates with the C# tool via:
- **Process Spawning**: Runs `dotnet run` or compiled executable
- **Standard Output**: Parses tool output for progress and results
- **Database**: Reads SQLite database for summaries
- **File System**: Monitors for changes when watch mode is enabled

### With VSCode APIs

Uses the following VSCode extension APIs:
- **Commands API**: Registers and executes commands
- **Window API**: Creates webviews, shows messages, reports progress
- **Languages API**: Registers hover and other language features
- **Workspace API**: Accesses workspace folders and configuration
- **FileSystem API**: Monitors files and directories
- **TreeView API**: Provides custom tree views

## Performance Considerations

### Database Queries
- Read-only access for safety
- Queries are indexed in SQLite
- Results cached by VSCode

### Process Management
- Single watch process per workspace
- Processes terminated on deactivation
- Error handling prevents zombie processes

### UI Updates
- Tree view updates on-demand
- Hover providers are lazy-loaded
- WebViews created only when needed

## Error Handling

The extension handles errors gracefully:

1. **Tool Not Found**: Falls back to `dotnet run` or shows error message
2. **Database Missing**: Shows helpful message to run analysis first
3. **API Errors**: Reports LLM provider errors to user
4. **Process Crashes**: Cleans up and reports error

## Future Enhancements

Potential improvements for future versions:

1. **Multi-Language Support**: Extend beyond C#
2. **Code Actions**: Generate summaries inline
3. **Diff View**: Compare summary changes over time
4. **Team Sync**: Share summaries across team
5. **Custom Prompts**: User-defined summary templates
6. **Performance**: Optimize for very large codebases
7. **Testing**: Add comprehensive unit and integration tests

## Testing

### Manual Testing Checklist

- [ ] Extension activates on C# file open
- [ ] Analyze workspace processes all files
- [ ] Tree view displays correct hierarchy
- [ ] Hover shows summaries correctly
- [ ] Semantic search returns results
- [ ] Watch mode detects changes
- [ ] Settings are respected
- [ ] Commands work from palette
- [ ] Context menus appear correctly
- [ ] Error messages are helpful

### Automated Testing (TODO)

Future test coverage should include:
- Unit tests for services
- Integration tests for commands
- UI tests for tree view and hover
- End-to-end tests for full workflow

## Troubleshooting

### Extension Not Activating

**Problem**: Extension doesn't load when opening C# files
**Solution**:
- Check extension is enabled
- Reload window (`Ctrl+R`)
- Check for errors in Developer Tools console

### Tool Not Found

**Problem**: "Tool path not configured" error
**Solution**:
- Build the C# project first
- Set `sourcecodeSummariser.toolPath` in settings
- Ensure .NET 8.0 SDK is installed

### Database Not Found

**Problem**: "Database not found" error
**Solution**:
- Run "Analyze Workspace" command first
- Check `databasePath` setting points to correct location
- Verify workspace has been analyzed

### Hover Not Working

**Problem**: No tooltips on hover
**Solution**:
- Enable `showHoverSummaries` setting
- Ensure summaries exist for hovered members
- Check you're hovering on C# files
- Try refreshing summaries

### Search Not Working

**Problem**: Search returns no results
**Solution**:
- Enable `enableEmbeddings` setting
- Re-run analysis to generate embeddings
- Check API key is configured
- Verify embedding model is correct

## Contributing

To contribute to the VSCode extension:

1. Fork the repository
2. Create a feature branch
3. Make your changes to the TypeScript files
4. Test thoroughly (see Testing section)
5. Compile and verify no errors
6. Submit a pull request

Follow the TypeScript style guide and maintain consistency with existing code.

## License

See the main SourceCodeSummariser project for license information.

## Support

For issues specific to the VSCode extension:
1. Check troubleshooting section above
2. Review extension output logs
3. Open an issue on GitHub with:
   - VSCode version
   - Extension version
   - Error messages from Output panel
   - Steps to reproduce

---

**Happy coding with AI-powered documentation!**
