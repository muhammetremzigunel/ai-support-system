namespace AiSupportApp.DTOs
{
    /// <summary>
    /// Lightweight DTO for serializing conversation turns into Session.
    /// Microsoft.Extensions.AI's ChatMessage is not trivially JSON-serializable,
    /// so we store just the role string and text content.
    /// </summary>
    public class ChatTurnDto
    {
        public string Role { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
