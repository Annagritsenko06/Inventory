using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CourseWork.Models
{
    [Table("tags")]
    public class InventoryTag
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; } = null!;

        public ICollection<Inventories> Inventories { get; set; } = new List<Inventories>();
    }
}
