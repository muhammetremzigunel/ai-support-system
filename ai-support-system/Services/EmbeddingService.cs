using System.Text.Json;
using System.Net.Http.Json;
using AiSupportApp.Exceptions;

namespace AiSupportApp.Services
{
    public class EmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _embeddingModel;

        public EmbeddingService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Gemini:ApiKey"];
            _embeddingModel = configuration["Gemini:EmbeddingModel"];
            
            // Move the API key to the header as per best practices
            if (!_httpClient.DefaultRequestHeaders.Contains("x-goog-api-key"))
            {
                _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", _apiKey);
            }
        }

        /// <summary>
        /// Embeds a user query. Uses RETRIEVAL_QUERY taskType for asymmetric projection.
        /// </summary>
        public async Task<float[]> EmbedQueryAsync(string query, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Query cannot be empty.");

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_embeddingModel}:embedContent";

            var request = new
            {
                model = $"models/{_embeddingModel}",
                content = new
                {
                    parts = new[] { new { text = query } }
                },
                taskType = "RETRIEVAL_QUERY"
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new AiServiceException(
                        "Embedding API Error",
                        "Yapay zeka servisi şu anda yanıt vermiyor. Lütfen daha sonra tekrar deneyin.",
                        $"Query embedding failed. Status: {response.StatusCode}, Body: {json}",
                        AiSupportApp.Enums.ErrorSeverity.Degraded);
                }

                var doc = JsonDocument.Parse(json);

                return doc.RootElement
                    .GetProperty("embedding")
                    .GetProperty("values")
                    .EnumerateArray()
                    .Select(x => x.GetSingle())
                    .ToArray();
            }
            catch (HttpRequestException ex)
            {
                throw new AiServiceException(
                    "Embedding API Network Error",
                    "Yapay zeka servisine bağlantı kurulamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.",
                    ex.Message,
                    AiSupportApp.Enums.ErrorSeverity.Transient,
                    ex);
            }
        }

        /// <summary>
        /// Embeds multiple document chunks in a single batch. Uses RETRIEVAL_DOCUMENT taskType.
        /// </summary>
        public async Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> chunks, string? title = null, CancellationToken cancellationToken = default)
        {
            if (chunks == null || chunks.Count == 0) return Array.Empty<float[]>();

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_embeddingModel}:batchEmbedContents";

            var requests = chunks.Select(chunk => new
            {
                model = $"models/{_embeddingModel}",
                content = new
                {
                    parts = new[] { new { text = chunk } }
                },
                taskType = "RETRIEVAL_DOCUMENT",
                title = title // Used by Gemini to better contextualize the document
            }).ToArray();

            var requestBody = new { requests };

            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new AiServiceException(
                        "Batch Embedding API Error",
                        "Yapay zeka servisi şu anda dökümanları işleyemiyor.",
                        $"Document batch embedding failed. Status: {response.StatusCode}, Body: {json}",
                        AiSupportApp.Enums.ErrorSeverity.Degraded);
                }

                var doc = JsonDocument.Parse(json);

                var embeddings = new List<float[]>();
                foreach (var embeddingElem in doc.RootElement.GetProperty("embeddings").EnumerateArray())
                {
                    var values = embeddingElem
                        .GetProperty("values")
                        .EnumerateArray()
                        .Select(x => x.GetSingle())
                        .ToArray();
                    embeddings.Add(values);
                }

                return embeddings;
            }
            catch (HttpRequestException ex)
            {
                throw new AiServiceException(
                    "Batch Embedding Network Error",
                    "Yapay zeka servisine bağlantı kurulamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.",
                    ex.Message,
                    AiSupportApp.Enums.ErrorSeverity.Transient,
                    ex);
            }
        }
    }
}