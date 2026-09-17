namespace GroceryDelivery.Api.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "🛒";
    public List<Product> Products { get; set; } = new();
}
