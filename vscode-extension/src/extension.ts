import * as vscode from 'vscode';
import { SummarizerService } from './summarizerService';
import { SummariesTreeProvider } from './summariesTreeProvider';
import { SummaryHoverProvider } from './hoverProvider';
import { DatabaseService } from './databaseService';

let summarizerService: SummarizerService;
let databaseService: DatabaseService;
let summariesTreeProvider: SummariesTreeProvider;
let watchModeActive = false;

export function activate(context: vscode.ExtensionContext) {
    console.log('SourceCode Summariser extension is now active');

    // Initialize services
    summarizerService = new SummarizerService();
    databaseService = new DatabaseService();
    summariesTreeProvider = new SummariesTreeProvider(databaseService);

    // Register tree view
    const treeView = vscode.window.createTreeView('summariesView', {
        treeDataProvider: summariesTreeProvider,
        showCollapseAll: true
    });

    // Register hover provider for C# files
    const config = vscode.workspace.getConfiguration('sourcecodeSummariser');
    if (config.get<boolean>('showHoverSummaries', true)) {
        const hoverProvider = new SummaryHoverProvider(databaseService);
        context.subscriptions.push(
            vscode.languages.registerHoverProvider('csharp', hoverProvider)
        );
    }

    // Register commands
    context.subscriptions.push(
        vscode.commands.registerCommand('sourcecodeSummariser.analyzeWorkspace', async () => {
            await analyzeWorkspace();
        }),

        vscode.commands.registerCommand('sourcecodeSummariser.analyzeFile', async (uri?: vscode.Uri) => {
            await analyzeFile(uri);
        }),

        vscode.commands.registerCommand('sourcecodeSummariser.semanticSearch', async () => {
            await performSemanticSearch();
        }),

        vscode.commands.registerCommand('sourcecodeSummariser.showSummaries', async () => {
            await showSummaries();
        }),

        vscode.commands.registerCommand('sourcecodeSummariser.toggleWatch', async () => {
            await toggleWatchMode();
        }),

        vscode.commands.registerCommand('sourcecodeSummariser.initialize', async () => {
            await initializeProject();
        }),

        vscode.commands.registerCommand('sourcecodeSummariser.refreshSummaries', async () => {
            await summariesTreeProvider.refresh();
        }),

        vscode.commands.registerCommand('sourcecodeSummariser.showMemberDetails', async (member: any) => {
            await showMemberDetails(member);
        })
    );

    context.subscriptions.push(treeView);

    // Auto-watch if configured
    if (config.get<boolean>('autoWatch', false)) {
        toggleWatchMode();
    }

    // Show welcome message
    vscode.window.showInformationMessage('SourceCode Summariser is ready!');
}

async function analyzeWorkspace() {
    const workspaceFolders = vscode.workspace.workspaceFolders;
    if (!workspaceFolders) {
        vscode.window.showErrorMessage('No workspace folder open');
        return;
    }

    const folder = workspaceFolders[0].uri.fsPath;

    await vscode.window.withProgress({
        location: vscode.ProgressLocation.Notification,
        title: 'Analyzing workspace...',
        cancellable: true
    }, async (progress, token) => {
        try {
            const result = await summarizerService.analyzeWorkspace(folder, (message) => {
                progress.report({ message });
            }, token);

            if (result.success) {
                vscode.window.showInformationMessage(`Workspace analyzed successfully! Processed ${result.filesProcessed} files.`);
                await summariesTreeProvider.refresh();
            } else {
                vscode.window.showErrorMessage(`Analysis failed: ${result.error}`);
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Analysis error: ${error}`);
        }
    });
}

async function analyzeFile(uri?: vscode.Uri) {
    let filePath: string;

    if (uri) {
        filePath = uri.fsPath;
    } else {
        const editor = vscode.window.activeTextEditor;
        if (!editor || editor.document.languageId !== 'csharp') {
            vscode.window.showErrorMessage('Please open a C# file');
            return;
        }
        filePath = editor.document.uri.fsPath;
    }

    await vscode.window.withProgress({
        location: vscode.ProgressLocation.Notification,
        title: `Analyzing ${filePath.split('/').pop()}...`,
        cancellable: false
    }, async (progress) => {
        try {
            const result = await summarizerService.analyzeFile(filePath);

            if (result.success) {
                vscode.window.showInformationMessage(`File analyzed successfully!`);
                await summariesTreeProvider.refresh();
            } else {
                vscode.window.showErrorMessage(`Analysis failed: ${result.error}`);
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Analysis error: ${error}`);
        }
    });
}

