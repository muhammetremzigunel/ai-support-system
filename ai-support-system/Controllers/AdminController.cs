using AiSupportApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiSupportApp.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminController : Controller
    {
        private readonly QdrantService _qdrantService;
        private readonly RagPipeline _ragPipeline;
        private readonly UserManager<IdentityUser> _userManager;

        public AdminController(QdrantService qdrantService, RagPipeline ragPipeline, UserManager<IdentityUser> userManager)
        {
            _qdrantService = qdrantService;
            _ragPipeline = ragPipeline;
            _userManager = userManager;
        }

        // Kullanıcı yönetimi
        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            var users = await _userManager.Users.ToListAsync(cancellationToken);
            var userList = new List<(IdentityUser User, string Role)>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault() ?? "user";
                userList.Add((user, role));
            }

            ViewBag.Users = userList;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRole(string userId, string newRole, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
                return RedirectToAction("Index");

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, newRole);

            return RedirectToAction("Index");
        }

        // Belge yönetimi
        public async Task<IActionResult> Documents(string? category = null, string? title = null, CancellationToken cancellationToken = default)
        {
            await _qdrantService.CreateCollectionIfNotExistsAsync(cancellationToken);
            var documents = await _qdrantService.GetAllDocumentsAsync(category, title, cancellationToken);
            var categories = await _qdrantService.GetCategoriesAsync(cancellationToken);
            var titles = string.IsNullOrEmpty(category)
                ? new List<string>()
                : await _qdrantService.GetTitlesByCategoryAsync(category, cancellationToken);

            ViewBag.Documents = documents;
            ViewBag.Categories = categories;
            ViewBag.Titles = titles;
            ViewBag.SelectedCategory = category;
            ViewBag.SelectedTitle = title;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDocument(string title, string category, string content, string newCategory, string newTitle, CancellationToken cancellationToken = default)
        {
            var finalCategory = !string.IsNullOrEmpty(newCategory) ? newCategory : category;
            var finalTitle = !string.IsNullOrEmpty(newTitle) ? newTitle : title;

            await _ragPipeline.IngestAsync(finalTitle, finalCategory, content, cancellationToken);

            return RedirectToAction("Documents");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDocument(string id, string newCategory, string newTitle, string content, CancellationToken cancellationToken = default)
        {
            try
            {
                // Delete old document chunks
                await _qdrantService.DeleteDocumentAsync(id, cancellationToken);

                // Insert the updated document
                await _ragPipeline.IngestAsync(newTitle, newCategory, content, cancellationToken);

                TempData["Success"] = "Belge başarıyla güncellendi.";
            }
            catch (Grpc.Core.RpcException ex)
            {
                throw new AiSupportApp.Exceptions.VectorDatabaseException(
                    "Vector DB Edit Error",
                    "Belge güncellenirken veritabanına ulaşılamadı. Lütfen daha sonra tekrar deneyin.",
                    $"Qdrant Edit Error: {ex.Status.Detail}",
                    AiSupportApp.Enums.ErrorSeverity.Transient,
                    ex);
            }

            return RedirectToAction("Documents");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument(string id, CancellationToken cancellationToken = default)
        {
            await _qdrantService.DeleteDocumentAsync(id, cancellationToken);
            return RedirectToAction("Documents");
        }
    }
}