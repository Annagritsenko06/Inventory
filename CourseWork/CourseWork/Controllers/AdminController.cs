using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CourseWork.Models;
using CourseWork.Services;

namespace CourseWork.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly AppDbContext _db;

        public AdminController(UserManager<User> userManager, RoleManager<IdentityRole<Guid>> roleManager, AppDbContext db)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users
                .Select(u => new UserProfile
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Email = u.Email,
                    Status = u.Status,
                    RegistrationTime = u.RegistrationTime,
                    IsAdmin = _userManager.IsInRoleAsync(u, "Admin").Result
                })
                .ToListAsync();

            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleUserStatus(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return NotFound();

            user.Status = user.Status == "Active" ? "Blocked" : "Active";
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
                return Json(new { success = true, newStatus = user.Status });
            
            return Json(new { success = false, errors = result.Errors.Select(e => e.Description) });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAdminRole(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return NotFound();

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            
            if (isAdmin)
            {
                await _userManager.RemoveFromRoleAsync(user, "Admin");
            }
            else
            {
                await _userManager.AddToRoleAsync(user, "Admin");
            }

            return Json(new { success = true, isAdmin = !isAdmin });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return NotFound();

            // Проверяем, что админ не удаляет сам себя
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == userId)
                return Json(new { success = false, error = "Нельзя удалить самого себя" });

            var result = await _userManager.DeleteAsync(user);
            
            if (result.Succeeded)
                return Json(new { success = true });
            
            return Json(new { success = false, errors = result.Errors.Select(e => e.Description) });
        }

        [HttpGet]
        public async Task<IActionResult> UserDetails(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return NotFound();

            var userInventories = await _db.Inventories
                .Where(i => i.OwnerId == userId)
                .Select(i => new { i.Id, i.Name })
                .ToListAsync();

            var userProfile = new UserProfile
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                Status = user.Status,
                RegistrationTime = user.RegistrationTime,
                IsAdmin = await _userManager.IsInRoleAsync(user, "Admin")
            };

            ViewBag.UserInventories = userInventories;
            return View(userProfile);
        }
    }
}
