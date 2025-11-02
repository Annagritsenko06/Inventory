namespace CourseWork.Models{ 

public class UserRolesViewModel
{
    public User User { get; set; }
    public List<string> Roles { get; set; } = new List<string>();
}
}
