import * as vscode from 'vscode';
import * as path from 'path';
import * as child_process from 'child_process';
import * as fs from 'fs';

export interface AnalysisResult {
    success: boolean;
    filesProcessed?: number;
    error?: string;
}

export interface SearchResult {
    success: boolean;
    results?: any[];
    error?: string;
}

export class SummarizerService {
    private watchProcess?: child_process.ChildProcess;
    private toolPath?: string;

    constructor() {
        this.detectToolPath();
    }

    private detectToolPath() {
        const config = vscode.workspace.getConfiguration('sourcecodeSummariser');
        const configuredPath = config.get<string>('toolPath');

        if (configuredPath && fs.existsSync(configuredPath)) {
            this.toolPath = configuredPath;
            return;
        }

        // Try to find the tool in common locations
        const workspaceFolders = vscode.workspace.workspaceFolders;
        if (!workspaceFolders) {
            return;
        }

        const possiblePaths = [
            path.join(workspaceFolders[0].uri.fsPath, 'bin', 'Debug', 'net8.0', 'SourceCodeSummariser'),
            path.join(workspaceFolders[0].uri.fsPath, 'bin', 'Release', 'net8.0', 'SourceCodeSummariser'),
            path.join(workspaceFolders[0].uri.fsPath, '..', 'bin', 'Debug', 'net8.0', 'SourceCodeSummariser'),
            path.join(workspaceFolders[0].uri.fsPath, '..', 'bin', 'Release', 'net8.0', 'SourceCodeSummariser')
        ];

        for (const testPath of possiblePaths) {
            if (fs.existsSync(testPath) || fs.existsSync(testPath + '.dll')) {
                this.toolPath = testPath;
                break;
            }
        }
    }

    private async runTool(args: string[], onOutput?: (message: string) => void, cancellationToken?: vscode.CancellationToken): Promise<AnalysisResult> {
        return new Promise((resolve) => {
            if (!this.toolPath) {
                // Try using dotnet run as fallback
                const workspaceFolders = vscode.workspace.workspaceFolders;
                if (!workspaceFolders) {
                    resolve({ success: false, error: 'Tool path not configured and could not auto-detect' });
                    return;
                }

                // Look for the project directory
                const projectDir = this.findProjectDirectory(workspaceFolders[0].uri.fsPath);
                if (!projectDir) {
                    resolve({ success: false, error: 'Could not find SourceCodeSummariser project. Please configure the tool path.' });
                    return;
                }

                this.runWithDotnet(projectDir, args, onOutput, cancellationToken, resolve);
                return;
            }

            // Run the compiled executable
            const process = child_process.spawn(this.toolPath, args);
            this.handleProcess(process, onOutput, cancellationToken, resolve);
        });
    }

    private findProjectDirectory(startPath: string): string | null {
        // Look for SourceCodeSummariser.csproj
        let currentDir = startPath;

        for (let i = 0; i < 5; i++) {  // Search up to 5 levels
            const projectFile = path.join(currentDir, 'SourceCodeSummariser.csproj');
            if (fs.existsSync(projectFile)) {
                return currentDir;
            }

            const parentDir = path.dirname(currentDir);
            if (parentDir === currentDir) {
                break;  // Reached root
            }
            currentDir = parentDir;
        }

        return null;
    }

    private runWithDotnet(projectDir: string, args: string[], onOutput?: (message: string) => void, cancellationToken?: vscode.CancellationToken, resolve?: (result: AnalysisResult) => void) {
        const process = child_process.spawn('dotnet', ['run', '--project', projectDir, '--', ...args]);
        if (resolve) {
            this.handleProcess(process, onOutput, cancellationToken, resolve);
        }
    }

    private handleProcess(process: child_process.ChildProcess, onOutput: ((message: string) => void) | undefined, cancellationToken: vscode.CancellationToken | undefined, resolve: (result: AnalysisResult) => void) {
        let output = '';
        let errorOutput = '';
        let filesProcessed = 0;

        process.stdout?.on('data', (data) => {
            const message = data.toString();
            output += message;

            if (onOutput) {
                onOutput(message);
            }

            // Try to extract file count from output
            const match = message.match(/Processed (\d+) files?/i);
            if (match) {
                filesProcessed = parseInt(match[1]);
            }
        });

        process.stderr?.on('data', (data) => {
            errorOutput += data.toString();
        });

        if (cancellationToken) {
            cancellationToken.onCancellationRequested(() => {
                process.kill();
                resolve({ success: false, error: 'Cancelled by user' });
            });
        }

        process.on('close', (code) => {
            if (code === 0) {
                resolve({ success: true, filesProcessed });
            } else {
                resolve({
                    success: false,
                    error: errorOutput || `Process exited with code ${code}`
                });
            }
        });

        process.on('error', (error) => {
            resolve({
                success: false,
                error: `Failed to start process: ${error.message}`
            });
        });
    }

