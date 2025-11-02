namespace CourseWork.Models
{
    public class ItemDetailsViewModel
    {
        public InventoryItemViewModel Item { get; set; } = null!;
        public Inventories Inventory { get; set; } = null!;
        public bool IsEdit { get; set; }
    }

}
