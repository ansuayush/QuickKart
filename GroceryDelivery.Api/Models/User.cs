namespace GroceryDelivery.Api.Models;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore]
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Customer";
    public int? PreferredDeliveryBoyId { get; set; }
    public int? DeliveryBoyId { get; set; }
    public List<Address> Addresses { get; set; } = new();
    public List<CartItem> CartItems { get; set; } = new();
    public List<Order> Orders { get; set; } = new();
}
