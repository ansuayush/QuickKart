namespace GroceryDelivery.Api.Models;

public class DeliveryBoy
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Vehicle { get; set; } = "Bike";
    public string VehicleType { get; set; } = "Bike";
    public string VehicleNumber { get; set; } = "";
    public string DutyStatus { get; set; } = "Available";
    public double CurrentLatitude { get; set; } = 12.9352;
    public double CurrentLongitude { get; set; } = 77.6245;
    public int StoreId { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public string PhotoUrl { get; set; } = "/images/rider.svg";
    public bool IsAvailable { get; set; } = true;
    public double RatingAverage { get; set; }
    public int RatingCount { get; set; }
    public List<Order> Orders { get; set; } = new();
}
