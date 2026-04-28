using System;
using AiSupportApp.Enums;

namespace AiSupportApp.Exceptions
{
    public class EmbeddingException : AiServiceException
    {
        public EmbeddingException(string message, string technicalDetail, ErrorSeverity severity = ErrorSeverity.Degraded, Exception innerException = null) 
            : base(message, "Yapay zeka modelleriyle iletişim kurulurken bir hata oluştu. Lütfen tekrar deneyin.", technicalDetail, severity, innerException) { }
    }
}
