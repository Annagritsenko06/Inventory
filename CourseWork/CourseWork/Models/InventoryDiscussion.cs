using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CourseWork.Models
{
    [Table("inventory_discussions")]
    public class InventoryDiscussion
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("inventory_id")]
        public int InventoryId { get; set; }
        public Inventories? Inventory { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }
        public User? User { get; set; }

        [Required]
        [Column("message")]
        public string Message { get; set; } = null!;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
