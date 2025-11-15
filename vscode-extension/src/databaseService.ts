import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import * as sqlite3 from 'sqlite3';

export interface CodeMember {
    id: number;
    name: string;
    type: string;
    summary: string;
    fileName: string;
    hash: string;
    tags?: string[];
}

export interface FileGroup {
    fileName: string;
    members: CodeMember[];
}

export class DatabaseService {
    private db?: sqlite3.Database;
    private dbPath?: string;

    constructor() {
        this.initializeDatabase();
    }

    private initializeDatabase() {
        const workspaceFolders = vscode.workspace.workspaceFolders;
        if (!workspaceFolders) {
            return;
        }

        const config = vscode.workspace.getConfiguration('sourcecodeSummariser');
        const dbFileName = config.get<string>('databasePath', 'summaries.db');
        this.dbPath = path.join(workspaceFolders[0].uri.fsPath, dbFileName);

        if (!fs.existsSync(this.dbPath)) {
            console.log('Database not found at:', this.dbPath);
            return;
        }

        this.db = new sqlite3.Database(this.dbPath, sqlite3.OPEN_READONLY, (err) => {
            if (err) {
                console.error('Failed to open database:', err);
            } else {
                console.log('Database opened successfully');
            }
        });
    }

    async getAllMembers(): Promise<FileGroup[]> {
        return new Promise((resolve, reject) => {
            if (!this.db) {
                this.initializeDatabase();
                if (!this.db) {
                    resolve([]);
                    return;
                }
            }

            const query = `
                SELECT
                    m.Id, m.Name, m.Type, m.Summary, m.Hash,
                    f.FileName
                FROM Members m
                INNER JOIN Files f ON m.FileEntityId = f.Id
                ORDER BY f.FileName, m.Name
            `;

            this.db!.all(query, [], (err, rows: any[]) => {
                if (err) {
                    console.error('Database query error:', err);
                    resolve([]);
                    return;
                }

                // Group by file
                const fileGroups = new Map<string, CodeMember[]>();

                for (const row of rows) {
                    const member: CodeMember = {
                        id: row.Id,
                        name: row.Name,
                        type: row.Type,
                        summary: row.Summary,
                        fileName: row.FileName,
                        hash: row.Hash
                    };

                    if (!fileGroups.has(row.FileName)) {
                        fileGroups.set(row.FileName, []);
                    }
                    fileGroups.get(row.FileName)!.push(member);
                }

                const result: FileGroup[] = Array.from(fileGroups.entries()).map(([fileName, members]) => ({
                    fileName,
                    members
                }));

                resolve(result);
            });
        });
    }

    async getMembersByFile(fileName: string): Promise<CodeMember[]> {
        return new Promise((resolve, reject) => {
            if (!this.db) {
                this.initializeDatabase();
                if (!this.db) {
                    resolve([]);
                    return;
                }
            }

            const query = `
                SELECT
                    m.Id, m.Name, m.Type, m.Summary, m.Hash,
                    f.FileName
                FROM Members m
                INNER JOIN Files f ON m.FileEntityId = f.Id
                WHERE f.FileName = ?
                ORDER BY m.Name
            `;

            this.db!.all(query, [fileName], (err, rows: any[]) => {
                if (err) {
                    console.error('Database query error:', err);
                    resolve([]);
                    return;
                }

                const members: CodeMember[] = rows.map(row => ({
                    id: row.Id,
                    name: row.Name,
                    type: row.Type,
                    summary: row.Summary,
                    fileName: row.FileName,
                    hash: row.Hash
                }));

                resolve(members);
            });
        });
    }

    async getMemberByNameAndFile(memberName: string, fileName: string): Promise<CodeMember | null> {
        return new Promise((resolve, reject) => {
            if (!this.db) {
                this.initializeDatabase();
                if (!this.db) {
                    resolve(null);
                    return;
                }
            }

            const query = `
                SELECT
                    m.Id, m.Name, m.Type, m.Summary, m.Hash,
                    f.FileName
                FROM Members m
                INNER JOIN Files f ON m.FileEntityId = f.Id
                WHERE m.Name = ? AND f.FileName = ?
                LIMIT 1
            `;

            this.db!.get(query, [memberName, fileName], (err, row: any) => {
                if (err || !row) {
                    resolve(null);
                    return;
                }

                const member: CodeMember = {
                    id: row.Id,
                    name: row.Name,
                    type: row.Type,
                    summary: row.Summary,
                    fileName: row.FileName,
                    hash: row.Hash
                };

                resolve(member);
            });
        });
    }

    async getStatistics(): Promise<{ totalFiles: number; totalMembers: number; totalTags: number }> {
        return new Promise((resolve) => {
            if (!this.db) {
                this.initializeDatabase();
                if (!this.db) {
                    resolve({ totalFiles: 0, totalMembers: 0, totalTags: 0 });
                    return;
                }
            }

            const queries = [
                'SELECT COUNT(*) as count FROM Files',
                'SELECT COUNT(*) as count FROM Members',
                'SELECT COUNT(*) as count FROM Tags'
            ];

            let results = { totalFiles: 0, totalMembers: 0, totalTags: 0 };
            let completed = 0;

            for (let i = 0; i < queries.length; i++) {
                this.db!.get(queries[i], [], (err, row: any) => {
                    if (!err && row) {
                        if (i === 0) {
                            results.totalFiles = row.count;
                        } else if (i === 1) {
                            results.totalMembers = row.count;
                        } else {
                            results.totalTags = row.count;
                        }
                    }
                    completed++;
                    if (completed === queries.length) {
                        resolve(results);
                    }
                });
            }
        });
    }

    close() {
        if (this.db) {
            this.db.close();
        }
    }

    refresh() {
        this.close();
        this.initializeDatabase();
    }
}
