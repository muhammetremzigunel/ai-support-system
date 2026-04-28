using System;
using AiSupportApp.Enums;

namespace AiSupportApp.Exceptions
{
    public abstract class DomainException : Exception
    {
        public string UserMessage { get; }
        public string TechnicalDetail { get; }
        public ErrorSeverity Severity { get; }

        protected DomainException(string message, string userMessage, string technicalDetail, ErrorSeverity severity, Exception innerException = null) 
            : base(message, innerException)
        {
            UserMessage = userMessage;
            TechnicalDetail = technicalDetail;
            Severity = severity;
        }
    }
}
