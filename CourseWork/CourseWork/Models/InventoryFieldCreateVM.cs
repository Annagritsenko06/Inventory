using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace CourseWork.Models
{
    
    public class InventoryFieldsVM
    {
     
        public List<InventoryFieldCreateVM> Fields { get; set; } = new();
        public InventoryFieldCreateVM FieldForm { get; set; } = new InventoryFieldCreateVM();
    }


    public class InventoryFieldCreateVM
    {
        public int Id { get; set; }
        public int InventoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public FieldType Type { get; set; }
        public bool ShowInTable { get; set; }
        public int Order { get; set; }
    }

}
