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

        [HttpPost]
        public async Task<IActionResult> Register(string email, string password)
        {
            await EnsureAdminExists();

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

                var roleResult = await _userManager.AddToRoleAsync(user, "User");
                if (!roleResult.Succeeded)
                {
                    foreach (var err in roleResult.Errors)
                        ModelState.AddModelError("", $"Ошибка при добавлении роли: {err.Description}");
                }

                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View();
        }


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
            await EnsureAdminExists();

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                _logger.LogWarning("ExternalLoginCallback: External login info is null.");
                return RedirectToAction(nameof(Login));
            }

            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false);
            if (result.Succeeded)
            {
                _logger.LogInformation("ExternalLoginCallback: пользователь успешно вошёл через {Provider}", info.LoginProvider);
                return RedirectToAction("Index", "Home");
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (email == null)
            {
                _logger.LogWarning("ExternalLoginCallback: Email отсутствует в данных провайдера");
                return RedirectToAction(nameof(Login));
            }

            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                var logins = await _userManager.GetLoginsAsync(existingUser);
                if (!logins.Any(l => l.LoginProvider == info.LoginProvider && l.ProviderKey == info.ProviderKey))
                {
                    await _userManager.AddLoginAsync(existingUser, info);
                }

                await _signInManager.SignInAsync(existingUser, isPersistent: false);
                _logger.LogInformation("ExternalLoginCallback: существующий пользователь {Email} вошёл через {Provider}.", email, info.LoginProvider);
                return RedirectToAction("Index", "Home");
            }

            var user = new User
            {
                UserName = email,
                Email = email,
                Status = "Active",
                RegistrationTime = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                    _logger.LogError("ExternalLoginCallback: ошибка при создании пользователя: {Description}", error.Description);
                return View("Login");
            }

            await _userManager.AddLoginAsync(user, info);

            if (!await _roleManager.RoleExistsAsync("User"))
            {
                await _roleManager.CreateAsync(new IdentityRole<Guid> { Name = "User", NormalizedName = "USER" });
            }

            await _userManager.AddToRoleAsync(user, "User");

            await _signInManager.SignInAsync(user, false);
            _logger.LogInformation("ExternalLoginCallback: новый пользователь {Email} вошёл через {Provider}.", email, info.LoginProvider);

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
            string adminPassword = "Admin@123"; 

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

            if (!await _userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                await _userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

    }
}

       