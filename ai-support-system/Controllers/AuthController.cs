using AiSupportApp.DTOs;
using AiSupportApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiSupportApp.Controllers
{
    [AllowAnonymous]
    public class AuthController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly JwtService _jwtService;
        private readonly IWebHostEnvironment _env;

        private CookieOptions CreateAuthCookieOptions() => new()
        {
            HttpOnly = true,
            Secure = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(24)
        };

        public AuthController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, JwtService jwtService, IWebHostEnvironment env)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
            _env = env;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginDto dto, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == dto.Email, cancellationToken);
            if (user == null)
            {
                ViewBag.Error = "Email veya şifre hatalı.";
                return View();
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
            if (!result.Succeeded)
            {
                ViewBag.Error = "Email veya şifre hatalı.";
                return View();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var token = _jwtService.GenerateToken(user, roles);

            Response.Cookies.Append("access_token", token, CreateAuthCookieOptions());

            return RedirectToAction("Index", "Chat");
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterDto dto, CancellationToken cancellationToken = default)
        {
            if (dto.Password != dto.ConfirmPassword)
            {
                ViewBag.Error = "Şifreler uyuşmuyor.";
                return View();
            }

            var user = new IdentityUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                ViewBag.Error = string.Join(", ", result.Errors.Select(e => e.Description));
                return View();
            }

            await _userManager.AddToRoleAsync(user, "user");

            return RedirectToAction("Login");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("access_token", CreateAuthCookieOptions());
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}