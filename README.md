# Source Code Summariser

An AI-powered C# code documentation tool that automatically generates intelligent summaries of your codebase using OpenAI's GPT models. Track changes over time and maintain up-to-date documentation effortlessly.

## Features

- **Easy Initialization**: Quick setup for existing codebases with `--init` command
- **AI-Powered Summaries**: Uses OpenAI's GPT models to generate concise, accurate summaries of your code
- **Multi-Provider Support**: Works with OpenAI, Anthropic (via LangChain), or Local LLMs (Ollama)
- **Watch Mode**: Continuously monitors your codebase for changes and updates summaries in real-time
- **Change Tracking**: Stores summaries in a SQLite database and tracks changes over time
- **Comprehensive Analysis**: Analyzes classes, methods, properties, fields, interfaces, structs, and namespaces
- **Tag System**: Normalized tag system for efficient categorization
- **Configurable**: Flexible configuration via JSON file or environment variables
- **Error Handling**: Robust error handling with detailed logging
- **Fast & Async**: Fully asynchronous processing for optimal performance

## Prerequisites

- .NET 8.0 SDK or later
- OpenAI API key (get one at https://platform.openai.com/api-keys)

## Quick Start

### Option 1: Initialize for Existing Codebase (Recommended)

The easiest way to get started is to use the `--init` command:

1. Clone the repository:
```bash
git clone <repository-url>
cd SourceCodeSummariser
```

2. Restore dependencies and build:
```bash
dotnet restore
dotnet build
```

3. Initialize the tool:
```bash
dotnet run --init
```

This will:
- Create `appsettings.json` from the example template
- Initialize the SQLite database
- Provide guidance on next steps

4. Set your API key (see Configuration section below)

5. Start processing your code:
```bash
dotnet run <path-to-your-code>
```

### Option 2: Manual Installation

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

4. (Optional) Copy configuration:
```bash
cp appsettings.example.json appsettings.json
```

## Configuration

### Set Your OpenAI API Key (Required)

**IMPORTANT**: For security, always set your API key as an environment variable. Never commit API keys to version control.

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

### Optional: Customize Settings

The `appsettings.json` file is optional. If you want to customize settings like the model, max tokens, or excluded folders:

1. Copy the example configuration:
```bash
cp appsettings.example.json appsettings.json
```

2. Edit `appsettings.json` to customize non-sensitive settings (model, timeouts, excluded folders, etc.)

**Note**: The application will work with just environment variables using sensible defaults. You only need `appsettings.json` if you want to customize the default behavior.

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
dotnet run --init      # Initialize tool for existing codebase
dotnet run --help      # Show help message
dotnet run -h          # Show help message
dotnet run --watch     # Enable watch mode (continuous monitoring)
dotnet run -w          # Enable watch mode (short form)
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

- `ApiKey`: Your OpenAI API key (required) - **Set via environment variable `OpenAI__ApiKey`**
- `Model`: OpenAI model to use (default: `gpt-3.5-turbo`) - Configurable in appsettings.json
- `MaxTokens`: Maximum tokens per summary (default: `50`) - Configurable in appsettings.json
- `TimeoutSeconds`: HTTP timeout in seconds (default: `30`) - Configurable in appsettings.json

### Database Settings

- `ConnectionString`: SQLite connection string (default: `Data Source=summaries.db`) - Configurable in appsettings.json

### Processing Settings

- `ExcludedFolders`: Array of folder names to skip (default: `["bin", "obj", ".git", ".vs", "node_modules"]`) - Configurable in appsettings.json
- `FilePattern`: File pattern to match (default: `*.cs`) - Configurable in appsettings.json
- `MaxRetries`: Number of retry attempts for failed operations (default: `3`) - Configurable in appsettings.json
- `RetryDelayMilliseconds`: Delay between retries in milliseconds (default: `1000`) - Configurable in appsettings.json

### Customizing Settings

You can customize non-sensitive settings by editing `appsettings.json`:

```json
{
  "OpenAI": {
    "Model": "gpt-4",
    "MaxTokens": 100,
    "TimeoutSeconds": 60
  },
  "Processing": {
    "ExcludedFolders": [ "bin", "obj", ".git", ".vs", "node_modules", "packages" ]
  }
}
```

**Note**: Never put your API key in `appsettings.json`. Always use environment variables for sensitive credentials.

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

Make sure you've set your API key as an environment variable (`OpenAI__ApiKey`). See the Configuration section above for details.

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
