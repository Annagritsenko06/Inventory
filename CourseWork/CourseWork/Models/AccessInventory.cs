using System.ComponentModel.DataAnnotations.Schema;

namespace CourseWork.Models
{
    
    public enum access_type { Write =1 }

    public class AccessInventory
    {
        public int id { get; set; }
        public int inventory_template_id { get; set; }
        public Inventories inventory_template { get; set; }


        public Guid user_id { get; set; }
        [ForeignKey(nameof(user_id))] 
        public User user { get; set; }

        public access_type type { get; set; }
    }
}
