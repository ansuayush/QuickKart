namespace GroceryDelivery.Api.Models;

public class Address
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public string Label { get; set; } = "Home";
    public string Line1 { get; set; } = "";
    public string City { get; set; } = "";
    public string Pincode { get; set; } = "";
    public string Phone { get; set; } = "";
    public bool IsDefault { get; set; }
    public double Latitude { get; set; } = 12.9716;
    public double Longitude { get; set; } = 77.5946;
}
