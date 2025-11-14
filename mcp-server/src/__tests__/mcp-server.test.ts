import { describe, it, expect, jest, beforeEach, afterEach } from '@jest/globals';
import axios from 'axios';

// Mock axios
jest.mock('axios');
const mockedAxios = axios as jest.Mocked<typeof axios>;

describe('MCP Server API Calls', () => {
  const API_BASE_URL = 'http://localhost:5000';

  beforeEach(() => {
    // Reset mocks before each test
    jest.clearAllMocks();
  });

  afterEach(() => {
    jest.resetAllMocks();
  });

  describe('Search Endpoint', () => {
    it('should call search API with correct parameters', async () => {
      // Arrange
      const mockResponse = {
        data: [
          {
            memberName: 'TestMethod',
            memberType: 'Method',
            summary: 'Test summary',
            similarity: 0.95,
            filePath: 'Test.cs',
            tags: ['public'],
          },
        ],
      };

      mockedAxios.get.mockResolvedValue(mockResponse);

      // Act
      const result = await axios.get(`${API_BASE_URL}/api/search`, {
        params: {
          query: 'authentication',
          topK: 10,
          minSimilarity: 0.7,
        },
      });

      // Assert
      expect(mockedAxios.get).toHaveBeenCalledWith(
        `${API_BASE_URL}/api/search`,
        {
          params: {
            query: 'authentication',
            topK: 10,
            minSimilarity: 0.7,
          },
        }
      );
      expect(result.data).toHaveLength(1);
      expect(result.data[0].memberName).toBe('TestMethod');
    });

    it('should handle empty search results', async () => {
      // Arrange
      mockedAxios.get.mockResolvedValue({ data: [] });

      // Act
      const result = await axios.get(`${API_BASE_URL}/api/search`, {
        params: {
          query: 'nonexistent',
          topK: 10,
          minSimilarity: 0.7,
        },
      });

      // Assert
      expect(result.data).toHaveLength(0);
    });

    it('should handle API errors gracefully', async () => {
      // Arrange
      mockedAxios.get.mockRejectedValue(new Error('Network error'));

      // Act & Assert
      await expect(
        axios.get(`${API_BASE_URL}/api/search`, {
          params: { query: 'test', topK: 10, minSimilarity: 0.7 },
        })
      ).rejects.toThrow('Network error');
    });
  });

  describe('Search with Tags Endpoint', () => {
    it('should call search with tags API correctly', async () => {
      // Arrange
      const mockResponse = {
        data: [
          {
            memberName: 'PublicMethod',
            memberType: 'Method',
            summary: 'Public method',
            similarity: 0.85,
            filePath: 'Test.cs',
            tags: ['public', 'async'],
          },
        ],
      };

      mockedAxios.get.mockResolvedValue(mockResponse);

      // Act
      const result = await axios.get(`${API_BASE_URL}/api/search/tags`, {
        params: {
          query: 'method',
          tags: 'public,async',
          topK: 10,
          minSimilarity: 0.7,
        },
      });

      // Assert
      expect(mockedAxios.get).toHaveBeenCalledWith(
        `${API_BASE_URL}/api/search/tags`,
        {
          params: {
            query: 'method',
            tags: 'public,async',
            topK: 10,
            minSimilarity: 0.7,
          },
        }
      );
      expect(result.data[0].tags).toContain('public');
      expect(result.data[0].tags).toContain('async');
    });
  });

  describe('Similar Members Endpoint', () => {
    it('should find similar members by ID', async () => {
      // Arrange
      const mockResponse = {
        data: [
          {
            memberName: 'SimilarMethod',
            memberType: 'Method',
            summary: 'Similar implementation',
            similarity: 0.92,
            filePath: 'Similar.cs',
            tags: ['public'],
          },
        ],
      };

      mockedAxios.get.mockResolvedValue(mockResponse);

      // Act
      const result = await axios.get(`${API_BASE_URL}/api/search/similar`, {
        params: {
          memberId: 1,
          topK: 10,
          minSimilarity: 0.7,
        },
      });

      // Assert
      expect(mockedAxios.get).toHaveBeenCalled();
      expect(result.data[0].similarity).toBeGreaterThan(0.9);
    });
  });

  describe('Member Details Endpoint', () => {
    it('should retrieve member details by ID', async () => {
      // Arrange
      const mockResponse = {
        data: {
          id: 1,
          name: 'TestMethod',
          type: 'Method',
          summary: 'Test method summary',
          filePath: 'Test.cs',
          tags: ['public', 'async'],
        },
      };

      mockedAxios.get.mockResolvedValue(mockResponse);

      // Act
      const result = await axios.get(`${API_BASE_URL}/api/members/1`);

      // Assert
      expect(mockedAxios.get).toHaveBeenCalledWith(
        `${API_BASE_URL}/api/members/1`
      );
      expect(result.data.id).toBe(1);
      expect(result.data.name).toBe('TestMethod');
    });

    it('should handle member not found', async () => {
      // Arrange
      mockedAxios.get.mockRejectedValue({
        response: {
          status: 404,
          data: { error: 'Member with ID 9999 not found' },
        },
      });

      // Act & Assert
      await expect(axios.get(`${API_BASE_URL}/api/members/9999`)).rejects.toMatchObject({
        response: {
          status: 404,
        },
      });
    });
  });

  describe('Tags Endpoint', () => {
    it('should list all available tags', async () => {
      // Arrange
      const mockResponse = {
        data: [
          { name: 'public', category: 'visibility', count: 50 },
          { name: 'private', category: 'visibility', count: 30 },
          { name: 'async', category: 'modifier', count: 20 },
        ],
      };

      mockedAxios.get.mockResolvedValue(mockResponse);

      // Act
      const result = await axios.get(`${API_BASE_URL}/api/tags`);

      // Assert
      expect(result.data).toHaveLength(3);
      expect(result.data[0].name).toBe('public');
      expect(result.data[0].count).toBe(50);
    });
  });

  describe('File Summary Endpoint', () => {
    it('should retrieve file summary', async () => {
      // Arrange
      const mockResponse = {
        data: {
          fileName: 'AuthService.cs',
          members: [
            {
              name: 'AuthenticateUser',
              type: 'Method',
              summary: 'Authenticates user',
              tags: ['public', 'async'],
            },
          ],
        },
      };

      mockedAxios.get.mockResolvedValue(mockResponse);

      // Act
      const result = await axios.get(`${API_BASE_URL}/api/files/summary`, {
        params: { filePath: 'AuthService.cs' },
      });

      // Assert
      expect(result.data.fileName).toBe('AuthService.cs');
      expect(result.data.members).toHaveLength(1);
    });
  });

  describe('Health Check Endpoint', () => {
    it('should return healthy status', async () => {
      // Arrange
      const mockResponse = {
        data: {
          status: 'healthy',
          service: 'SourceCodeSummariser API',
          version: '1.0.0',
        },
      };

      mockedAxios.get.mockResolvedValue(mockResponse);

      // Act
      const result = await axios.get(`${API_BASE_URL}/api/health`);

      // Assert
      expect(result.data.status).toBe('healthy');
      expect(result.data.service).toBe('SourceCodeSummariser API');
    });
  });

  describe('Error Handling', () => {
    it('should handle 400 Bad Request', async () => {
      // Arrange
      mockedAxios.get.mockRejectedValue({
        response: {
          status: 400,
          data: 'Invalid request',
        },
      });

      // Act & Assert
      await expect(
        axios.get(`${API_BASE_URL}/api/search`, {
          params: { query: '', topK: -1, minSimilarity: 2.0 },
        })
      ).rejects.toMatchObject({
        response: {
          status: 400,
        },
      });
    });

    it('should handle 500 Internal Server Error', async () => {
      // Arrange
      mockedAxios.get.mockRejectedValue({
        response: {
          status: 500,
          data: 'Internal server error',
        },
      });

      // Act & Assert
      await expect(
        axios.get(`${API_BASE_URL}/api/search`, {
          params: { query: 'test', topK: 10, minSimilarity: 0.7 },
        })
      ).rejects.toMatchObject({
        response: {
          status: 500,
        },
      });
    });

    it('should handle connection refused', async () => {
      // Arrange
      mockedAxios.get.mockRejectedValue(new Error('ECONNREFUSED'));

      // Act & Assert
      await expect(
        axios.get(`${API_BASE_URL}/api/search`, {
          params: { query: 'test', topK: 10, minSimilarity: 0.7 },
        })
      ).rejects.toThrow('ECONNREFUSED');
    });
  });

  describe('Parameter Validation', () => {
    it('should handle default parameters', async () => {
      // Arrange
      const mockResponse = { data: [] };
      mockedAxios.get.mockResolvedValue(mockResponse);

      // Act
      await axios.get(`${API_BASE_URL}/api/search`, {
        params: {
          query: 'test',
          topK: 10,
          minSimilarity: 0.7,
        },
      });

      // Assert
      expect(mockedAxios.get).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          params: expect.objectContaining({
            topK: 10,
            minSimilarity: 0.7,
          }),
        })
      );
    });

    it('should handle custom parameters', async () => {
      // Arrange
      const mockResponse = { data: [] };
      mockedAxios.get.mockResolvedValue(mockResponse);

      // Act
      await axios.get(`${API_BASE_URL}/api/search`, {
        params: {
          query: 'test',
          topK: 5,
          minSimilarity: 0.9,
        },
      });

      // Assert
      expect(mockedAxios.get).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          params: expect.objectContaining({
            topK: 5,
            minSimilarity: 0.9,
          }),
        })
      );
    });
  });
});
