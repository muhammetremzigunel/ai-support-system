using AiSupportApp.Models;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AiSupportApp.Services
{
    public class QdrantService
    {
        private readonly QdrantClient _client;
        private const string CollectionName = "support_chunks";
        private const int VectorSize = 3072;

        public QdrantService(QdrantClient client)
        {
            _client = client;
        }

        public async Task CreateCollectionIfNotExistsAsync(CancellationToken cancellationToken = default)
        {
            var collections = await _client.ListCollectionsAsync(cancellationToken: cancellationToken);
            if (!collections.Any(c => c == CollectionName))
            {
                await _client.CreateCollectionAsync(CollectionName,
                    new VectorParams
                    {
                        Size = VectorSize,
                        Distance = Distance.Cosine
                    }, cancellationToken: cancellationToken);

                // Create payload indexes for faster filtering
                await _client.CreatePayloadIndexAsync(CollectionName, "docId", PayloadSchemaType.Keyword, cancellationToken: cancellationToken);
                await _client.CreatePayloadIndexAsync(CollectionName, "category", PayloadSchemaType.Keyword, cancellationToken: cancellationToken);
                await _client.CreatePayloadIndexAsync(CollectionName, "contentHash", PayloadSchemaType.Keyword, cancellationToken: cancellationToken);
            }
        }

        public async Task UpsertChunksAsync(IReadOnlyList<DocumentChunk> chunks, IReadOnlyList<float[]> vectors, CancellationToken cancellationToken = default)
        {
            var points = new List<PointStruct>();
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var point = new PointStruct
                {
                    Id = new PointId { Uuid = Guid.NewGuid().ToString() }, // Each chunk gets its own point ID
                    Vectors = vectors[i]
                };

                point.Payload["docId"] = chunk.DocId;
                point.Payload["chunkIndex"] = chunk.ChunkIndex;
                point.Payload["totalChunks"] = chunk.TotalChunks;
                point.Payload["title"] = chunk.Title;
                point.Payload["category"] = chunk.Category;
                point.Payload["content"] = chunk.Content;
                point.Payload["contentHash"] = chunk.ContentHash;

                points.Add(point);
            }

            if (points.Count > 0)
            {
                try
                {
                    await _client.UpsertAsync(CollectionName, points, cancellationToken: cancellationToken);
                }
                catch (Grpc.Core.RpcException ex)
                {
                    throw new AiSupportApp.Exceptions.VectorDatabaseException(
                        "Vector DB Upsert Error",
                        "Dökümanlar veritabanına kaydedilemedi. Lütfen daha sonra tekrar deneyin.",
                        $"Qdrant Upsert Error: {ex.Status.Detail}",
                        AiSupportApp.Enums.ErrorSeverity.Transient,
                        ex);
                }
            }
        }

        // We delete by docId to remove all chunks belonging to a document
        public async Task DeleteDocumentAsync(string docId, CancellationToken cancellationToken = default)
        {
            var filter = new Filter
            {
                Must = { new Condition { Field = new FieldCondition { Key = "docId", Match = new Match { Keyword = docId } } } }
            };
            
            await _client.DeleteAsync(CollectionName, filter, cancellationToken: cancellationToken);
        }

        public async Task<bool> DocumentHashExistsAsync(string hash, CancellationToken cancellationToken = default)
        {
            var filter = new Filter
            {
                Must = { new Condition { Field = new FieldCondition { Key = "contentHash", Match = new Match { Keyword = hash } } } }
            };

            var countResult = await _client.CountAsync(CollectionName, filter, cancellationToken: cancellationToken);
            return countResult > 0;
        }

        public async Task<List<DocumentChunk>> SearchAsync(float[] vector, string? category = null, int limit = 20, float scoreThreshold = 0.65f, CancellationToken cancellationToken = default)
        {
            Filter? filter = null;
            if (!string.IsNullOrEmpty(category))
            {
                filter = new Filter
                {
                    Must = { new Condition { Field = new FieldCondition { Key = "category", Match = new Match { Keyword = category } } } }
                };
            }

            try
            {
                var results = await _client.SearchAsync(
                    CollectionName,
                    vector,
                    filter: filter,
                    limit: (ulong)limit,
                    scoreThreshold: scoreThreshold,
                    cancellationToken: cancellationToken
                );

                return results
                    .Where(r => r.Payload.ContainsKey("content"))
                    .Select(r => new DocumentChunk
                    {
                        DocId = r.Payload.ContainsKey("docId") ? r.Payload["docId"].StringValue : "",
                        ChunkIndex = r.Payload.ContainsKey("chunkIndex") ? (int)r.Payload["chunkIndex"].IntegerValue : 0,
                        TotalChunks = r.Payload.ContainsKey("totalChunks") ? (int)r.Payload["totalChunks"].IntegerValue : 0,
                        Title = r.Payload.ContainsKey("title") ? r.Payload["title"].StringValue : "",
                        Category = r.Payload.ContainsKey("category") ? r.Payload["category"].StringValue : "",
                        Content = r.Payload["content"].StringValue,
                        ContentHash = r.Payload.ContainsKey("contentHash") ? r.Payload["contentHash"].StringValue : ""
                    })
                    .ToList();
            }
            catch (Grpc.Core.RpcException ex)
            {
                throw new AiSupportApp.Exceptions.VectorDatabaseException(
                    "Vector DB Search Error",
                    "Arama işlemi sırasında veritabanına ulaşılamadı. Lütfen daha sonra tekrar deneyin.",
                    $"Qdrant Search Error: {ex.Status.Detail}",
                    AiSupportApp.Enums.ErrorSeverity.Transient,
                    ex);
            }
        }

        public async Task<List<DocumentModel>> GetAllDocumentsAsync(string? category = null, string? title = null, CancellationToken cancellationToken = default)
        {
            Filter? filter = null;
            var conditions = new List<Condition>();
            
            if (!string.IsNullOrEmpty(category))
                conditions.Add(new Condition { Field = new FieldCondition { Key = "category", Match = new Match { Keyword = category } } });
                
            if (!string.IsNullOrEmpty(title))
                conditions.Add(new Condition { Field = new FieldCondition { Key = "title", Match = new Match { Keyword = title } } });

            if (conditions.Count > 0)
            {
                filter = new Filter { Must = { conditions } };
            }

            var results = await _client.ScrollAsync(CollectionName, filter: filter, limit: 1000, cancellationToken: cancellationToken);

            // Group by DocId to reconstruct the logical documents for listing
            return results.Result
                .Where(r => r.Payload.ContainsKey("docId"))
                .GroupBy(r => r.Payload["docId"].StringValue)
                .Select(g => 
                {
                    var firstChunk = g.First();
                    return new DocumentModel
                    {
                        Id = g.Key,
                        Title = firstChunk.Payload.ContainsKey("title") ? firstChunk.Payload["title"].StringValue : "",
                        Category = firstChunk.Payload.ContainsKey("category") ? firstChunk.Payload["category"].StringValue : "",
                        Content = string.Join("\n\n", g.OrderBy(r => r.Payload.ContainsKey("chunkIndex") ? r.Payload["chunkIndex"].IntegerValue : 0)
                                                       .Select(r => r.Payload.ContainsKey("content") ? r.Payload["content"].StringValue : ""))
                    };
                })
                .ToList();
        }

        public async Task<List<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            var results = await _client.ScrollAsync(CollectionName, limit: 1000, cancellationToken: cancellationToken);

            return results.Result
                .Where(r => r.Payload.ContainsKey("category"))
                .Select(r => r.Payload["category"].StringValue)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        public async Task<List<string>> GetTitlesByCategoryAsync(string category, CancellationToken cancellationToken = default)
        {
            var filter = new Filter
            {
                Must = { new Condition { Field = new FieldCondition { Key = "category", Match = new Match { Keyword = category } } } }
            };

            var results = await _client.ScrollAsync(CollectionName, filter: filter, limit: 1000, cancellationToken: cancellationToken);

            return results.Result
                .Where(r => r.Payload.ContainsKey("title"))
                .Select(r => r.Payload["title"].StringValue)
                .Distinct()
                .OrderBy(t => t)
                .ToList();
        }
    }
}