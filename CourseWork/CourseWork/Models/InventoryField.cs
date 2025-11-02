using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CourseWork.Models
{
    public enum FieldType { TextSingle, TextMulti, Number, ImageLink, Boolean }


    public class InventoryField
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
       
        [Column("name")]
        public string Name { get; set; } = null!;
        [Column("description")]
        public string? Description { get; set; }
        [Column("type")]
     
        public FieldType Type { get; set; }
        [Column("show_in_table")]
        public bool ShowInTable { get; set; }
        [Column("order")]
        public int Order { get; set; }
        [Column("inventory_id")]
        public int InventoryId { get; set; }
        public Inventories? Inventory { get; set; }
    }
}
