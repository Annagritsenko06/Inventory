
using Microsoft.AspNetCore.Mvc;
using CourseWork.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Identity;
using CourseWork.Models;

public class HomeController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<User> _userManager;

    public HomeController(AppDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");

        // Основной запрос с подгрузкой элементов
        var inventoriesQuery = _context.Inventories
            .Include(i => i.Items)
            .AsQueryable();

        // Фильтруем в зависимости от роли
        if (!isAdmin)
        {
            if (user != null)
            {
                inventoriesQuery = inventoriesQuery.Where(i =>
                    i.IsPublic || i.OwnerId == user.Id);
            }
            else
            {
                inventoriesQuery = inventoriesQuery.Where(i => i.IsPublic);
            }
        }

        // Получаем 5 самых популярных по количеству Items
        var inventories = await inventoriesQuery
            .OrderByDescending(i => i.Items.Count)
            .Take(5)
            .ToListAsync();

        // Теги (категории)
        var tags = await _context.Inventories
            .Where(i => !string.IsNullOrEmpty(i.Category.ToString()))
            .Select(i => i.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        ViewBag.Tags = tags;

        return View(inventories);
    }
    public IActionResult SetTheme(string theme, string returnUrl)
    {
        string themeUrl = theme switch
        {
            "light" => "https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css",
            "dark" => "https://cdn.jsdelivr.net/npm/bootswatch@5.3.2/dist/cyborg/bootstrap.min.css", 
            _ => "https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css"
        };

        Response.Cookies.Append("theme", themeUrl, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });

        return LocalRedirect(returnUrl);
    }

    public IActionResult SetLanguage(string culture, string returnUrl)
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
        );
        return LocalRedirect(returnUrl);
    }
}
