using Microsoft.ML.Tokenizers;
using System.Text;

namespace AiSupportApp.Services
{
    /// <summary>
    /// Service to split large documents into smaller chunks for semantic vector storage.
    /// This resolves the issue of token limitations and semantic dilution in the RAG pipeline.
    /// </summary>
    public class TextChunker
    {
        private readonly Tokenizer _tokenizer;
        private readonly int _targetTokens;
        private readonly int _overlapTokens;

        public TextChunker(int targetTokens = 500, int overlapTokens = 60)
        {
            _targetTokens = targetTokens;
            _overlapTokens = overlapTokens;
            // cl100k_base is an OpenAI tokenizer but serves as an excellent, fast approximation 
            // for the Gemini token limits we are trying to stay under.
            _tokenizer = TiktokenTokenizer.CreateForEncoding("cl100k_base");
        }

        /// <summary>
        /// Splits text recursively based on token budget, maintaining an overlap between chunks.
        /// </summary>
        public IReadOnlyList<string> ChunkText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();

            var chunks = new List<string>();
            var separators = new[] { "\n\n", "\n", ". ", " ", "" };
            
            SplitRecursively(text, separators, 0, chunks);

            return chunks;
        }

        private void SplitRecursively(string text, string[] separators, int separatorIndex, List<string> finalChunks)
        {
            if (string.IsNullOrEmpty(text)) return;

            int tokenCount = _tokenizer.CountTokens(text);
            if (tokenCount <= _targetTokens)
            {
                finalChunks.Add(text);
                return;
            }

            if (separatorIndex >= separators.Length)
            {
                // Fallback for an unbreakable string: slice by tokens or just yield it
                finalChunks.Add(text); 
                return;
            }

            string separator = separators[separatorIndex];
            var splits = string.IsNullOrEmpty(separator) 
                ? text.Select(c => c.ToString()).ToArray() // split by character if no separators left
                : text.Split(new[] { separator }, StringSplitOptions.None);

            var chunkParts = new List<string>();
            int currentTokenCount = 0;

            foreach (var split in splits)
            {
                // Re-attach separator except for the last piece (or if we are character-splitting)
                var piece = string.IsNullOrEmpty(separator) ? split : split + separator;
                int pieceTokenCount = _tokenizer.CountTokens(piece);

                if (currentTokenCount + pieceTokenCount > _targetTokens && chunkParts.Count > 0)
                {
                    // 1. Yield the current chunk
                    var chunkText = BuildChunkText(chunkParts, separator);
                    if (!string.IsNullOrWhiteSpace(chunkText))
                    {
                        finalChunks.Add(chunkText);
                    }

                    // 2. Establish overlap for the next chunk
                    var overlapParts = new List<string>();
                    int overlapTokenCount = 0;
                    for (int i = chunkParts.Count - 1; i >= 0; i--)
                    {
                        int pCount = _tokenizer.CountTokens(chunkParts[i]);
                        if (overlapTokenCount + pCount <= _overlapTokens)
                        {
                            overlapParts.Insert(0, chunkParts[i]);
                            overlapTokenCount += pCount;
                        }
                        else
                        {
                            break; // overlap budget met
                        }
                    }

                    chunkParts.Clear();
                    chunkParts.AddRange(overlapParts);
                    currentTokenCount = overlapTokenCount;
                }

                if (pieceTokenCount > _targetTokens)
                {
                    // The single piece itself is larger than target budget, recurse with next separator
                    if (chunkParts.Count > 0)
                    {
                        var chunkText = BuildChunkText(chunkParts, separator);
                        if (!string.IsNullOrWhiteSpace(chunkText))
                        {
                            finalChunks.Add(chunkText);
                        }
                        chunkParts.Clear();
                        currentTokenCount = 0;
                    }
                    
                    SplitRecursively(piece, separators, separatorIndex + 1, finalChunks);
                }
                else
                {
                    chunkParts.Add(piece);
                    currentTokenCount += pieceTokenCount;
                }
            }

            // Flush the remaining parts
            if (chunkParts.Count > 0)
            {
                var chunkText = BuildChunkText(chunkParts, separator);
                if (!string.IsNullOrWhiteSpace(chunkText))
                {
                    finalChunks.Add(chunkText);
                }
            }
        }

        private string BuildChunkText(List<string> parts, string separator)
        {
            var text = string.Join("", parts);
            if (!string.IsNullOrEmpty(separator) && text.EndsWith(separator))
            {
                text = text.Substring(0, text.Length - separator.Length);
            }
            return text.Trim();
        }
    }
}