    async analyzeWorkspace(folderPath: string, onProgress?: (message: string) => void, cancellationToken?: vscode.CancellationToken): Promise<AnalysisResult> {
        return this.runTool([folderPath], onProgress, cancellationToken);
    }

    async analyzeFile(filePath: string): Promise<AnalysisResult> {
        // For single file analysis, we still need to analyze the workspace
        // but the tool will detect changes and only process modified files
        const workspaceFolder = vscode.workspace.getWorkspaceFolder(vscode.Uri.file(filePath));
        if (!workspaceFolder) {
            return { success: false, error: 'File is not in workspace' };
        }

        return this.runTool([workspaceFolder.uri.fsPath]);
    }

    async semanticSearch(query: string, topK: number = 10): Promise<SearchResult> {
        return new Promise((resolve) => {
            const workspaceFolders = vscode.workspace.workspaceFolders;
            if (!workspaceFolders) {
                resolve({ success: false, error: 'No workspace folder open' });
                return;
            }

            // Run the search program
            const projectDir = this.findProjectDirectory(workspaceFolders[0].uri.fsPath);
            if (!projectDir) {
                resolve({ success: false, error: 'Could not find SourceCodeSummariser project' });
                return;
            }

            const searchProgramPath = path.join(projectDir, 'SearchProgram.cs');
            if (!fs.existsSync(searchProgramPath)) {
                resolve({ success: false, error: 'Search program not found' });
                return;
            }

            // Build command to run search
            const dbPath = path.join(workspaceFolders[0].uri.fsPath, 'summaries.db');
            const args = ['run', '--project', projectDir, 'SearchProgram.cs', '--', query, topK.toString(), dbPath];

            const process = child_process.spawn('dotnet', args);
            let output = '';
            let errorOutput = '';

            process.stdout?.on('data', (data) => {
                output += data.toString();
            });

            process.stderr?.on('data', (data) => {
                errorOutput += data.toString();
            });

            process.on('close', (code) => {
                if (code === 0) {
                    try {
                        // Parse the output for results
                        const results = this.parseSearchOutput(output);
                        resolve({ success: true, results });
                    } catch (error) {
                        resolve({ success: false, error: `Failed to parse results: ${error}` });
                    }
                } else {
                    resolve({
                        success: false,
                        error: errorOutput || `Search failed with code ${code}`
                    });
                }
            });
        });
    }

    private parseSearchOutput(output: string): any[] {
        // Parse the console output from SearchProgram
        const results: any[] = [];
        const lines = output.split('\n');

        let currentResult: any = {};
        for (const line of lines) {
            if (line.includes('Member:')) {
                if (currentResult.memberName) {
                    results.push(currentResult);
                }
                currentResult = {
                    memberName: line.split('Member:')[1]?.trim()
                };
            } else if (line.includes('Type:')) {
                currentResult.memberType = line.split('Type:')[1]?.trim();
            } else if (line.includes('File:')) {
                currentResult.fileName = line.split('File:')[1]?.trim();
            } else if (line.includes('Similarity:')) {
                const match = line.match(/(\d+\.?\d*)%/);
                if (match) {
                    currentResult.similarity = parseFloat(match[1]) / 100;
                }
            } else if (line.includes('Summary:')) {
                currentResult.summary = line.split('Summary:')[1]?.trim();
            }
        }

        if (currentResult.memberName) {
            results.push(currentResult);
        }

        return results;
    }

    async startWatch(folderPath: string, onChange: () => Promise<void>): Promise<AnalysisResult> {
        return new Promise((resolve) => {
            if (this.watchProcess) {
                resolve({ success: false, error: 'Watch mode already active' });
                return;
            }

            const projectDir = this.findProjectDirectory(folderPath);
            if (!projectDir) {
                resolve({ success: false, error: 'Could not find SourceCodeSummariser project' });
                return;
            }

            this.watchProcess = child_process.spawn('dotnet', ['run', '--project', projectDir, '--', folderPath, '--watch']);

            this.watchProcess.stdout?.on('data', (data) => {
                const message = data.toString();
                console.log('Watch:', message);

                // If file was processed, refresh the tree
                if (message.includes('Processed') || message.includes('Updated')) {
                    onChange();
                }
            });

            this.watchProcess.on('error', (error) => {
                vscode.window.showErrorMessage(`Watch mode error: ${error.message}`);
                this.watchProcess = undefined;
            });

            this.watchProcess.on('close', () => {
                this.watchProcess = undefined;
            });

            resolve({ success: true });
        });
    }

    async stopWatch(): Promise<void> {
        if (this.watchProcess) {
            this.watchProcess.kill();
            this.watchProcess = undefined;
        }
    }

    async initializeProject(folderPath: string): Promise<AnalysisResult> {
        return this.runTool(['--init', folderPath]);
    }
}
