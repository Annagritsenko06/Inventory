//using Microsoft.AspNetCore.Mvc;
//using CourseWork.Services;
//using Microsoft.EntityFrameworkCore;


//namespace InventoryManager.Controllers
//{
//    public class HomeController : Controller
//    {
//        private readonly AppDbContext _db;
//        public HomeController(AppDbContext db) { _db = db; }


//        public async Task<IActionResult> Index()
//        {
//            var recent = await _db.Inventories
//            .Include(i => i.Fields)
//            .OrderByDescending(i => i.Id)
//            .Take(10)
//            .ToListAsync();


//            return View(recent);
//        }
//    }
//}
using Microsoft.AspNetCore.Mvc;
using CourseWork.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;

public class HomeController : Controller
{
    private readonly AppDbContext _context;

    public HomeController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
{
    var inventories = await _context.Inventories
        .Include(i => i.Items)
        .OrderByDescending(i => i.Id)
        .ToListAsync();

    var tags = await _context.Inventories
        .Where(i => !string.IsNullOrEmpty(i.Category))
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
            "dark" => "https://cdn.jsdelivr.net/npm/bootswatch@5.3.2/dist/cyborg/bootstrap.min.css", // мягкая dark
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
