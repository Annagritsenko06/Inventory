namespace CourseWork.Models
{
    public class SearchResultViewModel
    {
        public string Query { get; set; } = "";
        public List<Inventories> Inventories { get; set; } = new();
        public List<InventoryItem> Items { get; set; } = new();
    }

}
