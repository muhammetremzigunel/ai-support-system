namespace AiSupportApp.Models
{
    public class DocumentChunk
    {
        public string DocId { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public int TotalChunks { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
    }
}
