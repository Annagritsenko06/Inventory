using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using CourseWork.Models;
using System.Security.Claims;

namespace CourseWork.Controllers
{
    public class RegistrationController : Controller

    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;

        private readonly ILogger<RegistrationController> _logger;
        public RegistrationController(UserManager<User> userManager, SignInManager<User> signInManager, RoleManager<IdentityRole<Guid>> roleManager, ILogger<RegistrationController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var result = await _signInManager.PasswordSignInAsync(email, password, false, false);
            if (result.Succeeded)
                return RedirectToAction("Index", "Home");

            ModelState.AddModelError("", "Invalid login");
            return View();
        }

        [HttpGet]
        public IActionResult Register() => View();

        //[HttpPost]
        //public async Task<IActionResult> Register(string email, string password)
        //{
        //    // Создаем пользователя и сразу заполняем доп. поля
        //    var user = new User
        //    {
        //        UserName = email,
        //        Email = email,
        //        Status = "Active", // можно задать значение по умолчанию
        //        RegistrationTime = DateTime.UtcNow // сохраняем время регистрации
        //    };

        //    var result = await _userManager.CreateAsync(user, password);

        //    if (result.Succeeded)
        //    {
        //        await _signInManager.SignInAsync(user, false);
        //        return RedirectToAction("Index", "Home");
        //    }

        //    foreach (var error in result.Errors)
        //        ModelState.AddModelError("", error.Description);

        //    return View();
        //}

        [HttpPost]
        public async Task<IActionResult> Register(string email, string password)
        {
            var user = new User
            {
                UserName = email,
                Email = email,
                Status = "Active",
                RegistrationTime = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                // Проверяем, есть ли роль "User"
                if (!await _roleManager.RoleExistsAsync("User"))
                {
                    var role = new IdentityRole<Guid>
                    {
                        Id = Guid.NewGuid(),
                        Name = "User",
                        NormalizedName = "USER"
                    };
                    await _roleManager.CreateAsync(role);
                }

                // Добавляем пользователю роль
                var roleResult = await _userManager.AddToRoleAsync(user, "User");
                if (!roleResult.Succeeded)
                {
                    foreach (var err in roleResult.Errors)
                        ModelState.AddModelError("", $"Ошибка при добавлении роли: {err.Description}");
                }

                // Перезаходим, чтобы применились клеймы с ролью
                await _signInManager.SignInAsync(user, isPersistent: false);

                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View();
        }

        // === Внешние провайдеры ===

        [HttpPost]
        public IActionResult ExternalLogin(string provider)
        {
            Console.WriteLine($"ExternalLogin called with provider: {provider}");
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Registration");
            Console.WriteLine($"RedirectUrl: {redirectUrl}");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }
        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null)
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                _logger.LogWarning("ExternalLoginCallback: External login info is null.");
                return RedirectToAction(nameof(Login));
            }

            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false);
            if (result.Succeeded)
                return RedirectToAction("Index", "Home");

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (email == null)
                return RedirectToAction(nameof(Login));

            var user = new User
            {
                UserName = email,
                Email = email,
                Status = "Active",
                RegistrationTime = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
                return View("Login");

            await _userManager.AddLoginAsync(user, info);

            // === Назначаем роль ===
            string adminEmail = "angritsen@gmail.com";

            // Проверяем, есть ли роль Admin, если нет — создаём
            if (!await _roleManager.RoleExistsAsync("Admin"))
            {
                var adminRole = new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = "Admin",
                    NormalizedName = "ADMIN"
                };
                await _roleManager.CreateAsync(adminRole);
            }

            // Проверяем, есть ли роль User, если нет — создаём
            if (!await _roleManager.RoleExistsAsync("User"))
            {
                var userRole = new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = "User",
                    NormalizedName = "USER"
                };
                await _roleManager.CreateAsync(userRole);
            }

            // Если email совпадает с adminEmail — назначаем роль Admin
            if (email.Equals(adminEmail, StringComparison.OrdinalIgnoreCase))
            {
                await _userManager.AddToRoleAsync(user, "Admin");
                _logger.LogInformation("ExternalLoginCallback: {Email} назначен как администратор.", email);
            }
            else
            {
                await _userManager.AddToRoleAsync(user, "User");
                _logger.LogInformation("ExternalLoginCallback: {Email} назначен как пользователь.", email);
            }

            await _signInManager.SignInAsync(user, false);
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        public async Task EnsureAdminExists()
        {
            string adminEmail = "angritsen@gmail.com";
            string adminPassword = "Admin@123"; // задать безопасный пароль

            // Проверяем, есть ли пользователь
            var adminUser = await _userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new User
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    Status = "Active",
                    RegistrationTime = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(adminUser, adminPassword);
                if (!result.Succeeded)
                {
                    throw new Exception("Не удалось создать администратора: " + string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }

            // Создаём роль Admin, если её нет
            if (!await _roleManager.RoleExistsAsync("Admin"))
            {
                var role = new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = "Admin",
                    NormalizedName = "ADMIN"
                };
                await _roleManager.CreateAsync(role);
            }

            // Добавляем пользователя в роль Admin
            if (!await _userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                await _userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

    }
}

       