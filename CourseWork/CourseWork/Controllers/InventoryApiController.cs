using CourseWork.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;


namespace CourseWork.Controllers
{
  [Route("api/inventory")]
[ApiController]
public class InventoryApiController : ControllerBase
    {
        private readonly AppDbContext _db;

        public InventoryApiController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("{token}")]
        public async Task<IActionResult> GetInventoryByToken(string token)
        {
            var inv = await _db.Inventories
                .Include(i => i.Items)
                .Include(i => i.Fields)
                .FirstOrDefaultAsync(i => i.ApiToken == token);

            if (inv == null)
                return NotFound(new { error = "Invalid token" });

            // агрегирование
            var items = inv.Items
    .Select(i => JsonSerializer.Deserialize<Dictionary<string, object>>(i.ValuesJson)!)
    .ToList();

            var aggregates = CalculateAggregates(items);

            return Ok(new
            {
                id = inv.Id,
                name = inv.Name,
                description = inv.Description,
                fields = inv.Fields.Select(f => new { f.Name, f.Type }),
                fieldCount = inv.Fields.Count, 
                itemCount = inv.Items.Count,  
                aggregates
            });

        }

        private object CalculateAggregates(List<Dictionary<string, object>> items)
        {
            var numeric = new Dictionary<string, List<double>>();
            var text = new Dictionary<string, List<string>>();

            foreach (var dict in items)
            {
                foreach (var kv in dict)
                {
                    if (kv.Value is JsonElement el)
                    {
                        if (el.ValueKind == JsonValueKind.Number)
                        {
                            double val = el.GetDouble();
                            if (!numeric.ContainsKey(kv.Key))
                                numeric[kv.Key] = new List<double>();
                            numeric[kv.Key].Add(val);
                        }
                        else if (el.ValueKind == JsonValueKind.String)
                        {
                            string str = el.GetString()!;
                            if (double.TryParse(str, out var num))
                            {
                                if (!numeric.ContainsKey(kv.Key))
                                    numeric[kv.Key] = new List<double>();
                                numeric[kv.Key].Add(num);
                            }
                            else
                            {
                                if (!text.ContainsKey(kv.Key))
                                    text[kv.Key] = new List<string>();
                                text[kv.Key].Add(str);
                            }
                        }
                    }
                }
            }

            return new
            {
                numeric = numeric.ToDictionary(k => k.Key, v => new
                {
                    min = v.Value.Min(),
                    max = v.Value.Max(),
                    avg = v.Value.Average()
                }),
                text = text.ToDictionary(k => k.Key,
                    v => v.Value
                        .GroupBy(x => x)
                        .OrderByDescending(g => g.Count())
                        .Take(3)
                        .Select(g => new { value = g.Key, count = g.Count() })
                )
            };
        }
    }

}