async function performSemanticSearch() {
    const query = await vscode.window.showInputBox({
        prompt: 'Enter search query',
        placeHolder: 'e.g., "methods that handle authentication"'
    });

    if (!query) {
        return;
    }

    const topK = await vscode.window.showInputBox({
        prompt: 'Number of results',
        value: '10',
        validateInput: (value) => {
            const num = parseInt(value);
            return isNaN(num) || num < 1 ? 'Please enter a valid number' : null;
        }
    });

    const k = topK ? parseInt(topK) : 10;

    await vscode.window.withProgress({
        location: vscode.ProgressLocation.Notification,
        title: 'Searching...',
        cancellable: false
    }, async () => {
        try {
            const results = await summarizerService.semanticSearch(query, k);

            if (results.success && results.results) {
                await showSearchResults(query, results.results);
            } else {
                vscode.window.showErrorMessage(`Search failed: ${results.error}`);
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Search error: ${error}`);
        }
    });
}

async function showSearchResults(query: string, results: any[]) {
    const panel = vscode.window.createWebviewPanel(
        'searchResults',
        `Search: ${query}`,
        vscode.ViewColumn.One,
        { enableScripts: true }
    );

    panel.webview.html = getSearchResultsHtml(query, results);
}

function getSearchResultsHtml(query: string, results: any[]): string {
    const resultItems = results.map((result, index) => `
        <div class="result-item">
            <div class="result-header">
                <span class="result-index">${index + 1}</span>
                <span class="result-name">${result.memberName || result.MemberSignature}</span>
                <span class="similarity-score">${(result.similarity * 100).toFixed(1)}%</span>
            </div>
            <div class="result-type">${result.memberType || result.Type}</div>
            <div class="result-file">${result.fileName || result.FileName}</div>
            <div class="result-summary">${result.summary || result.Summary}</div>
        </div>
    `).join('');

    return `<!DOCTYPE html>
    <html lang="en">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>Search Results</title>
        <style>
            body {
                padding: 20px;
                font-family: var(--vscode-font-family);
                color: var(--vscode-foreground);
                background-color: var(--vscode-editor-background);
            }
            h1 {
                border-bottom: 1px solid var(--vscode-panel-border);
                padding-bottom: 10px;
            }
            .query {
                color: var(--vscode-textLink-foreground);
                font-style: italic;
            }
            .result-item {
                margin: 20px 0;
                padding: 15px;
                border: 1px solid var(--vscode-panel-border);
                border-radius: 4px;
                background-color: var(--vscode-editor-inactiveSelectionBackground);
            }
            .result-header {
                display: flex;
                align-items: center;
                margin-bottom: 8px;
            }
            .result-index {
                background-color: var(--vscode-badge-background);
                color: var(--vscode-badge-foreground);
                padding: 2px 8px;
                border-radius: 10px;
                font-size: 12px;
                margin-right: 10px;
            }
            .result-name {
                font-weight: bold;
                font-size: 16px;
                flex: 1;
                color: var(--vscode-symbolIcon-methodForeground);
            }
            .similarity-score {
                background-color: var(--vscode-button-background);
                color: var(--vscode-button-foreground);
                padding: 4px 12px;
                border-radius: 12px;
                font-size: 12px;
                font-weight: bold;
            }
            .result-type {
                color: var(--vscode-descriptionForeground);
                font-size: 12px;
                margin-bottom: 4px;
            }
            .result-file {
                color: var(--vscode-textLink-foreground);
                font-size: 13px;
                margin-bottom: 8px;
                font-family: monospace;
            }
            .result-summary {
                margin-top: 8px;
                line-height: 1.5;
                color: var(--vscode-editor-foreground);
            }
            .no-results {
                text-align: center;
                color: var(--vscode-descriptionForeground);
                padding: 40px;
            }
        </style>
    </head>
    <body>
        <h1>Semantic Search Results</h1>
        <p>Query: <span class="query">"${query}"</span></p>
        <p>Found ${results.length} results</p>
        <div class="results">
            ${results.length > 0 ? resultItems : '<div class="no-results">No results found</div>'}
        </div>
    </body>
    </html>`;
}

async function showSummaries() {
    vscode.commands.executeCommand('workbench.view.extension.sourcecodeSummariser');
}

async function toggleWatchMode() {
    const workspaceFolders = vscode.workspace.workspaceFolders;
    if (!workspaceFolders) {
        vscode.window.showErrorMessage('No workspace folder open');
        return;
    }

    const folder = workspaceFolders[0].uri.fsPath;

    if (watchModeActive) {
        // Stop watch mode
        await summarizerService.stopWatch();
        watchModeActive = false;
        vscode.window.showInformationMessage('Watch mode stopped');
    } else {
        // Start watch mode
        const result = await summarizerService.startWatch(folder, async () => {
            await summariesTreeProvider.refresh();
        });

        if (result.success) {
            watchModeActive = true;
            vscode.window.showInformationMessage('Watch mode enabled - monitoring for changes...');
        } else {
            vscode.window.showErrorMessage(`Failed to start watch mode: ${result.error}`);
        }
    }
}

async function initializeProject() {
    const workspaceFolders = vscode.workspace.workspaceFolders;
    if (!workspaceFolders) {
        vscode.window.showErrorMessage('No workspace folder open');
        return;
    }

    const folder = workspaceFolders[0].uri.fsPath;

    await vscode.window.withProgress({
        location: vscode.ProgressLocation.Notification,
        title: 'Initializing project...',
        cancellable: false
    }, async () => {
        try {
            const result = await summarizerService.initializeProject(folder);

            if (result.success) {
                vscode.window.showInformationMessage('Project initialized successfully!');
            } else {
                vscode.window.showErrorMessage(`Initialization failed: ${result.error}`);
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Initialization error: ${error}`);
        }
    });
}

