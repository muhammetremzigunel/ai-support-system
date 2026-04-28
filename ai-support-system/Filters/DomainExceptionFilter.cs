using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using AiSupportApp.Exceptions;
using Microsoft.Extensions.Logging;

namespace AiSupportApp.Filters
{
    public class DomainExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<DomainExceptionFilter> _logger;
        private readonly ITempDataDictionaryFactory _tempDataDictionaryFactory;

        public DomainExceptionFilter(ILogger<DomainExceptionFilter> logger, ITempDataDictionaryFactory tempDataDictionaryFactory)
        {
            _logger = logger;
            _tempDataDictionaryFactory = tempDataDictionaryFactory;
        }

        public void OnException(ExceptionContext context)
        {
            if (context.Exception is DomainException domainEx)
            {
                // Log the technical details for developers
                _logger.LogError(domainEx, "Domain Exception [{Severity}]: {TechnicalDetail}", domainEx.Severity, domainEx.TechnicalDetail);

                // For POST requests (like form submissions) or AJAX, we want to stay on the flow
                var isPost = context.HttpContext.Request.Method == "POST";
                
                if (isPost)
                {
                    var tempData = _tempDataDictionaryFactory.GetTempData(context.HttpContext);
                    tempData["Error"] = domainEx.UserMessage;

                    // Try to redirect back to the page they came from
                    var referer = context.HttpContext.Request.Headers["Referer"].ToString();
                    if (!string.IsNullOrEmpty(referer))
                    {
                        context.Result = new RedirectResult(referer);
                    }
                    else
                    {
                        // Fallback if no referer is available
                        var controller = context.RouteData.Values["controller"]?.ToString() ?? "Home";
                        var action = context.RouteData.Values["action"]?.ToString() ?? "Index";
                        context.Result = new RedirectToActionResult(action, controller, null);
                    }
                }
                else
                {
                    // For GET requests, we can just return the View with the error message in ViewData
                    var result = new ViewResult
                    {
                        ViewName = context.RouteData.Values["action"]?.ToString() ?? "Index",
                        ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), context.ModelState)
                        {
                            ["Error"] = domainEx.UserMessage
                        }
                    };

                    context.Result = result;
                }

                // Mark the exception as handled so it doesn't propagate to the 500 page
                context.ExceptionHandled = true;
            }
        }
    }
}
