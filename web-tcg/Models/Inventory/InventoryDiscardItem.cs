using System.ComponentModel.DataAnnotations;

namespace web_tcg.Models.Inventory;

public sealed class InventoryDiscardItem
{
    [Range(1, int.MaxValue)]
    public int Id { get; set; }
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}
