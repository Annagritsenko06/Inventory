using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Metadata;
namespace CourseWork.Models
{
    
        public class Inventories
        {
        public enum InventoryCategory
        {
            Equipment,
            Furniture,
            Book,
            Other
        }
        [Column("id")]
        public int Id { get; set; }
            [Required]
            [Column("name")]
        public string Name { get; set; } = null!;
        [Column("description")]
        public string? Description { get; set; }
        [Column("category")]
      public InventoryCategory Category { get; set; } = InventoryCategory.Other; [Column("owner_id")]
        public Guid OwnerId { get; set; } = Guid.Empty!;
        [Column("is_public")]
        public bool IsPublic { get; set; }
        [Column("image_url")]
        public string? ImageUrl { get; set; } // ссылка в облаке
        [Column("custom_id_format_json", TypeName = "jsonb")]

        public string? CustomIdFormatJson { get; set; }

        public ICollection<InventoryItem> Items { get; set; } = new List<InventoryItem>();

        public ICollection<AccessInventory> access_list { get; set; } = new List<AccessInventory>();

          public ICollection<InventoryField> Fields { get; set; } = new List<InventoryField>();
        public ICollection<InventoryTag> Tags { get; set; } = new List<InventoryTag>();
      [NotMapped] // EF не будет пытаться вставлять AllowedUsers напрямую
    public IEnumerable<User> AllowedUsers => access_list.Select(a => a.user);


    }
}
