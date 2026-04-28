namespace AiSupportApp.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public string? ExceptionMessage { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
