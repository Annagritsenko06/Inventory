using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CourseWork.Models
{
    [Table("users")]
    public class User : IdentityUser<Guid>
    {
        public DateTime RegistrationTime { get; set; } = DateTime.UtcNow;

        public string Status { get; set; } = "";
        public virtual ICollection<Inventories> AccessibleInventories { get; set; } = new List<Inventories>();

    }
}
