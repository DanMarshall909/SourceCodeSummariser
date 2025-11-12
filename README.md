# Source Code Summariser

An AI-powered C# code documentation tool that automatically generates intelligent summaries of your codebase using OpenAI's GPT models. Track changes over time and maintain up-to-date documentation effortlessly.

## Features

- **AI-Powered Summaries**: Uses OpenAI's GPT models to generate concise, accurate summaries of your code
- **Change Tracking**: Stores summaries in a SQLite database and tracks changes over time
- **Comprehensive Analysis**: Analyzes classes, methods, properties, fields, interfaces, structs, and namespaces
- **Configurable**: Flexible configuration via JSON file or environment variables
- **Error Handling**: Robust error handling with detailed logging
- **Fast & Async**: Fully asynchronous processing for optimal performance

## Prerequisites

- .NET 8.0 SDK or later
- OpenAI API key (get one at https://platform.openai.com/api-keys)

## Installation

1. Clone the repository:
```bash
git clone <repository-url>
cd SourceCodeSummariser
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Build the project:
```bash
dotnet build
```

## Configuration

### Option 1: Edit appsettings.json

Open `appsettings.json` and set your OpenAI API key:

```json
{
  "OpenAI": {
    "ApiKey": "sk-your-api-key-here",
    "Model": "gpt-3.5-turbo",
    "MaxTokens": 50,
    "TimeoutSeconds": 30
  },
  "Database": {
    "ConnectionString": "Data Source=summaries.db"
  },
  "Processing": {
    "ExcludedFolders": [ "bin", "obj", ".git", ".vs", "node_modules" ],
    "FilePattern": "*.cs",
    "MaxRetries": 3,
    "RetryDelayMilliseconds": 1000
  }
}
```

### Option 2: Use Environment Variables

Set your API key via environment variable:

**Linux/macOS:**
```bash
export OpenAI__ApiKey="sk-your-api-key-here"
```

**Windows (PowerShell):**
```powershell
$env:OpenAI__ApiKey="sk-your-api-key-here"
```

**Windows (CMD):**
```cmd
set OpenAI__ApiKey=sk-your-api-key-here
```

## Usage

### Basic Usage

Run the tool with a path to your C# codebase:

```bash
dotnet run <path-to-source-folder>
```

### Examples

Analyze the current directory:
```bash
dotnet run .
```

Analyze a specific project:
```bash
dotnet run ./MyProject/src
```

Analyze with absolute path:
```bash
dotnet run /home/user/projects/MyApp
```

### Command-Line Options

```
dotnet run --help      # Show help message
dotnet run -h          # Show help message
```

## How It Works

1. **Discovery**: Scans the specified folder recursively for `.cs` files
2. **Parsing**: Uses Roslyn (Microsoft.CodeAnalysis) to parse C# syntax trees
3. **Analysis**: Extracts all code members (classes, methods, properties, etc.)
4. **Summarization**: Sends methods to OpenAI API for intelligent summarization
5. **Storage**: Saves summaries to SQLite database with hash-based change detection
6. **Reporting**: Displays any changes detected since last run

## Output

The tool provides detailed console output:

```
===========================================
  Source Code Summariser
  AI-powered C# code documentation tool
===========================================

Processing folder: ./MyProject
Using model: gpt-3.5-turbo
Database: Data Source=summaries.db

Found 15 C# files to process.

  [PROCESSING] Program.cs... ✓ (3 changes detected)
  [PROCESSING] SummarizerService.cs... ✓ (no changes)
  [SKIP] obj/Debug/Program.g.cs (excluded folder)
  ...

===========================================
Processing Summary:
  Total files found: 15
  Successfully processed: 13
  Skipped/Failed: 2
  Files with changes: 5
===========================================

CHANGES DETECTED:

File: ./MyProject/Program.cs

  ### Method: public async Task Main(string[] args)
      Old: Entry point for application startup.
      New: Initializes configuration and starts application processing.
```

## Database

Summaries are stored in `summaries.db` (SQLite database) with the following structure:

- **Files**: Tracks C# source files
- **Members**: Stores code member summaries with hash-based change detection

The database is created automatically on first run.

## Configuration Options

### OpenAI Settings

- `ApiKey`: Your OpenAI API key (required)
- `Model`: OpenAI model to use (default: `gpt-3.5-turbo`)
- `MaxTokens`: Maximum tokens per summary (default: `50`)
- `TimeoutSeconds`: HTTP timeout in seconds (default: `30`)

### Database Settings

- `ConnectionString`: SQLite connection string (default: `Data Source=summaries.db`)

### Processing Settings

- `ExcludedFolders`: Array of folder names to skip (default: `["bin", "obj", ".git", ".vs", "node_modules"]`)
- `FilePattern`: File pattern to match (default: `*.cs`)
- `MaxRetries`: Number of retry attempts for failed operations (default: `3`)
- `RetryDelayMilliseconds`: Delay between retries in milliseconds (default: `1000`)

## Architecture

### Key Components

- **Program.cs**: Entry point and CLI interface
- **SummarizerService**: Core service for code analysis and AI summarization
- **FileProcessorService**: Handles file processing and database operations
- **Summarizers**: Strategy pattern implementations for different code member types
  - `MemberSummarizer` (abstract base)
  - `ClassSummarizer`, `MethodSummarizer`, `PropertySummarizer`, etc.

### Design Patterns

- **Strategy Pattern**: Different summarizers for different code member types
- **Dependency Injection**: Services passed through constructors
- **Repository Pattern**: FileProcessorService manages database operations
- **Async/Await**: Fully asynchronous for optimal performance

## Development

### Building

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Creating a Release Build

```bash
dotnet publish -c Release -r win-x64 --self-contained
dotnet publish -c Release -r linux-x64 --self-contained
dotnet publish -c Release -r osx-x64 --self-contained
```

## Troubleshooting

### "OpenAI API key not configured"

Make sure you've set your API key in `appsettings.json` or via environment variable.

### "The specified folder does not exist"

Check that the path you provided exists and is accessible.

### API Request Errors

- Verify your API key is valid
- Check your internet connection
- Ensure you have sufficient OpenAI API credits
- Try increasing the `TimeoutSeconds` setting

### Database Errors

If you encounter database errors, try deleting `summaries.db` to start fresh.

## Cost Considerations

This tool makes API calls to OpenAI for each method in your codebase. Consider:

- Start with a small codebase to estimate costs
- Use `gpt-3.5-turbo` for cost-effective summarization
- Adjust `MaxTokens` to control summary length and cost
- The tool only processes files that have changed after the first run

## Limitations

- Currently only supports C# code analysis
- Requires internet connection for OpenAI API
- Methods are the only members that get AI-powered summaries (other members get basic metadata)

## Future Enhancements

- Support for additional programming languages
- Local LLM support (no internet required)
- Summary export to markdown/HTML
- Integration with documentation generators
- Parallel processing for faster analysis
- Custom summary templates

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

[Add your license here]

## Acknowledgments

- Built with [Roslyn](https://github.com/dotnet/roslyn) for C# code analysis
- Powered by [OpenAI](https://openai.com) for intelligent summarization
- Uses [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/) for database management