async function showMemberDetails(member: any) {
    const panel = vscode.window.createWebviewPanel(
        'memberDetails',
        member.name,
        vscode.ViewColumn.One,
        { enableScripts: false }
    );

    panel.webview.html = getMemberDetailsHtml(member);
}

function getMemberDetailsHtml(member: any): string {
    const icon = getIconForType(member.type);

    return `<!DOCTYPE html>
    <html lang="en">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>${member.name}</title>
        <style>
            body {
                padding: 20px;
                font-family: var(--vscode-font-family);
                color: var(--vscode-foreground);
                background-color: var(--vscode-editor-background);
            }
            .header {
                border-bottom: 2px solid var(--vscode-panel-border);
                padding-bottom: 15px;
                margin-bottom: 20px;
            }
            .member-name {
                font-size: 24px;
                font-weight: bold;
                color: var(--vscode-symbolIcon-methodForeground);
            }
            .member-type {
                display: inline-block;
                background-color: var(--vscode-badge-background);
                color: var(--vscode-badge-foreground);
                padding: 4px 12px;
                border-radius: 12px;
                font-size: 14px;
                margin-top: 10px;
            }
            .section {
                margin: 20px 0;
            }
            .section-title {
                font-size: 16px;
                font-weight: bold;
                margin-bottom: 10px;
                color: var(--vscode-textLink-foreground);
            }
            .summary {
                line-height: 1.6;
                padding: 15px;
                background-color: var(--vscode-editor-inactiveSelectionBackground);
                border-left: 3px solid var(--vscode-textLink-foreground);
                border-radius: 4px;
            }
            .file-path {
                font-family: monospace;
                color: var(--vscode-textLink-foreground);
                font-size: 13px;
            }
            .tags {
                margin-top: 10px;
            }
            .tag {
                display: inline-block;
                background-color: var(--vscode-button-secondaryBackground);
                color: var(--vscode-button-secondaryForeground);
                padding: 4px 10px;
                border-radius: 8px;
                margin-right: 8px;
                margin-bottom: 8px;
                font-size: 12px;
            }
        </style>
    </head>
    <body>
        <div class="header">
            <div class="member-name">${icon} ${member.name}</div>
            <div class="member-type">${member.type}</div>
        </div>

        <div class="section">
            <div class="section-title">📄 File</div>
            <div class="file-path">${member.fileName}</div>
        </div>

        <div class="section">
            <div class="section-title">📝 Summary</div>
            <div class="summary">${member.summary}</div>
        </div>

        ${member.tags && member.tags.length > 0 ? `
        <div class="section">
            <div class="section-title">🏷️ Tags</div>
            <div class="tags">
                ${member.tags.map((tag: string) => `<span class="tag">${tag}</span>`).join('')}
            </div>
        </div>
        ` : ''}
    </body>
    </html>`;
}

function getIconForType(type: string): string {
    switch (type.toLowerCase()) {
        case 'method':
            return '⚙️';
        case 'class':
            return '📦';
        case 'interface':
            return '🔌';
        case 'property':
            return '🔧';
        case 'field':
            return '📊';
        case 'struct':
            return '🏗️';
        case 'namespace':
            return '📁';
        default:
            return '📄';
    }
}

export function deactivate() {
    if (watchModeActive) {
        summarizerService.stopWatch();
    }
    databaseService.close();
}
