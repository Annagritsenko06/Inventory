namespace CourseWork.Models
{
    public class UserProfile
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime RegistrationTime { get; set; }
        public bool IsAdmin { get; set; }
        public List<Inventories> OwnedTemplates { get; set; } = new List<Inventories>();
        public List<Inventories> WritableTemplates { get; set; } = new List<Inventories>();
    }
}
