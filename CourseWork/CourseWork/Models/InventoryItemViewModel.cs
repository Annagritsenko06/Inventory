using System.Text.Json;

namespace CourseWork.Models
{
    public class InventoryItemViewModel
    {
        public int Id { get; set; }
        public int InventoryId { get; set; }
        public string CustomId { get; set; } = string.Empty;
        public string CreatedById { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int Version { get; set; } = 1;

        public ICollection<ItemLike> Likes { get; set; } = new List<ItemLike>();

        public string ValuesJson { get; set; } = "{}";

        public List<string> FieldValues { get; set; } = new List<string>();

        public T? GetValues<T>() where T : class
        {
            return JsonSerializer.Deserialize<T>(ValuesJson);
        }
    }

}
