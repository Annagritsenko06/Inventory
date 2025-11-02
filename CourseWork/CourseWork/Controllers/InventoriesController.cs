using CourseWork.Models;
using CourseWork.Services;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.Logging;


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
                    i.Category.Contains(search) ||
                    i.Tags.Any(t => t.Name.Contains(search)));
            }

            var list = await query
                .OrderByDescending(i => i.Id)
                .ToListAsync();

            ViewBag.SearchTerm = search;
            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> SaveSettings(Inventories model, IFormFile? imageFile)
        {
            var user = await _userManager.GetUserAsync(User); // Получаем текущего пользователя
            if (user == null) return Unauthorized();
            var inv = await _db.Inventories.FindAsync(model.Id);
            if (inv == null)
            {
                // Создаем новый объект, если не найден существующий
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
            }
            else
            {
                // Обновляем существующий
                inv.Name = model.Name;
                inv.Description = model.Description;
                inv.Category = model.Category;
                inv.OwnerId = user.Id;
                if(imageFile != null && imageFile.Length > 0)
                {
                    Console.WriteLine($"Загружаем изображение: {imageFile.FileName}");
                    var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
                    if (!Directory.Exists(uploadsDir))
                        Directory.CreateDirectory(uploadsDir);

                    var fileName = Path.GetFileName(imageFile.FileName);
                    var filePath = Path.Combine(uploadsDir, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        imageFile.CopyTo(stream);
                    }

                    inv.ImageUrl = "/images/" + fileName;
                }
                inv.IsPublic = model.IsPublic;
                inv.CustomIdFormatJson = model.CustomIdFormatJson;
                _db.Update(inv);
            }

            await _db.SaveChangesAsync();

            return RedirectToAction("Details", new { id = inv.Id });
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> BulkAction(int inventoryId, string action, List<int> selectedIds)
        //{
        //    if (selectedIds == null || !selectedIds.Any())
        //    {
        //        TempData["Error"] = "Не выбрано ни одного элемента.";
        //        return RedirectToAction("Details", new { id = inventoryId });
        //    }

        //    switch (action)
        //    {
        //        case "delete":
        //            var itemsToDelete = _db.InventoryItems.Where(i => selectedIds.Contains(i.Id));
        //            _db.InventoryItems.RemoveRange(itemsToDelete);
        //            await _db.SaveChangesAsync();
        //            TempData["Success"] = "Выбранные элементы удалены.";
        //            break;

        //        case "edit":
        //            if (selectedIds.Count == 1)
        //                return RedirectToAction("EditItem", new { id = selectedIds.First() });
        //            TempData["Error"] = "Редактировать можно только один элемент за раз.";
        //            break;

        //        case "view":
        //            if (selectedIds.Count == 1)
        //                return RedirectToAction("ItemDetails", new { id = selectedIds.First() });
        //            TempData["Error"] = "Просматривать можно только один элемент за раз.";
        //            break;
        //    }

        //    return RedirectToAction("Details", new { id = inventoryId });
        //}
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
                    // Редирект на страницу редактирования первого выбранного элемента
                    return RedirectToAction("ItemDetails", new { id = selectedIds[0], isEdit = true });

                case "view":
                    // Редирект на просмотр первого выбранного элемента
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

            // Десериализация ValuesJson
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

            // Формируем словарь для сериализации в JSON
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
            //if (selectedIds == null || !selectedIds.Any())
            //    return RedirectToAction("Details", new { id = Id });
            //int itemId = selectedIds.First();
            //if (action.ToLower() == "delete")
            //{
            //    var itemsToDelete = _db.InventoryItems.Where(i => selectedIds.Contains(i.Id));
            //    _db.InventoryItems.RemoveRange(itemsToDelete);
            //    await _db.SaveChangesAsync();
            //    TempData["Success"] = "Элементы удалены.";
            //    break;
            //}

            //return RedirectToAction("ItemDetails", new { id = itemId, isEdit = true });
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
                    // Редирект на страницу редактирования первого выбранного элемента
                    return RedirectToAction("ItemDetails", new { id = selectedIds[0], isEdit = true });

                case "view":
                    // Редирект на просмотр первого выбранного элемента
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
                    // Удаляем все выбранные поля
                    var fieldsToDelete = _db.InventoryFields.Where(f => selectedFieldIds.Contains(f.Id)).ToList();
                    if (fieldsToDelete.Any())
                    {
                        _db.InventoryFields.RemoveRange(fieldsToDelete);
                        await _db.SaveChangesAsync();
                        TempData["Success"] = $"Удалено {fieldsToDelete.Count} полей.";
                    }
                    return RedirectToAction("Details", new { id = inventoryId });

                case "edit":
                    // Редактируем первое выбранное поле
                    return RedirectToAction("FieldDetails", new { id = selectedFieldIds[0], isEdit = true });

                case "view":
                    // Просмотр первого выбранного поля
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
                // Добавление нового поля
                _db.InventoryFields.Add(field);
                _logger.LogInformation("Добавление нового поля: {@Field}", field);
                TempData["Success"] = "Поле добавлено";
            }
            else
            {
                // Редактирование существующего
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


        // GET: /Inventories/Edit/5
        // [HttpGet]
        //public IActionResult Edit(int inventoryId)
        //{
        //    var inventory = _db.Inventories
        //                       .Include(i => i.Fields)
        //                       .Include(i => i.Tags)
        //                       .FirstOrDefault(i => i.Id == inventoryId);

        //    if (inventory == null)
        //        return NotFound();

        //    // Формируем ViewModel
        //    var vm = new InventoryEditVM
        //    {
        //        Inventory = inventory,
        //        Fields = new InventoryFieldsVM
        //        {
        //            Fields = inventory.Fields.Select(f => new InventoryFieldCreateVM
        //            {
        //                Id = f.Id,
        //                InventoryId = f.InventoryId,
        //                Name = f.Name,
        //                Description = f.Description,
        //                Type = f.Type,
        //                ShowInTable = f.ShowInTable,
        //                Order = f.Order
        //            }).ToList(),

        //            // Для формы добавления нового поля
        //            FieldForm = new InventoryFieldCreateVM
        //            {
        //                InventoryId = inventory.Id
        //            }
        //        }
        //    };

        //    return View(vm);
        //}

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public IActionResult Edit(InventoryEditVM model, string TagsHidden, IFormFile? imageFile)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        Console.WriteLine("ModelState не валиден!");
        //        foreach (var kvp in ModelState)
        //        {
        //            var key = kvp.Key;
        //            var errors = kvp.Value.Errors;
        //            foreach (var error in errors)
        //            {
        //                Console.WriteLine($"Ошибка в поле {key}: {error.ErrorMessage}");
        //            }
        //        }
        //        return View(model);
        //    }


        //    var inv = _db.Inventories
        //                 .Include(i => i.Tags)
        //                 .Include(i => i.Fields)
        //                 .FirstOrDefault(i => i.Id == model.Inventory.Id);

        //    if (inv == null)
        //    {
        //        Console.WriteLine($"Инвентарь с ID {model.Inventory.Id} не найден!");
        //        return NotFound();
        //    }

        //    inv.Name = model.Inventory.Name;
        //    inv.Description = model.Inventory.Description;
        //    inv.Category = model.Inventory.Category;
        //    inv.IsPublic = model.Inventory.IsPublic;

        //    // Обновляем изображение
        //    if (imageFile != null && imageFile.Length > 0)
        //    {
        //        Console.WriteLine($"Загружаем изображение: {imageFile.FileName}");
        //        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
        //        if (!Directory.Exists(uploadsDir))
        //            Directory.CreateDirectory(uploadsDir);

        //        var fileName = Path.GetFileName(imageFile.FileName);
        //        var filePath = Path.Combine(uploadsDir, fileName);

        //        using (var stream = new FileStream(filePath, FileMode.Create))
        //        {
        //            imageFile.CopyTo(stream);
        //        }

        //        inv.ImageUrl = "/images/" + fileName;
        //    }

        //    // Обновляем теги
        //    var currentTags = JsonSerializer.Deserialize<List<string>>(TagsHidden ?? "[]") ?? new List<string>();
        //    inv.Tags.Clear();
        //    foreach (var tagName in currentTags)
        //    {
        //        var tag = _db.InventoryTags.FirstOrDefault(t => t.Name == tagName)
        //                  ?? new InventoryTag { Name = tagName };
        //        inv.Tags.Add(tag);
        //        Console.WriteLine($"Добавляем тег: {tag.Name}");
        //    }

        //    //// Добавляем новое пользовательское поле
        //    //var newFieldVm = model.Fields.FieldForm;
        //    //if (!string.IsNullOrWhiteSpace(newFieldVm.Name))
        //    //{
        //    //    Console.WriteLine($"Добавляем новое поле: {newFieldVm.Name}");
        //    //    var newField = new InventoryField
        //    //    {
        //    //        InventoryId = inv.Id,
        //    //        Name = newFieldVm.Name,
        //    //        Description = newFieldVm.Description,
        //    //        Type = newFieldVm.Type,
        //    //        ShowInTable = newFieldVm.ShowInTable,
        //    //        Order = inv.Fields.Any() ? inv.Fields.Max(f => f.Order) + 1 : 1
        //    //    };

        //    //    if (inv.Fields == null)
        //    //    {
        //    //        Console.WriteLine("Навигационное свойство Fields пустое, создаем новый список");
        //    //        inv.Fields = new List<InventoryField>();
        //    //    }

        //    //    inv.Fields.Add(newField);
        //    //}
        //    //else
        //    //{
        //    //    Console.WriteLine("Поле не добавлено: Name пустой");
        //    //}

        //    //Console.WriteLine($"Всего полей после добавления: {inv.Fields.Count}");

        //    //_db.SaveChanges();
        //    //Console.WriteLine("SaveChanges выполнен");

        //    return RedirectToAction("Details", new { id = inv.Id });
        //}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTags(int InventoryId, List<string> Tags)
        {
            var inventory = await _db.Inventories
                .Include(i => i.Tags)
                .FirstOrDefaultAsync(i => i.Id == InventoryId);

            if (inventory == null)
                return NotFound();

            // Удаляем старые связи
            inventory.Tags.Clear();

            foreach (var tagName in Tags.Where(t => !string.IsNullOrWhiteSpace(t)))
            {
                var existingTag = await _db.InventoryTags.FirstOrDefaultAsync(t => t.Name == tagName);

                if (existingTag == null)
                {
                    // создаём новый тег и добавляем его в контекст
                    existingTag = new InventoryTag { Name = tagName };
                    _db.InventoryTags.Add(existingTag);
                }

                // связываем тег с инвентарём
                inventory.Tags.Add(existingTag);
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = "Теги сохранены!";
            return RedirectToAction("Details", new { id = InventoryId });
        }



        public async Task<IActionResult> ByTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return RedirectToAction("Index", "Home");

            var inventories = await _db.Inventories
                .Include(i => i.Items)
                .Where(i => i.Category == tag)
                .ToListAsync();

            ViewBag.Tag = tag;
            return View("Index", inventories);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUserAccess(int inventoryId, string searchTerm)
        {
            Console.WriteLine($"AddUserAccess called: inventoryId={inventoryId}, searchTerm={searchTerm}");

            // Загружаем инвентарь с access_list
            var inventory = await _db.Inventories
                .Include(i => i.access_list)
                .ThenInclude(a => a.user)
                .FirstOrDefaultAsync(i => i.Id == inventoryId);

            if (inventory == null)
                return NotFound();

            // Находим пользователя
            var user = await _userManager.FindByNameAsync(searchTerm)
                       ?? await _userManager.FindByEmailAsync(searchTerm);

            if (user == null)
            {
                TempData["Error"] = $"Пользователь «{searchTerm}» не найден.";
                return RedirectToAction("Details", "Inventories", new { id = inventory.Id });
            }

            // Проверяем, что доступа еще нет
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



        // ====== Удаление пользователя ======
        [HttpPost]
        public async Task<IActionResult> RemoveUserAccess(int inventoryId, Guid userId)
        {
            // Загружаем inventory с access_list
            var inventory = await _db.Inventories
                .Include(i => i.access_list)
                .FirstOrDefaultAsync(i => i.Id == inventoryId);

            if (inventory == null)
                return NotFound();

            // Находим доступ пользователя
            var access = inventory.access_list.FirstOrDefault(a => a.user_id == userId);
            if (access != null)
            {
                _db.AccessInventories.Remove(access); // удаляем запись из промежуточной таблицы
                await _db.SaveChangesAsync();

                // Можно получить имя пользователя для уведомления
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                TempData["Success"] = user != null
                    ? $"Пользователь {user.UserName} удалён."
                    : "Пользователь удалён.";
            }

            return RedirectToAction("Details", new { id = inventoryId });
        }




        [HttpGet]
        public IActionResult GetTagSuggestions(string term)
        {
            var tags = _db.InventoryTags
                .Where(t => t.Name.ToLower().StartsWith(term.ToLower()))
                .Select(t => t.Name)
                .Take(10)
                .ToList();
            return Json(tags);
        }

        //public IActionResult SaveField(InventoryFieldsVM model)
        //{
        //    var field = model.FieldForm;
        //    // === Дебаг ModelState ===
        //    if (!ModelState.IsValid)
        //    {
        //        Console.WriteLine("=== ModelState НЕ валиден ===");
        //        foreach (var kv in ModelState)
        //        {
        //            var key = kv.Key;
        //            foreach (var err in kv.Value.Errors)
        //            {
        //                Console.WriteLine($"Поле: {key}, Ошибка: {err.ErrorMessage}");
        //            }
        //        }

        //        var errors = string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
        //        TempData["Error"] = $"Неверные данные: {errors}";
        //        return RedirectToAction("Details", new { id = field.InventoryId });
        //    }

        //    Console.WriteLine($"=== Начало сохранения поля ===");
        //    Console.WriteLine($"ID поля: {field.Id}");
        //    Console.WriteLine($"InventoryId: {field.InventoryId}");
        //    Console.WriteLine($"Название: {field.Name}");
        //    Console.WriteLine($"Описание: {field.Description}");
        //    Console.WriteLine($"Тип: {field.Type}");
        //    Console.WriteLine($"ShowInTable: {field.ShowInTable}");

        //    // Проверка лимита по типу
        //    var existingCount = _db.InventoryFields
        //        .Count(f => f.InventoryId == field.InventoryId && f.Type == field.Type && f.Id != field.Id);
        //    Console.WriteLine($"Существующих полей такого типа: {existingCount}");

        //    if (field.Id == 0 && existingCount >= 3)
        //    {
        //        TempData["Error"] = $"Нельзя добавить больше 3 полей типа {field.Type}";
        //        Console.WriteLine("Превышен лимит полей по типу!");
        //        return RedirectToAction("Details", new { id = field.InventoryId });
        //    }

        //    if (field.Id == 0)
        //    {
        //        int maxOrder = _db.InventoryFields
        //            .Where(f => f.InventoryId == field.InventoryId)
        //            .Max(f => (int?)f.Order) ?? 0;
        //        field.Order = maxOrder + 1;

        //        Console.WriteLine($"Максимальный порядок: {maxOrder}, Новый порядок: {field.Order}");

        //        var entity = new InventoryField
        //        {
        //            InventoryId = field.InventoryId,
        //            Name = field.Name,
        //            Description = field.Description,
        //            Type = field.Type,
        //            ShowInTable = field.ShowInTable,
        //            Order = field.Order
        //        };

        //        _db.InventoryFields.Add(entity);
        //        Console.WriteLine("Добавлено новое поле в DbContext.");
        //    }
        //    else
        //    {
        //        var existing = _db.InventoryFields.Find(field.Id);
        //        if (existing == null)
        //        {
        //            TempData["Error"] = "Поле не найдено";
        //            Console.WriteLine("Ошибка: поле не найдено при редактировании!");
        //            return RedirectToAction("Details", new { id = field.InventoryId });
        //        }

        //        existing.Name = field.Name;
        //        existing.Description = field.Description;
        //        existing.Type = field.Type;
        //        existing.ShowInTable = field.ShowInTable;
        //        existing.Order = field.Order;

        //        Console.WriteLine("Существующее поле обновлено.");
        //    }

        //    Console.WriteLine("=== Сохраняем изменения в БД ===");
        //    _db.SaveChanges();
        //    Console.WriteLine("=== Изменения сохранены ===");

        //    TempData["Success"] = "Поле успешно сохранено";
        //    return RedirectToAction("Details", new { id = field.InventoryId });
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveField(InventoryFieldsVM vm)
        {
            if (vm.FieldForm.InventoryId == 0)
            {
                TempData["Error"] = "InventoryId не указан";
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


        [HttpPost]
        public IActionResult ReorderFields([FromBody] List<int> orderedIds)
        {
            for (int i = 0; i < orderedIds.Count; i++)
            {
                var field = _db.InventoryFields.FirstOrDefault(f => f.Id == orderedIds[i]);
                if (field != null)
                    field.Order = i;
            }
            _db.SaveChanges();
            return Ok();
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

                    // Правильная обработка типов полей
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
                            // Оставляем как есть, но проверяем на null
                            if (string.IsNullOrEmpty(value))
                                value = null;
                            break;
                    }

                    values[field.Name] = value;
                }

                // Генерируем пользовательский ID
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
                // Логируем ошибку и возвращаем пользователя на форму с сообщением об ошибке
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
            // Если есть пользовательский формат, используем его
            if (!string.IsNullOrEmpty(inventory.CustomIdFormatJson))
            {
                var format = CustomIdFormat.FromJson(inventory.CustomIdFormatJson);
                if (format != null && format.Parts.Any())
                {
                    return await GenerateCustomIdWithFormatAsync(inventory, format);
                }
            }

            // Иначе используем простую генерацию
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
        //    public IActionResult Details(int id)
        //    {
        //        var inventory = _db.Inventories
        //.Include(i => i.Fields)
        //.Include(i => i.Items)
        //    .ThenInclude(it => it.Likes)
        //.Include(i => i.AllowedUsers) // <- важно
        //.FirstOrDefault(i => i.Id == id);


        //        if (inventory == null) return NotFound();

        //        // Подготовка FieldForm для формы
        //        var fieldForm = new InventoryFieldCreateVM
        //        {
        //            InventoryId = inventory.Id
        //        };

        //        // Подготовка списка существующих полей
        //        var fieldsVM = new InventoryFieldsVM
        //        {
        //            Fields = inventory.Fields
        //                .OrderBy(f => f.Order)
        //                .Select(f => new InventoryFieldCreateVM
        //                {
        //                    Id = f.Id,
        //                    InventoryId = f.InventoryId,
        //                    Name = f.Name,
        //                    Description = f.Description,
        //                    Type = f.Type,
        //                    ShowInTable = f.ShowInTable,
        //                    Order = f.Order
        //                })
        //                .ToList(),
        //            FieldForm = fieldForm
        //        };

        //        var viewModel = new InventoryWithItemsViewModel
        //        {
        //            Inventory = inventory,
        //            Items = inventory.Items.Select(item =>
        //            {
        //                var jsonNode = System.Text.Json.Nodes.JsonNode.Parse(item.ValuesJson);

        //                return new InventoryItemViewModel
        //                {
        //                    Id = item.Id,
        //                    InventoryId = item.InventoryId,
        //                    CustomId = item.CustomId,
        //                    CreatedAt = item.CreatedAt,
        //                    Likes = item.Likes,
        //                    ValuesJson = item.ValuesJson,
        //                    FieldValues = inventory.Fields
        //                        .OrderBy(f => f.Order)
        //                        .Select(f => jsonNode?[f.Name]?.ToString() ?? string.Empty)
        //                        .ToList()
        //                };
        //            }).ToList(),
        //            Fields = fieldsVM
        //        };

        //        return View(viewModel);
        //    }

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

            // Формируем список AllowedUsers через access_list
            var allowedUsers = inventory.access_list
                .Select(a => a.user)
                .AsQueryable();

            allowedUsers = sortOrder switch
            {
                "email" => allowedUsers.OrderBy(u => u.Email),
                _ => allowedUsers.OrderBy(u => u.UserName)
            };

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
                AllowedUsers = allowedUsers.ToList() // передаем в ViewModel
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLike(int itemId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier); // string
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
                    UserId = userGuid, // теперь тип совпадает
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