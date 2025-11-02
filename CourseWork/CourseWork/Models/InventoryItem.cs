using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace CourseWork.Models
{
    public class InventoryWithItemsViewModel
    {
        public Inventories Inventory { get; set; }
        public List<InventoryItemViewModel> Items { get; set; }

        public InventoryFieldsVM? Fields { get; set; } = new InventoryFieldsVM();
        public string? SearchTerm { get; set; }
        public string SortOrder { get; set; } = "name";
        public List<User> AllowedUsers { get; set; }
    }
    public class InventoryItem
    {
        [Column("id")]
        public int Id { get; set; }
        [Column("inventory_id")]
        public int InventoryId { get; set; }
        public Inventories? Inventory { get; set; }


        // Custom editable user-facing ID
        [MaxLength(200)]
        [Column("custom_id")]
        public string CustomId { get; set; } = string.Empty;

        [Column("created_by_id")]
        public string CreatedById { get; set; } = null!;
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("values_json")]
        // Store field values as JSON to avoid many DB columns
        public string ValuesJson { get; set; } = JsonSerializer.Serialize(new { });

        // Version for optimistic locking
        [Column("version")]
        public int Version { get; set; } = 1;

        // Likes functionality
        public ICollection<ItemLike> Likes { get; set; } = new List<ItemLike>();
       

        public T? GetValues<T>() where T : class
        {
            return JsonSerializer.Deserialize<T>(ValuesJson);
        }
    }
}
