using System.Security.Cryptography;
using System.Text;
using AiSupportApp.Models;

namespace AiSupportApp.Services
{
    public class RagPipeline
    {
        private readonly TextChunker _chunker;
        private readonly EmbeddingService _embeddingService;
        private readonly QdrantService _qdrantService;

        public RagPipeline(TextChunker chunker, EmbeddingService embeddingService, QdrantService qdrantService)
        {
            _chunker = chunker;
            _embeddingService = embeddingService;
            _qdrantService = qdrantService;
        }

        public async Task IngestAsync(string title, string category, string content, CancellationToken cancellationToken = default)
        {
            var contentHash = ComputeSha256(content);

            if (await _qdrantService.DocumentHashExistsAsync(contentHash, cancellationToken))
            {
                // Duplicate document, skip ingestion
                return;
            }

            var chunkTexts = _chunker.ChunkText(content);
            if (chunkTexts.Count == 0) return;

            var docId = Guid.NewGuid().ToString();
            var chunks = new List<DocumentChunk>();

            for (int i = 0; i < chunkTexts.Count; i++)
            {
                chunks.Add(new DocumentChunk
                {
                    DocId = docId,
                    ChunkIndex = i,
                    TotalChunks = chunkTexts.Count,
                    Title = title,
                    Category = category,
                    Content = chunkTexts[i],
                    ContentHash = contentHash
                });
            }

            var vectors = await _embeddingService.EmbedDocumentsAsync(chunkTexts, title, cancellationToken);
            await _qdrantService.UpsertChunksAsync(chunks, vectors, cancellationToken);
        }

        public async Task<string> AskAsync(string query, CancellationToken cancellationToken = default)
        {
            var queryVector = await _embeddingService.EmbedQueryAsync(query, cancellationToken);
            
            // Retrieve top 20 candidates
            var candidates = await _qdrantService.SearchAsync(queryVector, limit: 20, scoreThreshold: 0.65f, cancellationToken: cancellationToken);

            // Basic MMR: prioritize diversity by taking at most 2 chunks from the same document initially.
            var topChunks = ApplyBasicMmr(candidates, topK: 5);

            if (topChunks.Count == 0)
                return string.Empty;

            var contextBuilder = new StringBuilder();
            foreach (var chunk in topChunks)
            {
                contextBuilder.AppendLine($"<doc id=\"{chunk.DocId}#{chunk.ChunkIndex}\">");
                contextBuilder.AppendLine($"Title: {chunk.Title}");
                contextBuilder.AppendLine($"Category: {chunk.Category}");
                contextBuilder.AppendLine(chunk.Content);
                contextBuilder.AppendLine("</doc>");
            }

            return contextBuilder.ToString();
        }

        private List<DocumentChunk> ApplyBasicMmr(List<DocumentChunk> candidates, int topK)
        {
            var selected = new List<DocumentChunk>();
            var docCounts = new Dictionary<string, int>();

            foreach (var chunk in candidates)
            {
                if (selected.Count >= topK) break;

                docCounts.TryGetValue(chunk.DocId, out int count);
                
                // Allow max 2 chunks per document to enforce diversity
                if (count < 2)
                {
                    selected.Add(chunk);
                    docCounts[chunk.DocId] = count + 1;
                }
            }

            // Fill up remaining slots if needed
            if (selected.Count < topK)
            {
                foreach (var chunk in candidates)
                {
                    if (selected.Count >= topK) break;
                    if (!selected.Contains(chunk))
                    {
                        selected.Add(chunk);
                    }
                }
            }

            return selected;
        }

        private string ComputeSha256(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
