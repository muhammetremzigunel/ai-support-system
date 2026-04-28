using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;
using AiSupportApp.Models;

namespace AiSupportApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _env;

        public HomeController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var exceptionHandlerPathFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            
            var errorMsg = _env.IsDevelopment() 
                ? exceptionHandlerPathFeature?.Error?.Message ?? "An unknown error occurred."
                : "Sistemsel beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyiniz.";

            return View(new ErrorViewModel 
            { 
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                ExceptionMessage = errorMsg
            });
        }
    }
}
