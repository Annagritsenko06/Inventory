using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CourseWork.Services;
namespace CourseWork.Models { 
public class Search1Controller : Controller
{
    private readonly AppDbContext _context;

    public Search1Controller(AppDbContext context)
    {
        _context = context;
    }

    public IActionResult Index(string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return View(new SearchResultViewModel());
        }

        // Поиск по Inventories (русский и английский)
        var inventories = _context.Inventories
            .FromSqlRaw(@"
                SELECT *, ts_rank(""SearchVectorRu"" || ""SearchVectorEn"", 
                    plainto_tsquery('russian', {0}) || plainto_tsquery('english', {0})) AS rank
                FROM ""inventories""
                WHERE ""SearchVectorRu"" @@ plainto_tsquery('russian', {0})
                   OR ""SearchVectorEn"" @@ plainto_tsquery('english', {0})
                ORDER BY rank DESC
            ", q)
            .ToList();

        // Поиск по InventoryItem (русский и английский)
        var items = _context.InventoryItems
            .FromSqlRaw(@"
                SELECT *, ts_rank(""SearchVectorRu"" || ""SearchVectorEn"", 
                    plainto_tsquery('russian', {0}) || plainto_tsquery('english', {0})) AS rank
                FROM ""inventory_items""
                WHERE ""SearchVectorRu"" @@ plainto_tsquery('russian', {0})
                   OR ""SearchVectorEn"" @@ plainto_tsquery('english', {0})
                ORDER BY rank DESC
            ", q)
            .ToList();

        var model = new SearchResultViewModel
        {
            Query = q,
            Inventories = inventories,
            Items = items
        };

        return View(model);
    }
}
}