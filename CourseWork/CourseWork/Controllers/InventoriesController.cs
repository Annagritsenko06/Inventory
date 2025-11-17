using CourseWork.Models;
using CourseWork.Services;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;


namespace InventoryManager.Controllers
{
    public class InventoriesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<InventoriesController> _logger;

        public InventoriesController(AppDbContext db, UserManager<User> userManager, ILogger<InventoriesController> logger) { 
            _db = db;
            _userManager = userManager;
            _logger = logger;
        }
        [Route("api/inventories/{inventoryId}/items")]
        [HttpGet]
        public IActionResult GetItems(int inventoryId)
        {
            var items = _db.InventoryItems
                .Where(i => i.InventoryId == inventoryId)
                .Select(i => new
                {
                    customId = i.CustomId,
                    createdAt = i.CreatedAt
                })
                .ToList();

            return Ok(items);
        }
        [HttpPost]
        public async Task<IActionResult> GenerateToken(int Id)
        {
            var inv = await _db.Inventories.FindAsync(Id);
            if (inv == null) return NotFound();

            bool wasNew = string.IsNullOrEmpty(inv.ApiToken);

            var bytes = RandomNumberGenerator.GetBytes(32);
            string token = Convert.ToBase64String(bytes)
        .TrimEnd('=')      
        .Replace('+', '-')  
        .Replace('/', '_'); 

            inv.ApiToken = token;
            _db.Entry(inv).Property(i => i.ApiToken).IsModified = true;
            await _db.SaveChangesAsync();


            TempData["Success"] = wasNew
                ? $"API token has been generated. Copy api token: {token}"
                : "API token has been regenerated.";

            return RedirectToAction("Details", new { id = Id });
        }



        public IActionResult Create()
        {
            var newInventory = new Inventories();
            return View("_SettingsTab", newInventory);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Inventories model)
        {
            if (!ModelState.IsValid)
                return View(model);

            _db.Inventories.Add(model);
            _db.SaveChanges();
            return RedirectToAction("Details", new { id = model.Id });
        }

