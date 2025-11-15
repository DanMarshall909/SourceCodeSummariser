import * as vscode from 'vscode';
import * as path from 'path';
import { DatabaseService, FileGroup, CodeMember } from './databaseService';

export class SummariesTreeProvider implements vscode.TreeDataProvider<TreeItem> {
    private _onDidChangeTreeData: vscode.EventEmitter<TreeItem | undefined | null | void> = new vscode.EventEmitter<TreeItem | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<TreeItem | undefined | null | void> = this._onDidChangeTreeData.event;

    constructor(private databaseService: DatabaseService) { }

    refresh(): void {
        this.databaseService.refresh();
        this._onDidChangeTreeData.fire();
    }

    getTreeItem(element: TreeItem): vscode.TreeItem {
        return element;
    }

    async getChildren(element?: TreeItem): Promise<TreeItem[]> {
        if (!element) {
            // Root level - show statistics and files
            const stats = await this.databaseService.getStatistics();

            if (stats.totalMembers === 0) {
                return [new TreeItem('No summaries found. Run "Analyze Workspace" to generate.', vscode.TreeItemCollapsibleState.None, 'info')];
            }

            const items: TreeItem[] = [
                new TreeItem(`📊 ${stats.totalFiles} files, ${stats.totalMembers} members`, vscode.TreeItemCollapsibleState.None, 'stats')
            ];

            // Get all files
            const fileGroups = await this.databaseService.getAllMembers();
            for (const group of fileGroups) {
                const fileName = path.basename(group.fileName);
                const item = new TreeItem(fileName, vscode.TreeItemCollapsibleState.Collapsed, 'file');
                item.tooltip = group.fileName;
                item.description = `${group.members.length} members`;
                item.contextValue = 'file';
                item.resourceUri = vscode.Uri.file(group.fileName);
                item.fileGroup = group;
                items.push(item);
            }

            return items;
        } else if (element.contextValue === 'file' && element.fileGroup) {
            // Show members in the file
            return element.fileGroup.members.map(member => {
                const icon = this.getIconForMemberType(member.type);
                const item = new TreeItem(`${icon} ${member.name}`, vscode.TreeItemCollapsibleState.None, 'member');
                item.tooltip = new vscode.MarkdownString(`**${member.type}**: ${member.name}\n\n${member.summary}`);
                item.description = member.type;
                item.contextValue = 'member';
                item.member = member;

                // Make it clickable to show details
                item.command = {
                    command: 'sourcecodeSummariser.showMemberDetails',
                    title: 'Show Details',
                    arguments: [member]
                };

                return item;
            });
        }

        return [];
    }

    private getIconForMemberType(type: string): string {
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
}

class TreeItem extends vscode.TreeItem {
    public fileGroup?: FileGroup;
    public member?: CodeMember;
    public itemType: string;

    constructor(
        public readonly label: string,
        public readonly collapsibleState: vscode.TreeItemCollapsibleState,
        itemType: string
    ) {
        super(label, collapsibleState);
        this.itemType = itemType;

        if (itemType === 'file') {
            this.iconPath = new vscode.ThemeIcon('file-code');
        } else if (itemType === 'member') {
            this.iconPath = new vscode.ThemeIcon('symbol-method');
        } else if (itemType === 'info') {
            this.iconPath = new vscode.ThemeIcon('info');
        } else if (itemType === 'stats') {
            this.iconPath = new vscode.ThemeIcon('graph');
        }
    }
}
