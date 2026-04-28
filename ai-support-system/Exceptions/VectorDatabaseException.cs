using System;
using AiSupportApp.Enums;

namespace AiSupportApp.Exceptions
{
    public class VectorDatabaseException : DomainException
    {
        public VectorDatabaseException(string message, string userMessage, string technicalDetail, ErrorSeverity severity, Exception innerException = null) 
            : base(message, userMessage, technicalDetail, severity, innerException)
        {
        }
    }
}