        [HttpPost]
        public IActionResult Delete(int[] selectedIds)
        {
            if (selectedIds != null && selectedIds.Length > 0)
            {
                _db.Inventories.RemoveRange(_db.Inventories.Where(i => selectedIds.Contains(i.Id)));
                _db.SaveChanges();
            }
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer))
            {
                return Redirect(referer);
            }
            return RedirectToAction("Profile", "Users");

        }


        public async Task<IActionResult> Index(string search)
        {
            var query = _db.Inventories
                .Include(i => i.Fields)
                .Include(i => i.Tags)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(i => 
                    i.Name.Contains(search) ||
                    i.Description.Contains(search) ||
                    i.Category.ToString().Contains(search) ||
                    i.Tags.Any(t => t.Name.Contains(search)));
            }

            var list = await query
                .OrderByDescending(i => i.Id)
                .ToListAsync();

            ViewBag.SearchTerm = search;
            return View(list);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditInv(InventoryWithItemsViewModel m)
        {
            var model = m.Inventory;
            if (model == null) return BadRequest();

            var inv = _db.Inventories.Include(i => i.Tags).FirstOrDefault(i => i.Id == model.Id);
            if (inv == null) return NotFound();

            inv.Name = model.Name;
            inv.Description = model.Description;
            inv.Category = model.Category;
            inv.ImageUrl = model.ImageUrl;
            inv.IsPublic = model.IsPublic;

            
            _db.Entry(inv).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            _db.SaveChanges();
            return RedirectToAction("Details", new { id = inv.Id });
        }

        [HttpPost]
        public async Task<IActionResult> SaveSettings(Inventories model, string? Tags) 
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var inv = await _db.Inventories
                .Include(i => i.Tags)
                .FirstOrDefaultAsync(i => i.Id == model.Id);
            _logger.LogDebug(inv == null ? "Инвентарь не найден" : $"Найден инвентарь Id={inv.Id}");

            if (inv == null)
            {
                inv = new Inventories
                {
                    Name = model.Name,
                    Description = model.Description,
                    Category = model.Category,
                    OwnerId = user.Id,
                    ImageUrl = model.ImageUrl,
                    IsPublic = model.IsPublic,
                    CustomIdFormatJson = model.CustomIdFormatJson
                };

                _db.Inventories.Add(inv);
                await _db.SaveChangesAsync(); 
            }
            else
            {
            
                inv.Name = model.Name;
                inv.Description = model.Description;
                inv.Category = model.Category;
               
                inv.ImageUrl = model.ImageUrl;
                inv.IsPublic = model.IsPublic;
                inv.CustomIdFormatJson = model.CustomIdFormatJson;
            }

            if (!string.IsNullOrEmpty(Tags))
            {
                var tagNames = Tags
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct()
                    .ToList();

                await _db.Entry(inv).Collection(i => i.Tags).LoadAsync();
              

                foreach (var name in tagNames)
                {
                    var tag = await _db.InventoryTags.FirstOrDefaultAsync(t => t.Name == name);
                    if (tag == null)
                    {
                        tag = new InventoryTag { Name = name };
                        _db.InventoryTags.Add(tag);
                    }
                    inv.Tags.Add(tag); 
                }
            }

            await _db.SaveChangesAsync();

            return RedirectToAction("Details", new { id = inv.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkAction(int inventoryId, string action, int[] selectedIds)
        {
            if (selectedIds == null || selectedIds.Length == 0)
            {
                TempData["Error"] = "Не выбраны элементы для действия.";
                return RedirectToAction("Details", new { id = inventoryId });
            }

            switch (action.ToLower())
            {
                case "delete":
                    var itemsToDelete = _db.InventoryItems.Where(i => selectedIds.Contains(i.Id));
                    _db.InventoryItems.RemoveRange(itemsToDelete);
                    await _db.SaveChangesAsync();
                    TempData["Success"] = "Элементы удалены.";
                    break;

                case "edit":
                    
                    return RedirectToAction("ItemDetails", new { id = selectedIds[0], isEdit = true });

                case "view":
                    
                    return RedirectToAction("ItemDetails", new { id = selectedIds[0], isEdit = false });
            }

            return RedirectToAction("Details", new { id = inventoryId });
        }

        //NEWNEWNEW
        [HttpGet]
        public async Task<IActionResult> ItemDetails(int id, bool isEdit = false)
        {
            var item = await _db.InventoryItems
                .Include(i => i.Inventory)
                .ThenInclude(inv => inv.Fields) 
                .Include(i => i.Likes)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null) return NotFound();

            var values = JsonSerializer.Deserialize<Dictionary<string, string?>>(item.ValuesJson ?? "{}")
                         ?? new Dictionary<string, string?>();

            var model = new InventoryItemViewModel
            {
                Id = item.Id,
                InventoryId = item.InventoryId,
                CustomId = item.CustomId,
                CreatedById = item.CreatedById,
                CreatedAt = item.CreatedAt,
                Version = item.Version,
                Likes = item.Likes,
                ValuesJson = item.ValuesJson,
                FieldValues = item.Inventory?.Fields
                    .OrderBy(f => f.Order)
                    .Select(f => values.ContainsKey(f.Name) ? values[f.Name] ?? "" : "")
                    .ToList() ?? new List<string>()
            };

            ViewData["IsEdit"] = isEdit;

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveItem(InventoryItemViewModel model)
        {
            var item = await _db.InventoryItems
                .Include(i => i.Inventory)
                .ThenInclude(inv => inv.Fields)
                .FirstOrDefaultAsync(i => i.Id == model.Id);

            if (item == null) return NotFound();

            var inventory = item.Inventory;

            if (inventory == null) return BadRequest();

            var values = new Dictionary<string, string?>();

            var orderedFields = inventory.Fields.OrderBy(f => f.Order).ToList();
            for (int i = 0; i < orderedFields.Count; i++)
            {
                var field = orderedFields[i];
                values[field.Name] = model.FieldValues.ElementAtOrDefault(i);
            }

            item.CustomId = model.CustomId;
            item.ValuesJson = JsonSerializer.Serialize(values);
            _db.Entry(item).State = EntityState.Modified;
            await _db.SaveChangesAsync();


            TempData["Success"] = "Изменения сохранены.";

            return RedirectToAction("ItemDetails", new { id = item.Id, isEdit = false });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSelected(int inventoryId, List<int> selectedIds, string action)
        {
            
            if (selectedIds == null || !selectedIds.Any())
            {
                TempData["Error"] = "Не выбраны элементы для действия.";
                return RedirectToAction("Details", new { id = inventoryId });
            }

            switch (action.ToLower())
            {
                case "delete":
                    var itemsToDelete = _db.InventoryItems.Where(i => selectedIds.Contains(i.Id));
                    _db.InventoryItems.RemoveRange(itemsToDelete);
                    await _db.SaveChangesAsync();
                    TempData["Success"] = "Элементы удалены.";
                    break;

                case "edit":
                    return RedirectToAction("ItemDetails", new { id = selectedIds[0], isEdit = true });

                case "view":
                    return RedirectToAction("ItemDetails", new { id = selectedIds[0], isEdit = false });
            }

            return RedirectToAction("Details", new { id = inventoryId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSelectedField(int inventoryId, List<int> selectedFieldIds, string action)
        {
            if (selectedFieldIds == null || !selectedFieldIds.Any())
            {
                TempData["Error"] = "Не выбраны элементы для действия.";
                return RedirectToAction("Details", new { id = inventoryId });
            }

            switch (action.ToLower())
            {
                case "delete":
                    var fieldsToDelete = _db.InventoryFields.Where(f => selectedFieldIds.Contains(f.Id)).ToList();
                    if (fieldsToDelete.Any())
                    {
                        _db.InventoryFields.RemoveRange(fieldsToDelete);
                        await _db.SaveChangesAsync();
                        TempData["Success"] = $"Удалено {fieldsToDelete.Count} полей.";
                    }
                    return RedirectToAction("Details", new { id = inventoryId });

                case "edit":
                    return RedirectToAction("FieldDetails", new { id = selectedFieldIds[0], isEdit = true });

                case "view":
                    return RedirectToAction("FieldDetails", new { id = selectedFieldIds[0], isEdit = false });
            }

            return RedirectToAction("Details", new { id = inventoryId });
        }

        public async Task<IActionResult> FieldDetails(int id, bool isEdit = false)
        {
            var field = await _db.InventoryFields.FindAsync(id);
            if (field == null)
                return NotFound();

            ViewData["IsEdit"] = isEdit;
            return View(field); 
        }
  

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveFieldDetails(InventoryField field)
        {
            if (field.InventoryId == 0)
            {
                TempData["Error"] = "InventoryId не указан";
                _logger.LogWarning("Попытка сохранить поле без InventoryId");
                return RedirectToAction("Details", new { id = field.InventoryId });
            }

            if (field.Id == 0)
            {
                _db.InventoryFields.Add(field);
                _logger.LogInformation("Добавление нового поля: {@Field}", field);
                TempData["Success"] = "Поле добавлено";
            }
            else
            {
                var existing = await _db.InventoryFields.FindAsync(field.Id);
                if (existing == null)
                {
                    TempData["Error"] = "Поле не найдено";
                    _logger.LogWarning("Попытка редактирования несуществующего поля с Id {Id}", field.Id);
                    return RedirectToAction("Details", new { id = field.InventoryId });
                }

                _logger.LogInformation("Старые значения: Name={Name}, Description={Description}, Type={Type}, ShowInTable={ShowInTable}, Order={Order}",
    existing.Name, existing.Description, existing.Type, existing.ShowInTable, existing.Order);

                
                existing.Name = field.Name;
                existing.Description = field.Description;
                existing.Type = field.Type;
                existing.ShowInTable = field.ShowInTable;
                existing.Order = field.Order;
                _db.Entry(existing).State = EntityState.Modified;
                _logger.LogInformation("Новые значения: Name={Name}, Description={Description}, Type={Type}, ShowInTable={ShowInTable}, Order={Order}",
                    field.Name, field.Description, field.Type, field.ShowInTable, field.Order);

                TempData["Success"] = "Поле обновлено";
            }

            var changes = await _db.SaveChangesAsync();
            _logger.LogInformation("Сохранено изменений в БД: {ChangesCount}", changes);

            return RedirectToAction("Details", new { id = field.InventoryId });
        }


        public async Task<IActionResult> ByTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return RedirectToAction("Index", "Home");


            var inventories = await _db.Inventories
                .Include(i => i.Items)
                .Include(i => i.Tags) 
                .Where(i => i.Tags.Any(t => t.Name == tag))
                .ToListAsync();

            ViewBag.Tag = tag;
            return View("Index", inventories);
        }

        public async Task<IActionResult> ByTagCategories(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return RedirectToAction("Index", "Home");

            var inventories = await _db.Inventories
                .Include(i => i.Items)
                .Where(i => i.Category.ToString() == tag)
                .ToListAsync();

            ViewBag.Tag = tag;
            return View("Index", inventories);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUserAccess(int inventoryId, Guid userId)
        {
            Console.WriteLine($"AddUserAccess called: inventoryId={inventoryId},id пользователя= {userId} ");

            var inventory = await _db.Inventories
                .Include(i => i.access_list)
                .ThenInclude(a => a.user)
                .FirstOrDefaultAsync(i => i.Id == inventoryId);

            if (inventory == null)
                return NotFound();
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
            {
                TempData["Error"] = "Выбранный пользователь не найден.";
                return RedirectToAction("Details", "Inventories", new { id = inventory.Id });
            }

            if (inventory.access_list.Any(a => a.user_id == user.Id))
            {
                TempData["Warning"] = $"У пользователя {user.UserName} уже есть доступ.";
            }
            else
            {
                var access = new AccessInventory
                {
                    inventory_template_id = inventory.Id,
                    user_id = user.Id,
                    type = access_type.Write
                };

                _db.AccessInventories.Add(access);
                await _db.SaveChangesAsync();

                TempData["Success"] = $"Пользователь {user.UserName} успешно добавлен.";
            }

            return RedirectToAction("Details", "Inventories", new { id = inventory.Id });
        }



        [HttpPost]
        public async Task<IActionResult> RemoveUserAccessMultiple(int inventoryId, List<Guid> selectedUserIds)
        {
            if (selectedUserIds == null || !selectedUserIds.Any())
            {
                TempData["Error"] = "Не выбраны пользователи для удаления.";
                return RedirectToAction("Details", new { id = inventoryId });
            }

            var inventoryExists = await _db.Inventories.AnyAsync(i => i.Id == inventoryId);
            if (!inventoryExists)
                return NotFound();

            var accessesToDelete = await _db.AccessInventories
                .Where(a => a.inventory_template_id == inventoryId && selectedUserIds.Contains(a.user_id))
                .ToListAsync();

            if (accessesToDelete.Any())
            {
                _db.AccessInventories.RemoveRange(accessesToDelete);
                await _db.SaveChangesAsync();
                TempData["Success"] = $"Удалено пользователей: {accessesToDelete.Count}.";
            }
            else
            {
                TempData["Warning"] = "Выбранные пользователи не найдены в списке доступа.";
            }

            return RedirectToAction("Details", new { id = inventoryId });
        }


        [HttpGet]
        public async Task<IActionResult> GetTagSuggestions(string term)
        {
            if (string.IsNullOrWhiteSpace(term)) return Json(new List<string>());

            var tags = await _db.InventoryTags
                .Where(t => t.Name.StartsWith(term))
                .Select(t => t.Name)
                .Distinct()
                .Take(10)
                .ToListAsync();

            return Json(tags);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveField(InventoryFieldsVM vm)
        {
            if (vm.FieldForm.InventoryId == 0)
            {
                TempData["Error"] = "InventoryId не указан";
                return RedirectToAction("Details", new { id = vm.FieldForm.InventoryId });
            }

            var existingFields = _db.InventoryFields
                .Where(f => f.InventoryId == vm.FieldForm.InventoryId)
                .ToList();

            int textSingleCount = existingFields.Count(f => f.Type == FieldType.TextSingle);
            int textMultiCount = existingFields.Count(f => f.Type == FieldType.TextMulti);
            int numberCount = existingFields.Count(f => f.Type == FieldType.Number);
            int booleanCount = existingFields.Count(f => f.Type == FieldType.Boolean);
            int LinkCount = existingFields.Count(f => f.Type == FieldType.ImageLink);

            string errorMessage = null;
            switch (vm.FieldForm.Type)
            {
                case FieldType.TextSingle:
                    if (textSingleCount >= 3) errorMessage = "Нельзя добавить больше 3 однострочных текстовых полей";
                    break;
                case FieldType.TextMulti:
                    if (textMultiCount >= 3) errorMessage = "Нельзя добавить больше 3 многострочных текстовых полей";
                    break;
                case FieldType.Number:
                    if (numberCount >= 3) errorMessage = "Нельзя добавить больше 3 числовых полей";
                    break;
               
                case FieldType.Boolean:
                    if (booleanCount >= 3) errorMessage = "Нельзя добавить больше 3 логических полей";
                    break;
                case FieldType.ImageLink:
                    if (booleanCount >= 3) errorMessage = "Нельзя добавить больше 3 полей для ссылок";
                    break;
                default:
                    errorMessage = "Неизвестный тип поля";
                    break;
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                TempData["Error"] = errorMessage;
                return RedirectToAction("Details", new { id = vm.FieldForm.InventoryId });
            }

            var newField = new InventoryField
            {
                InventoryId = vm.FieldForm.InventoryId,
                Name = vm.FieldForm.Name,
                Description = vm.FieldForm.Description,
                Type = vm.FieldForm.Type,
                ShowInTable = vm.FieldForm.ShowInTable,
                Order = vm.FieldForm.Order
            };

            _db.InventoryFields.Add(newField);
            _db.SaveChanges();

            TempData["Success"] = "Поле добавлено";
            return RedirectToAction("Details", new { id = vm.FieldForm.InventoryId });
        }



        [HttpGet("GetField")]
        public IActionResult GetField(int id)
        {
            var field = _db.InventoryFields.Find(id);
            if (field == null)
                return NotFound();

            return Json(new
            {
                id = field.Id,
                name = field.Name,
                description = field.Description,
                type = (int)field.Type,
                showInTable = field.ShowInTable,
                order = field.Order
            });
        }

        [HttpPost("DeleteField")]
        public IActionResult DeleteField(int id)
        {
            var field = _db.InventoryFields.Find(id);
            if (field == null)
            {
                TempData["Error"] = "Поле не найдено";
                return NotFound();
            }
            _db.InventoryFields.Remove(field);
            _db.SaveChanges();
            TempData["Success"] = "Поле успешно удалено";
            return RedirectToAction("Details", new { id = field.InventoryId });
        }


        
        [HttpGet]
        public IActionResult AddItem(int inventoryId)
        {
            var inventory = _db.Inventories
                .Include(i => i.Fields)
                .FirstOrDefault(i => i.Id == inventoryId);

            if (inventory == null)
                return NotFound();

            ViewBag.InventoryName = inventory.Name;
            ViewBag.InventoryId = inventory.Id;
            return View(inventory);
        }

        [HttpPost]
        public async Task<IActionResult> AddItem(int inventoryId, IFormCollection form)
        {
            try
            {
                var inventory = _db.Inventories
                    .Include(i => i.Fields)
                    .FirstOrDefault(i => i.Id == inventoryId);

                if (inventory == null)
                    return NotFound();

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return Unauthorized();

                var values = new Dictionary<string, object?>();

                foreach (var field in inventory.Fields.OrderBy(f => f.Order))
                {
                    string key = $"Field_{field.Id}";
                    string? value = form[key];

                    switch (field.Type)
                    {
                        case FieldType.Boolean:
                            value = form[key] == "on" ? "true" : "false";
                            break;
                        case FieldType.Number:
                            if (string.IsNullOrEmpty(value))
                                value = null;
                            else if (decimal.TryParse(value, out decimal numValue))
                                value = numValue.ToString();
                            break;
                        case FieldType.TextSingle:
                        case FieldType.TextMulti:
                        case FieldType.ImageLink:
                            if (string.IsNullOrEmpty(value))
                                value = null;
                            break;
                    }

                    values[field.Name] = value;
                }

                string customId = await GenerateCustomIdAsync(inventory);

                var item = new InventoryItem
                {
                    InventoryId = inventory.Id,
                    CreatedById = user.Id.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    CustomId = customId,
                    ValuesJson = JsonSerializer.Serialize(values)
                };

                _db.InventoryItems.Add(item);
                await _db.SaveChangesAsync();

                return RedirectToAction("Details", "Inventories", new { id = inventoryId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Ошибка при добавлении элемента: {ex.Message}");
                return RedirectToAction("AddItem", new { inventoryId });
            }
        }



        [HttpGet]
        public async Task<IActionResult> GetItemsWithLikes(int inventoryId)
        {
            var items = await _db.InventoryItems
                .Where(i => i.InventoryId == inventoryId)
                .Select(i => new
                {
                    id = i.Id,
                    customId = i.CustomId,
                    createdAt = i.CreatedAt,
                    likeCount = i.Likes.Count,
                    isLiked = User.Identity.IsAuthenticated ? 
                        i.Likes.Any(l => l.UserId == Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value)) : false
                })
                .ToListAsync();

            return Json(items);
        }

        [HttpPost]
        public async Task<IActionResult> SaveCustomIdFormat([FromBody] SaveCustomIdRequest request)
        {
            try
            {
                var inventory = await _db.Inventories.FindAsync(request.InventoryId);
                if (inventory == null) return NotFound();

                inventory.CustomIdFormatJson = JsonSerializer.Serialize(request.Format);
                await _db.SaveChangesAsync();

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest($"Ошибка при сохранении формата: {ex.Message}");
            }
        }

        public class SaveCustomIdRequest
        {
            public int InventoryId { get; set; }
            public object Format { get; set; }
        }

        private async Task<string> GenerateCustomIdAsync(Inventories inventory)
        {
            if (!string.IsNullOrEmpty(inventory.CustomIdFormatJson))
            {
                var format = CustomIdFormat.FromJson(inventory.CustomIdFormatJson);
                if (format != null && format.Parts.Any())
                {
                    return await GenerateCustomIdWithFormatAsync(inventory, format);
                }
            }
            var existingIds = await _db.InventoryItems
                .Where(i => i.InventoryId == inventory.Id)
                .Select(i => i.CustomId)
                .ToListAsync();

            string newId;
            int counter = 1;
            do
            {
                newId = $"ITEM_{counter:D4}";
                counter++;
            } while (existingIds.Contains(newId));

            return newId;
        }
       
        public async Task<IActionResult> Details(int id, string sortOrder = "name")
        {
            var inventory = await _db.Inventories
                .Include(i => i.Fields)
                .Include(i => i.Items)
                    .ThenInclude(it => it.Likes)
                .Include(i => i.access_list)
                    .ThenInclude(a => a.user)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (inventory == null) return NotFound();

            
            var allowedUsers = inventory.access_list
                .Select(a => a.user)
                .AsQueryable();

            allowedUsers = sortOrder switch
            {
                "email" => allowedUsers.OrderBy(u => u.Email),
                _ => allowedUsers.OrderBy(u => u.UserName)
            };
            var allowedUserIds = inventory.access_list.Select(a => a.user_id).ToList();
            var usersWithoutAccess = await _db.Users
       .Where(u => !allowedUserIds.Contains(u.Id) && u.Id != inventory.OwnerId)
       .OrderBy(u => u.UserName)
       .ToListAsync();

            var user = User.Identity?.IsAuthenticated == true
    ? await _db.Users.FirstOrDefaultAsync(u => u.Id.ToString() == User.FindFirstValue(ClaimTypes.NameIdentifier))
    : null;

            bool isOwnerOrAdmin = user != null &&
                (user.Id == inventory.OwnerId || await _userManager.IsInRoleAsync(user, "Admin"));

            bool canEdit = false;

            if (isOwnerOrAdmin)
            {
                canEdit = true;
            }
            else if (inventory.IsPublic)
            {
                canEdit = true;
            }
            else if (user != null)
            {
                canEdit = await _db.AccessInventories
                    .AnyAsync(ai => ai.user_id == user.Id
                                    && ai.inventory_template_id == id
                                    && ai.type == access_type.Write);
            }
            var discussions = await _db.InventoryDiscussions
    .Where(d => d.InventoryId == inventory.Id)
    .Include(d => d.User)
    .OrderBy(d => d.CreatedAt)
    .ToListAsync();

            var fieldsVM = new InventoryFieldsVM
            {
                Fields = inventory.Fields
                    .OrderBy(f => f.Order)
                    .Select(f => new InventoryFieldCreateVM
                    {
                        Id = f.Id,
                        InventoryId = f.InventoryId,
                        Name = f.Name,
                        Description = f.Description,
                        Type = f.Type,
                        ShowInTable = f.ShowInTable,
                        Order = f.Order
                    })
                    .ToList(),
                FieldForm = new InventoryFieldCreateVM { InventoryId = inventory.Id }
            };

            var viewModel = new InventoryWithItemsViewModel
            {
                Inventory = inventory,
                Items = inventory.Items.Select(item =>
                {
                    var jsonNode = System.Text.Json.Nodes.JsonNode.Parse(item.ValuesJson);
                    return new InventoryItemViewModel
                    {
                        Id = item.Id,
                        InventoryId = item.InventoryId,
                        CustomId = item.CustomId,
                        CreatedAt = item.CreatedAt,
                        Likes = item.Likes,
                        ValuesJson = item.ValuesJson,
                        FieldValues = inventory.Fields
                            .OrderBy(f => f.Order)
                            .Select(f => jsonNode?[f.Name]?.ToString() ?? string.Empty)
                            .ToList()
                    };
                }).ToList(),
                Fields = fieldsVM,
                SortOrder = sortOrder,
                AllowedUsers = allowedUsers.ToList(),
                 UsersWithoutAccess = usersWithoutAccess,
                 CanEdit=canEdit,
                 Discussions=discussions
            };
          


            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLike(int itemId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier); 
            if (!Guid.TryParse(userIdString, out Guid userGuid))
            {
                return Unauthorized();
            }


            var existingLike = await _db.ItemLikes
                .FirstOrDefaultAsync(l => l.ItemId == itemId && l.UserId == userGuid);

            if (existingLike != null)
            {
                _db.ItemLikes.Remove(existingLike);
            }
            else
            {
                _db.ItemLikes.Add(new ItemLike
                {
                    ItemId = itemId,
                    UserId = userGuid, 
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();

            var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == itemId);
            return RedirectToAction("Details", new { id = item.InventoryId });
        }



        private async Task<string> GenerateCustomIdWithFormatAsync(Inventories inventory, CustomIdFormat format)
        {
            var parts = new List<string>();

            foreach (var part in format.Parts)
            {
                switch (part.Type)
                {
                    case CustomIdPartType.FixedText:
                        parts.Add(part.Value);
                        break;
                    case CustomIdPartType.Random20Bit:
                        parts.Add(new Random().Next(0, 1048576).ToString("X5"));
                        break;
                    case CustomIdPartType.Random32Bit:
                        parts.Add(new Random().Next(0, int.MaxValue).ToString("X8"));
                        break;
                    case CustomIdPartType.Random6Digit:
                        parts.Add(new Random().Next(0, 1000000).ToString("D6"));
                        break;
                    case CustomIdPartType.Random9Digit:
                        parts.Add(new Random().Next(0, 1000000000).ToString("D9"));
                        break;
                    case CustomIdPartType.Guid:
                        parts.Add(Guid.NewGuid().ToString("N")[..8]);
                        break;
                    case CustomIdPartType.DateTime:
                        parts.Add(DateTime.UtcNow.ToString("yyyyMMdd"));
                        break;
                    case CustomIdPartType.Sequence:
                        var maxSequence = await _db.InventoryItems
                            .Where(i => i.InventoryId == inventory.Id)
                            .CountAsync();
                        parts.Add((maxSequence + 1).ToString("D4"));
                        break;
                }
            }

            return string.Join("", parts);
        }

    }
}