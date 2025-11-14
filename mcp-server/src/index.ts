#!/usr/bin/env node

import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  Tool,
} from "@modelcontextprotocol/sdk/types.js";
import axios from "axios";

// Configuration
const API_BASE_URL = process.env.SUMMARISER_API_URL || "http://localhost:5000";

// Types for API responses
interface SearchResult {
  memberName: string;
  memberType: string;
  summary: string;
  similarity: number;
  filePath: string;
  tags: string[];
}

interface MemberDetails {
  id: number;
  name: string;
  type: string;
  summary: string;
  filePath: string;
  tags: string[];
}

interface TagInfo {
  name: string;
  category: string;
  count: number;
}

interface FileSummary {
  fileName: string;
  members: Array<{
    name: string;
    type: string;
    summary: string;
    tags: string[];
  }>;
}

// Helper function to make API calls
async function callApi<T>(endpoint: string, params?: Record<string, any>): Promise<T> {
  try {
    const response = await axios.get(`${API_BASE_URL}${endpoint}`, { params });
    return response.data;
  } catch (error: any) {
    if (error.response) {
      throw new Error(`API Error: ${error.response.status} - ${error.response.data}`);
    }
    throw new Error(`Failed to connect to Summariser API at ${API_BASE_URL}: ${error.message}`);
  }
}

// Define MCP tools
const tools: Tool[] = [
  {
    name: "search_code",
    description: "Perform semantic search across the codebase to find relevant code members. Returns code with summaries ranked by similarity to your query.",
    inputSchema: {
      type: "object",
      properties: {
        query: {
          type: "string",
          description: "The search query describing what you're looking for (e.g., 'authentication logic', 'database connection')",
        },
        topK: {
          type: "number",
          description: "Maximum number of results to return (default: 10)",
          default: 10,
        },
        minSimilarity: {
          type: "number",
          description: "Minimum similarity threshold 0.0-1.0 (default: 0.7)",
          default: 0.7,
        },
      },
      required: ["query"],
    },
  },
  {
    name: "search_code_with_tags",
    description: "Search code with tag filtering. Combine semantic search with structured tags to narrow results (e.g., find 'validation' code tagged as 'public').",
    inputSchema: {
      type: "object",
      properties: {
        query: {
          type: "string",
          description: "The search query",
        },
        tags: {
          type: "array",
          items: { type: "string" },
          description: "Tags to filter by (e.g., ['public', 'async'])",
        },
        topK: {
          type: "number",
          description: "Maximum number of results (default: 10)",
          default: 10,
        },
        minSimilarity: {
          type: "number",
          description: "Minimum similarity threshold (default: 0.7)",
          default: 0.7,
        },
      },
      required: ["query", "tags"],
    },
  },
  {
    name: "find_similar_code",
    description: "Find code members similar to a specific member by ID. Useful for discovering related implementations or duplicated code patterns.",
    inputSchema: {
      type: "object",
      properties: {
        memberId: {
          type: "number",
          description: "The ID of the member to find similar code for",
        },
        topK: {
          type: "number",
          description: "Maximum number of results (default: 10)",
          default: 10,
        },
        minSimilarity: {
          type: "number",
          description: "Minimum similarity threshold (default: 0.7)",
          default: 0.7,
        },
      },
      required: ["memberId"],
    },
  },
  {
    name: "get_member_details",
    description: "Get detailed information about a specific code member by ID including its summary, type, file path, and tags.",
    inputSchema: {
      type: "object",
      properties: {
        memberId: {
          type: "number",
          description: "The ID of the member to retrieve",
        },
      },
      required: ["memberId"],
    },
  },
  {
    name: "list_tags",
    description: "List all available tags in the codebase with their categories and usage counts. Useful for discovering what tags are available for filtering.",
    inputSchema: {
      type: "object",
      properties: {},
    },
  },
  {
    name: "get_file_summary",
    description: "Get all code member summaries for a specific file. Returns comprehensive documentation for an entire file.",
    inputSchema: {
      type: "object",
      properties: {
        filePath: {
          type: "string",
          description: "Relative path to the file (e.g., 'Services/AuthService.cs')",
        },
      },
      required: ["filePath"],
    },
  },
  {
    name: "process_file",
    description: "Process and summarize a new or updated file. This analyzes the code, generates AI summaries, creates embeddings, and stores them in the database.",
    inputSchema: {
      type: "object",
      properties: {
        filePath: {
          type: "string",
          description: "Absolute path to the file to process",
        },
      },
      required: ["filePath"],
    },
  },
];

// Create server instance
const server = new Server(
  {
    name: "source-code-summariser",
    version: "1.0.0",
  },
  {
    capabilities: {
      tools: {},
    },
  }
);

// Handle tool listing
server.setRequestHandler(ListToolsRequestSchema, async () => {
  return { tools };
});

// Handle tool execution
server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  try {
    switch (name) {
      case "search_code": {
        const { query, topK = 10, minSimilarity = 0.7 } = args as {
          query: string;
          topK?: number;
          minSimilarity?: number;
        };

        const results = await callApi<SearchResult[]>("/api/search", {
          query,
          topK,
          minSimilarity,
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(results, null, 2),
            },
          ],
        };
      }

      case "search_code_with_tags": {
        const { query, tags, topK = 10, minSimilarity = 0.7 } = args as {
          query: string;
          tags: string[];
          topK?: number;
          minSimilarity?: number;
        };

        const results = await callApi<SearchResult[]>("/api/search/tags", {
          query,
          tags: tags.join(","),
          topK,
          minSimilarity,
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(results, null, 2),
            },
          ],
        };
      }

      case "find_similar_code": {
        const { memberId, topK = 10, minSimilarity = 0.7 } = args as {
          memberId: number;
          topK?: number;
          minSimilarity?: number;
        };

        const results = await callApi<SearchResult[]>("/api/search/similar", {
          memberId,
          topK,
          minSimilarity,
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(results, null, 2),
            },
          ],
        };
      }

      case "get_member_details": {
        const { memberId } = args as { memberId: number };

        const member = await callApi<MemberDetails>(`/api/members/${memberId}`);

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(member, null, 2),
            },
          ],
        };
      }

      case "list_tags": {
        const tags = await callApi<TagInfo[]>("/api/tags");

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(tags, null, 2),
            },
          ],
        };
      }

      case "get_file_summary": {
        const { filePath } = args as { filePath: string };

        const summary = await callApi<FileSummary>("/api/files/summary", {
          filePath,
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(summary, null, 2),
            },
          ],
        };
      }

      case "process_file": {
        const { filePath } = args as { filePath: string };

        const result = await callApi<{ success: boolean; message: string }>(
          "/api/files/process",
          { filePath }
        );

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(result, null, 2),
            },
          ],
        };
      }

      default:
        throw new Error(`Unknown tool: ${name}`);
    }
  } catch (error: any) {
    return {
      content: [
        {
          type: "text",
          text: `Error: ${error.message}`,
        },
      ],
      isError: true,
    };
  }
});

// Start server
async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);

  console.error("Source Code Summariser MCP Server running on stdio");
  console.error(`API endpoint: ${API_BASE_URL}`);
}

main().catch((error) => {
  console.error("Fatal error:", error);
  process.exit(1);
});
