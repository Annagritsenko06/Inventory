using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CourseWork.Models;
using System;
using CourseWork.Services;

namespace task5.Controllers
{
    [Authorize]
    public class UsersController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly SignInManager<User> _signInManager;
        private readonly AppDbContext _db;

        public UsersController(UserManager<User> userManager, RoleManager<IdentityRole<Guid>> roleManager, SignInManager<User> signInManager, AppDbContext db)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _db = db;
        }

        [Authorize]
        public async Task<IActionResult> Profile(Guid? id)
        {
            var currentUserIdString = _userManager.GetUserId(User);
            if (!Guid.TryParse(currentUserIdString, out var currentUserId))
                return BadRequest("Invalid user ID format.");

            id ??= currentUserId;

            if (id != currentUserId && !User.IsInRole("Admin"))
                return Forbid();

            var ownedInventories = await _db.Inventories
                .Where(i => i.OwnerId == id)
                .Include(i => i.Fields)
                .AsNoTracking()
                .ToListAsync();

            var writableInventories = await _db.Inventories
                .Where(i => i.IsPublic || i.access_list.Any(a => a.user_id == id && a.type == access_type.Write))
                .Include(i => i.Fields)
                .AsNoTracking()
                .ToListAsync();

            var model = new UserProfile
            {
                OwnedTemplates = ownedInventories,
                WritableTemplates = writableInventories
            };

            return View(model);
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();

            // Получаем роли для каждого пользователя
            var userRoles = new List<UserRolesViewModel>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userRoles.Add(new UserRolesViewModel
                {
                    User = user,
                    Roles = roles.ToList()
                });
            }

            return View(userRoles);
        }


        // Блокировка выбранных пользователей
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Block(List<string> selectedUsers)
        {
            if (selectedUsers == null) return RedirectToAction(nameof(Index));

            foreach (var idStr in selectedUsers)
            {
                if (Guid.TryParse(idStr, out var userId))
                {
                    var user = await _userManager.FindByIdAsync(userId.ToString());
                    if (user != null)
                    {
                        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
                        user.Status = "Blocked";
                        await _userManager.UpdateAsync(user);
                        if (user.Id.ToString() == _userManager.GetUserId(User))
                        {
                           return RedirectToAction("Register", "Registration");
                        }
                    }
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // Разблокировка выбранных пользователей
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Unblock(List<string> selectedUsers)
        {
            if (selectedUsers == null) return RedirectToAction(nameof(Index));

            foreach (var idStr in selectedUsers)
            {
                if (Guid.TryParse(idStr, out var userId))
                {
                    var user = await _userManager.FindByIdAsync(userId.ToString());
                    if (user != null)
                    {
                        await _userManager.SetLockoutEndDateAsync(user, null);
                        user.Status = "Active";
                        await _userManager.UpdateAsync(user);
                    }
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // Удаление выбранных пользователей
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Delete(List<string> selectedUsers)
        {
            if (selectedUsers == null) return RedirectToAction(nameof(Index));

            foreach (var idStr in selectedUsers)
            {
                if (Guid.TryParse(idStr, out var userId))
                {
                    var user = await _userManager.FindByIdAsync(userId.ToString());
                    if (user != null)
                        await _userManager.DeleteAsync(user);
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // Изменение статуса пользователя вручную
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> ChangeStatus(string id, string status)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.Status = status;

            // Если статус "Blocked", блокируем пользователя
            if (status.Equals("Blocked", StringComparison.OrdinalIgnoreCase))
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
            }

            await _userManager.UpdateAsync(user);
            return RedirectToAction(nameof(Index));
        }

        // Добавление роли Admin
        [HttpPost]
        public async Task<IActionResult> AddAdminRole(List<string> selectedUsers)
        {
            if (selectedUsers == null) return RedirectToAction(nameof(Index));

            if (!await _roleManager.RoleExistsAsync("Admin"))
                await _roleManager.CreateAsync(new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Admin", NormalizedName = "ADMIN" });

            foreach (var id in selectedUsers)
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user != null && !await _userManager.IsInRoleAsync(user, "Admin"))
                    await _userManager.AddToRoleAsync(user, "Admin");
            }

            return RedirectToAction(nameof(Index));
        }


        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> RemoveAdminRole(List<string> selectedUsers)
        {
            if (selectedUsers == null) return RedirectToAction(nameof(Index));

            foreach (var id in selectedUsers)
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user != null && await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    await _userManager.RemoveFromRoleAsync(user, "Admin");

                    if (!await _userManager.IsInRoleAsync(user, "User"))
                    {

                        if (!await _roleManager.RoleExistsAsync("User"))
                        {
                            var role = new IdentityRole<Guid>
                            {
                                Id = Guid.NewGuid(),
                                Name = "User",
                                NormalizedName = "USER"
                            };
                            var roleResult = await _roleManager.CreateAsync(role);
                            await _userManager.AddToRoleAsync(user, "User");
                        }
                        await _userManager.AddToRoleAsync(user, "User");
                    }
                   

                    // Если это текущий пользователь — обновляем куки
                    if (user.Id.ToString() == _userManager.GetUserId(User))
                    {
                        await _signInManager.RefreshSignInAsync(user);
                    }
                }
            }

            return RedirectToAction("Index","Home");
        }

    }
}
