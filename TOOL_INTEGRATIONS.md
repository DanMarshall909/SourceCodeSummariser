# Tool Integrations Guide

This guide shows how to integrate the embedding-based semantic search system with existing coding tools and workflows.

## Overview

The embedding system can be integrated with:
- **IDEs**: VS Code, Visual Studio, JetBrains
- **CI/CD**: GitHub Actions, GitLab CI, Azure DevOps
- **Code Review**: GitHub, GitLab, Bitbucket
- **Git Hooks**: Pre-commit, pre-push
- **Chat Tools**: Slack, Discord, Teams
- **CLI Tools**: Custom scripts and automation
- **APIs**: REST endpoints for any tool

## Table of Contents

1. [REST API Server](#rest-api-server)
2. [VS Code Extension](#vs-code-extension)
3. [GitHub Actions](#github-actions)
4. [Git Hooks](#git-hooks)
5. [GitHub Code Review Bot](#github-code-review-bot)
6. [Slack Bot](#slack-bot)
7. [CLI Tools](#cli-tools)
8. [Language Server Protocol (LSP)](#language-server-protocol)

---

## REST API Server

Create a REST API to expose semantic search to any tool.

### Implementation

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace SourceCodeSummariser.Api;

public class ApiServer
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure services
        builder.Services.AddSingleton(sp =>
        {
            var dbContext = new SummaryContext("Data Source=summaries.db");
            var llmProvider = CreateLlmProvider();
            var settings = new LlmProviderSettings
            {
                EnableEmbeddings = true,
                EmbeddingModel = "text-embedding-ada-002"
            };
            return new SemanticSearchService(dbContext, llmProvider, settings);
        });

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
            });
        });

        var app = builder.Build();
        app.UseCors();

        // Health check
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

        // Search endpoint
        app.MapPost("/api/search", async (
            HttpContext context,
            SemanticSearchService searchService) =>
        {
            var request = await context.Request.ReadFromJsonAsync<SearchRequest>();
            if (request == null || string.IsNullOrEmpty(request.Query))
            {
                return Results.BadRequest(new { error = "Query is required" });
            }

            var results = await searchService.SearchAsync(
                request.Query,
                request.TopK ?? 10,
                request.MinSimilarity ?? 0.6f
            );

            return Results.Ok(new SearchResponse
            {
                Query = request.Query,
                Results = results.Select(r => new SearchResultDto
                {
                    Id = r.Member.Id,
                    Name = r.Member.Name,
                    Type = r.Member.Type,
                    Summary = r.Member.Summary,
                    FilePath = r.FilePath,
                    Similarity = r.Similarity
                }).ToList()
            });
        });

        // Search with tags
        app.MapPost("/api/search/tags", async (
            HttpContext context,
            SemanticSearchService searchService) =>
        {
            var request = await context.Request.ReadFromJsonAsync<SearchWithTagsRequest>();
            if (request == null || string.IsNullOrEmpty(request.Query))
            {
                return Results.BadRequest(new { error = "Query is required" });
            }

            var results = await searchService.SearchWithTagsAsync(
                request.Query,
                request.Tags ?? new List<string>(),
                request.TopK ?? 10,
                request.MinSimilarity ?? 0.6f
            );

            return Results.Ok(new SearchResponse
            {
                Query = request.Query,
                Results = results.Select(r => new SearchResultDto
                {
                    Id = r.Member.Id,
                    Name = r.Member.Name,
                    Type = r.Member.Type,
                    Summary = r.Member.Summary,
                    FilePath = r.FilePath,
                    Similarity = r.Similarity,
                    Tags = r.Tags
                }).ToList()
            });
        });

        // Find similar members
        app.MapGet("/api/similar/{memberId}", async (
            int memberId,
            int? topK,
            float? minSimilarity,
            SemanticSearchService searchService) =>
        {
            try
            {
                var results = await searchService.FindSimilarMembersAsync(
                    memberId,
                    topK ?? 10,
                    minSimilarity ?? 0.7f
                );

                return Results.Ok(new
                {
                    MemberId = memberId,
                    Results = results.Select(r => new SearchResultDto
                    {
                        Id = r.Member.Id,
                        Name = r.Member.Name,
                        Type = r.Member.Type,
                        Summary = r.Member.Summary,
                        FilePath = r.FilePath,
                        Similarity = r.Similarity
                    })
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Get context for AI
        app.MapPost("/api/context", async (
            HttpContext context,
            SemanticSearchService searchService) =>
        {
            var request = await context.Request.ReadFromJsonAsync<ContextRequest>();
            if (request == null || string.IsNullOrEmpty(request.Task))
            {
                return Results.BadRequest(new { error = "Task is required" });
            }

            var results = await searchService.SearchAsync(
                request.Task,
                request.MaxExamples ?? 5,
                request.MinSimilarity ?? 0.6f
            );

            // Format for LLM prompt
            var context = FormatContextForPrompt(results, request.Task);

            return Results.Ok(new
            {
                Task = request.Task,
                Context = context,
                Examples = results.Select(r => new SearchResultDto
                {
                    Id = r.Member.Id,
                    Name = r.Member.Name,
                    Type = r.Member.Type,
                    Summary = r.Member.Summary,
                    FilePath = r.FilePath,
                    Similarity = r.Similarity
                })
            });
        });

        app.Run("http://localhost:5000");
    }

    private static string FormatContextForPrompt(List<SemanticSearchResult> results, string task)
    {
        var context = $"Task: {task}\n\n";
        context += "Relevant examples from the codebase:\n\n";

        foreach (var result in results)
        {
            context += $"## {result.Member.Type}: {result.Member.Name}\n";
            context += $"File: {result.FilePath}\n";
            context += $"Similarity: {result.Similarity:F2}\n";
            context += $"Description: {result.Member.Summary}\n\n";
        }

        return context;
    }

    private static ILlmProvider CreateLlmProvider()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        return new OpenAIProvider(new HttpClient(), new OpenAISettings
        {
            ApiKey = apiKey,
            Model = "gpt-3.5-turbo",
            MaxTokens = 50
        });
    }
}

// DTOs
public record SearchRequest
{
    public string Query { get; init; }
    public int? TopK { get; init; }
    public float? MinSimilarity { get; init; }
}

public record SearchWithTagsRequest
{
    public string Query { get; init; }
    public List<string>? Tags { get; init; }
    public int? TopK { get; init; }
    public float? MinSimilarity { get; init; }
}

public record ContextRequest
{
    public string Task { get; init; }
    public int? MaxExamples { get; init; }
    public float? MinSimilarity { get; init; }
}

public record SearchResponse
{
    public string Query { get; init; }
    public List<SearchResultDto> Results { get; init; }
}

public record SearchResultDto
{
    public int Id { get; init; }
    public string Name { get; init; }
    public string Type { get; init; }
    public string Summary { get; init; }
    public string FilePath { get; init; }
    public float Similarity { get; init; }
    public List<string>? Tags { get; init; }
}
```

### Usage

```bash
# Start the API server
dotnet run --project ApiServer.csproj

# Search for code
curl -X POST http://localhost:5000/api/search \
  -H "Content-Type: application/json" \
  -d '{"query": "async file operations", "topK": 5}'

# Get context for AI
curl -X POST http://localhost:5000/api/context \
  -H "Content-Type: application/json" \
  -d '{"task": "Add authentication to UserController"}'
```

---

## VS Code Extension

Create a VS Code extension for semantic code search.

### `extension.js`

```javascript
const vscode = require('vscode');
const axios = require('axios');

const API_BASE_URL = 'http://localhost:5000';

async function searchCode(query) {
    try {
        const response = await axios.post(`${API_BASE_URL}/api/search`, {
            query: query,
            topK: 10,
            minSimilarity: 0.6
        });
        return response.data.results;
    } catch (error) {
        vscode.window.showErrorMessage(`Search failed: ${error.message}`);
        return [];
    }
}

async function getContextForCurrentFile() {
    const editor = vscode.window.activeTextEditor;
    if (!editor) return null;

    const document = editor.document;
    const selection = editor.selection;
    const selectedText = document.getText(selection);

    // Extract intent from current code
    const intent = selectedText || document.getText();
    const task = `Complete or implement: ${intent.substring(0, 100)}`;

    try {
        const response = await axios.post(`${API_BASE_URL}/api/context`, {
            task: task,
            maxExamples: 5
        });
        return response.data.context;
    } catch (error) {
        vscode.window.showErrorMessage(`Context fetch failed: ${error.message}`);
        return null;
    }
}

function activate(context) {
    // Command: Semantic Search
    let searchCommand = vscode.commands.registerCommand(
        'codebase-rag.search',
        async () => {
            const query = await vscode.window.showInputBox({
                prompt: 'Search your codebase semantically',
                placeHolder: 'e.g., async file operations'
            });

            if (!query) return;

            const results = await searchCode(query);

            if (results.length === 0) {
                vscode.window.showInformationMessage('No results found');
                return;
            }

            // Show results in quick pick
            const items = results.map(r => ({
                label: r.name,
                description: r.filePath,
                detail: `${r.type} - Similarity: ${r.similarity.toFixed(2)} - ${r.summary}`,
                result: r
            }));

            const selected = await vscode.window.showQuickPick(items, {
                placeHolder: 'Select a result to view'
            });

            if (selected) {
                // Open the file
                const uri = vscode.Uri.file(selected.result.filePath);
                const doc = await vscode.workspace.openTextDocument(uri);
                await vscode.window.showTextDocument(doc);
            }
        }
    );

    // Command: Get Context for AI
    let contextCommand = vscode.commands.registerCommand(
        'codebase-rag.getContext',
        async () => {
            const context = await getContextForCurrentFile();

            if (!context) return;

            // Show context in new editor
            const doc = await vscode.workspace.openTextDocument({
                content: context,
                language: 'markdown'
            });
            await vscode.window.showTextDocument(doc, vscode.ViewColumn.Beside);
        }
    );

    // Command: Find Similar Code
    let similarCommand = vscode.commands.registerCommand(
        'codebase-rag.findSimilar',
        async () => {
            const memberId = await vscode.window.showInputBox({
                prompt: 'Enter member ID to find similar code',
                placeHolder: 'e.g., 42'
            });

            if (!memberId) return;

            try {
                const response = await axios.get(
                    `${API_BASE_URL}/api/similar/${memberId}?topK=10`
                );

                const results = response.data.results;

                // Show results
                const items = results.map(r => ({
                    label: r.name,
                    description: r.filePath,
                    detail: `Similarity: ${r.similarity.toFixed(2)} - ${r.summary}`,
                    result: r
                }));

                const selected = await vscode.window.showQuickPick(items);

                if (selected) {
                    const uri = vscode.Uri.file(selected.result.filePath);
                    const doc = await vscode.workspace.openTextDocument(uri);
                    await vscode.window.showTextDocument(doc);
                }
            } catch (error) {
                vscode.window.showErrorMessage(`Failed to find similar code: ${error.message}`);
            }
        }
    );

    // Status bar item
    const statusBarItem = vscode.window.createStatusBarItem(
        vscode.StatusBarAlignment.Right,
        100
    );
    statusBarItem.text = "$(search) RAG Search";
    statusBarItem.command = 'codebase-rag.search';
    statusBarItem.tooltip = 'Search codebase semantically';
    statusBarItem.show();

    context.subscriptions.push(
        searchCommand,
        contextCommand,
        similarCommand,
        statusBarItem
    );
}

function deactivate() {}

module.exports = {
    activate,
    deactivate
};
```

### `package.json`

```json
{
  "name": "codebase-rag",
  "displayName": "Codebase RAG Search",
  "description": "Semantic code search using embeddings",
  "version": "1.0.0",
  "engines": {
    "vscode": "^1.60.0"
  },
  "activationEvents": [
    "onCommand:codebase-rag.search",
    "onCommand:codebase-rag.getContext",
    "onCommand:codebase-rag.findSimilar"
  ],
  "main": "./extension.js",
  "contributes": {
    "commands": [
      {
        "command": "codebase-rag.search",
        "title": "RAG: Search Codebase"
      },
      {
        "command": "codebase-rag.getContext",
        "title": "RAG: Get AI Context"
      },
      {
        "command": "codebase-rag.findSimilar",
        "title": "RAG: Find Similar Code"
      }
    ],
    "keybindings": [
      {
        "command": "codebase-rag.search",
        "key": "ctrl+shift+f",
        "mac": "cmd+shift+f"
      },
      {
        "command": "codebase-rag.getContext",
        "key": "ctrl+shift+c",
        "mac": "cmd+shift+c"
      }
    ]
  },
  "dependencies": {
    "axios": "^1.4.0"
  }
}
```

### Installation

```bash
# Install dependencies
npm install

# Package the extension
vsce package

# Install in VS Code
code --install-extension codebase-rag-1.0.0.vsix
```

---

## GitHub Actions

Integrate with CI/CD for automated code analysis.

### `.github/workflows/semantic-analysis.yml`

```yaml
name: Semantic Code Analysis

on:
  pull_request:
    types: [opened, synchronize]
  push:
    branches: [main, develop]

jobs:
  analyze:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v3
        with:
          fetch-depth: 0  # Full history for analysis

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Restore dependencies
        run: dotnet restore
        working-directory: ./SourceCodeSummariser

      - name: Download database
        run: |
          # Download pre-built embeddings database from artifacts
          gh run download --name summaries-db --repo ${{ github.repository }}
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}

      - name: Analyze changed files
        run: |
          # Get changed files in PR
          if [ "${{ github.event_name }}" == "pull_request" ]; then
            FILES=$(git diff --name-only ${{ github.event.pull_request.base.sha }} ${{ github.sha }} | grep '\.cs$')
          else
            FILES=$(git diff --name-only HEAD~1 HEAD | grep '\.cs$')
          fi

          # Process each changed file
          for file in $FILES; do
            echo "Analyzing $file..."
            dotnet run --project ./SourceCodeSummariser/SourceCodeSummariser.csproj -- "$file"
          done

      - name: Find similar code
        id: similar
        run: |
          # Use the search tool to find similar implementations
          OUTPUT=$(dotnet run --project ./SourceCodeSummariser/SearchProgram.cs -- \
            "similar patterns" -k 5 -s 0.8)

          echo "similar_code<<EOF" >> $GITHUB_OUTPUT
          echo "$OUTPUT" >> $GITHUB_OUTPUT
          echo "EOF" >> $GITHUB_OUTPUT

      - name: Check for duplicates
        id: duplicates
        run: |
          # Find potential duplicate code (high similarity)
          DUPLICATES=$(dotnet run --project ./SourceCodeSummariser/SearchProgram.cs -- \
            --similar-to $MEMBER_ID -k 10 -s 0.95)

          if [ ! -z "$DUPLICATES" ]; then
            echo "has_duplicates=true" >> $GITHUB_OUTPUT
            echo "duplicates<<EOF" >> $GITHUB_OUTPUT
            echo "$DUPLICATES" >> $GITHUB_OUTPUT
            echo "EOF" >> $GITHUB_OUTPUT
          fi

      - name: Comment on PR
        if: github.event_name == 'pull_request'
        uses: actions/github-script@v6
        with:
          script: |
            const fs = require('fs');

            let comment = '## 🔍 Semantic Code Analysis\n\n';

            // Add similar code findings
            if ('${{ steps.similar.outputs.similar_code }}') {
              comment += '### Similar Patterns Found\n\n';
              comment += '${{ steps.similar.outputs.similar_code }}\n\n';
            }

            // Add duplicate warnings
            if ('${{ steps.duplicates.outputs.has_duplicates }}' === 'true') {
              comment += '### ⚠️ Potential Duplicates Detected\n\n';
              comment += '${{ steps.duplicates.outputs.duplicates }}\n\n';
              comment += 'Consider refactoring to reduce code duplication.\n\n';
            }

            // Post comment
            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: comment
            });

      - name: Upload updated database
        uses: actions/upload-artifact@v3
        with:
          name: summaries-db
          path: summaries.db
```

### Usage in PR Workflow

```yaml
name: PR Code Review Assistant

on:
  pull_request:
    types: [opened, synchronize]

jobs:
  review:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Get AI context for changes
        run: |
          # Get PR description
          PR_DESC="${{ github.event.pull_request.body }}"

          # Search for relevant context
          curl -X POST http://your-api-server:5000/api/context \
            -H "Content-Type: application/json" \
            -d "{\"task\": \"$PR_DESC\"}" > context.json

          # Use context for code review
          # (Send to your LLM with the context)
```

---

## Git Hooks

Integrate with git hooks for local development.

### `.git/hooks/pre-commit`

```bash
#!/bin/bash

echo "🔍 Running semantic code analysis..."

# Get staged C# files
STAGED_FILES=$(git diff --cached --name-only --diff-filter=ACM | grep '\.cs$')

if [ -z "$STAGED_FILES" ]; then
    echo "No C# files to analyze"
    exit 0
fi

# Check for similar code
for file in $STAGED_FILES; do
    echo "Analyzing $file..."

    # Use the search API to find similar code
    SIMILAR=$(curl -s -X POST http://localhost:5000/api/search \
        -H "Content-Type: application/json" \
        -d "{\"query\": \"$(cat $file | head -50)\", \"topK\": 3, \"minSimilarity\": 0.9}")

    # Parse response
    COUNT=$(echo $SIMILAR | jq '.results | length')

    if [ "$COUNT" -gt 0 ]; then
        echo "⚠️  Found $COUNT similar code patterns in:"
        echo $SIMILAR | jq -r '.results[] | "  - \(.filePath) (similarity: \(.similarity))"'
        echo ""
        echo "Consider:"
        echo "  1. Reusing existing code"
        echo "  2. Extracting to a shared function"
        echo "  3. Documenting why duplication is necessary"
        echo ""

        # Ask user to confirm
        read -p "Continue with commit? (y/n) " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            exit 1
        fi
    fi
done

echo "✓ Analysis complete"
exit 0
```

### Installation

```bash
# Make executable
chmod +x .git/hooks/pre-commit

# Or install globally
git config --global core.hooksPath ~/.git-hooks
```

---

## GitHub Code Review Bot

Automated code review using semantic analysis.

### `review-bot.js`

```javascript
const { Octokit } = require('@octokit/rest');
const axios = require('axios');

const octokit = new Octokit({ auth: process.env.GITHUB_TOKEN });
const API_BASE = 'http://localhost:5000';

async function reviewPullRequest(owner, repo, pullNumber) {
    // Get PR details
    const { data: pr } = await octokit.pulls.get({
        owner,
        repo,
        pull_number: pullNumber
    });

    // Get changed files
    const { data: files } = await octokit.pulls.listFiles({
        owner,
        repo,
        pull_number: pullNumber
    });

    const comments = [];

    for (const file of files) {
        if (!file.filename.endsWith('.cs')) continue;

        // Get file content
        const { data: content } = await octokit.repos.getContent({
            owner,
            repo,
            path: file.filename,
            ref: pr.head.sha
        });

        const code = Buffer.from(content.content, 'base64').toString();

        // Search for similar code
        const response = await axios.post(`${API_BASE}/api/search`, {
            query: code.substring(0, 500),
            topK: 5,
            minSimilarity: 0.85
        });

        const similar = response.data.results;

        if (similar.length > 0) {
            // Create review comment
            comments.push({
                path: file.filename,
                body: `### 🔍 Similar Code Found\n\n` +
                      `This code is similar to:\n` +
                      similar.map(s =>
                          `- **${s.name}** in \`${s.filePath}\` (similarity: ${s.similarity.toFixed(2)})\n` +
                          `  ${s.summary}`
                      ).join('\n') +
                      `\n\nConsider refactoring to reduce duplication.`,
                line: 1
            });
        }

        // Check architectural consistency
        const contextResponse = await axios.post(`${API_BASE}/api/context`, {
            task: pr.title,
            maxExamples: 3
        });

        // Analyze if code follows patterns
        // (This is simplified - you'd use an LLM here)
    }

    // Post review
    if (comments.length > 0) {
        await octokit.pulls.createReview({
            owner,
            repo,
            pull_number: pullNumber,
            event: 'COMMENT',
            comments: comments
        });
    }
}

// Run on PR webhook
const express = require('express');
const app = express();

app.use(express.json());

app.post('/webhook', async (req, res) => {
    const { action, pull_request, repository } = req.body;

    if (action === 'opened' || action === 'synchronize') {
        const [owner, repo] = repository.full_name.split('/');
        await reviewPullRequest(owner, repo, pull_request.number);
    }

    res.status(200).send('OK');
});

app.listen(3000, () => {
    console.log('Review bot listening on port 3000');
});
```

---

## Slack Bot

Interactive code search via Slack.

### `slack-bot.js`

```javascript
const { App } = require('@slack/bolt');
const axios = require('axios');

const API_BASE = 'http://localhost:5000';

const app = new App({
    token: process.env.SLACK_BOT_TOKEN,
    signingSecret: process.env.SLACK_SIGNING_SECRET
});

// /code-search command
app.command('/code-search', async ({ command, ack, respond }) => {
    await ack();

    const query = command.text;

    if (!query) {
        await respond('Usage: `/code-search <query>`\nExample: `/code-search async file operations`');
        return;
    }

    try {
        const response = await axios.post(`${API_BASE}/api/search`, {
            query: query,
            topK: 5,
            minSimilarity: 0.6
        });

        const results = response.data.results;

        if (results.length === 0) {
            await respond(`No results found for: "${query}"`);
            return;
        }

        // Format results
        const blocks = [
            {
                type: 'section',
                text: {
                    type: 'mrkdwn',
                    text: `*Search results for:* "${query}"`
                }
            },
            { type: 'divider' }
        ];

        results.forEach((result, i) => {
            blocks.push({
                type: 'section',
                text: {
                    type: 'mrkdwn',
                    text: `*${i + 1}. ${result.name}*\n` +
                          `📁 \`${result.filePath}\`\n` +
                          `🎯 Similarity: ${result.similarity.toFixed(2)}\n` +
                          `📝 ${result.summary}`
                }
            });
        });

        await respond({ blocks });
    } catch (error) {
        await respond(`Error: ${error.message}`);
    }
});

// /code-context command for AI
app.command('/code-context', async ({ command, ack, respond }) => {
    await ack();

    const task = command.text;

    if (!task) {
        await respond('Usage: `/code-context <task>`\nExample: `/code-context Add authentication`');
        return;
    }

    try {
        const response = await axios.post(`${API_BASE}/api/context`, {
            task: task,
            maxExamples: 5
        });

        const context = response.data.context;

        await respond({
            blocks: [
                {
                    type: 'section',
                    text: {
                        type: 'mrkdwn',
                        text: `*Context for:* "${task}"`
                    }
                },
                {
                    type: 'section',
                    text: {
                        type: 'mrkdwn',
                        text: '```\n' + context + '\n```'
                    }
                }
            ]
        });
    } catch (error) {
        await respond(`Error: ${error.message}`);
    }
});

// Interactive search
app.action('search_action', async ({ ack, body, client }) => {
    await ack();

    // Open modal for search
    await client.views.open({
        trigger_id: body.trigger_id,
        view: {
            type: 'modal',
            callback_id: 'search_modal',
            title: { type: 'plain_text', text: 'Code Search' },
            submit: { type: 'plain_text', text: 'Search' },
            blocks: [
                {
                    type: 'input',
                    block_id: 'query_block',
                    element: {
                        type: 'plain_text_input',
                        action_id: 'query_input',
                        placeholder: {
                            type: 'plain_text',
                            text: 'e.g., async file operations'
                        }
                    },
                    label: { type: 'plain_text', text: 'Search Query' }
                }
            ]
        }
    });
});

(async () => {
    await app.start(process.env.PORT || 3000);
    console.log('⚡️ Slack bot is running!');
})();
```

---

## CLI Tools

Create custom shell commands.

### `~/.bashrc` or `~/.zshrc`

```bash
# Semantic code search
codesearch() {
    if [ -z "$1" ]; then
        echo "Usage: codesearch <query>"
        return 1
    fi

    curl -s -X POST http://localhost:5000/api/search \
        -H "Content-Type: application/json" \
        -d "{\"query\": \"$1\", \"topK\": 10}" \
        | jq -r '.results[] | "\(.name) in \(.filePath) (similarity: \(.similarity))\n  \(.summary)\n"'
}

# Get AI context
codecontext() {
    if [ -z "$1" ]; then
        echo "Usage: codecontext <task>"
        return 1
    fi

    curl -s -X POST http://localhost:5000/api/context \
        -H "Content-Type: application/json" \
        -d "{\"task\": \"$1\", \"maxExamples\": 5}" \
        | jq -r '.context'
}

# Find similar code
codesimilar() {
    if [ -z "$1" ]; then
        echo "Usage: codesimilar <member_id>"
        return 1
    fi

    curl -s "http://localhost:5000/api/similar/$1?topK=10" \
        | jq -r '.results[] | "\(.name) in \(.filePath) (similarity: \(.similarity))"'
}
```

### Usage

```bash
# Search semantically
$ codesearch "async file operations"

# Get context for task
$ codecontext "Add authentication to UserController"

# Find similar code
$ codesimilar 42
```

---

## Language Server Protocol

Integrate with any IDE via LSP.

### `lsp-server.js`

```javascript
const {
    createConnection,
    TextDocuments,
    ProposedFeatures
} = require('vscode-languageserver/node');
const { TextDocument } = require('vscode-languageserver-textdocument');
const axios = require('axios');

const API_BASE = 'http://localhost:5000';

// Create connection
const connection = createConnection(ProposedFeatures.all);
const documents = new TextDocuments(TextDocument);

connection.onInitialize(() => {
    return {
        capabilities: {
            textDocumentSync: 1,
            completionProvider: {
                resolveProvider: true
            },
            hoverProvider: true
        }
    };
});

// Provide context-aware completions
connection.onCompletion(async (textDocumentPosition) => {
    const document = documents.get(textDocumentPosition.textDocument.uri);
    const text = document.getText();

    // Get context from semantic search
    try {
        const response = await axios.post(`${API_BASE}/api/context`, {
            task: text.substring(0, 200),
            maxExamples: 3
        });

        const examples = response.data.examples;

        return examples.map((example, i) => ({
            label: example.name,
            kind: 2, // Method
            detail: example.type,
            documentation: example.summary,
            insertText: `// Similar to: ${example.filePath}\n`
        }));
    } catch (error) {
        return [];
    }
});

// Provide hover information
connection.onHover(async (textDocumentPosition) => {
    const document = documents.get(textDocumentPosition.textDocument.uri);
    const text = document.getText();

    // Search for similar code
    try {
        const response = await axios.post(`${API_BASE}/api/search`, {
            query: text.substring(0, 100),
            topK: 3,
            minSimilarity: 0.7
        });

        const results = response.data.results;

        if (results.length === 0) return null;

        const markdown = results.map(r =>
            `**${r.name}** in \`${r.filePath}\`\n${r.summary}`
        ).join('\n\n');

        return {
            contents: {
                kind: 'markdown',
                value: markdown
            }
        };
    } catch (error) {
        return null;
    }
});

documents.listen(connection);
connection.listen();
```

---

## Configuration

### Environment Variables

```bash
# API Settings
export CODEBASE_API_URL="http://localhost:5000"
export OPENAI_API_KEY="your-api-key"

# GitHub
export GITHUB_TOKEN="your-github-token"

# Slack
export SLACK_BOT_TOKEN="xoxb-your-token"
export SLACK_SIGNING_SECRET="your-secret"

# Database
export DATABASE_PATH="/path/to/summaries.db"
```

### `appsettings.json` for API Server

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000", "https://your-app.com"]
  },
  "LlmProvider": {
    "Provider": "OpenAI",
    "Model": "gpt-3.5-turbo",
    "EnableEmbeddings": true,
    "EmbeddingModel": "text-embedding-ada-002"
  },
  "Database": {
    "ConnectionString": "Data Source=summaries.db"
  }
}
```

---

## Deployment

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "ApiServer.dll"]
```

```bash
# Build and run
docker build -t codebase-rag-api .
docker run -p 5000:5000 \
  -e OPENAI_API_KEY=$OPENAI_API_KEY \
  -v $(pwd)/summaries.db:/app/summaries.db \
  codebase-rag-api
```

---

## Summary

Integration points:
- ✅ REST API for universal access
- ✅ VS Code extension for IDE integration
- ✅ GitHub Actions for CI/CD
- ✅ Git hooks for local development
- ✅ GitHub bot for code review
- ✅ Slack bot for team collaboration
- ✅ CLI tools for shell integration
- ✅ LSP server for any IDE

All tools communicate via the REST API, making the system highly extensible and tool-agnostic.
