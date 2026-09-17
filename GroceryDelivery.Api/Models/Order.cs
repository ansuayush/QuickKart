namespace GroceryDelivery.Api.Models;

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public int UserId { get; set; }
    public User? User { get; set; }
    public int AddressId { get; set; }
    public Address? Address { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Total { get; set; }
    public string PaymentMethod { get; set; } = "COD";
    public string PaymentStatus { get; set; } = "Pending";
    public string PaymentRef { get; set; } = "";
    public string Status { get; set; } = "Placed";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? OutForDeliveryAt { get; set; }
    public double StoreLat { get; set; } = 12.9352;
    public double StoreLng { get; set; } = 77.6245;
    public int? DeliveryBoyId { get; set; }
    public DeliveryBoy? DeliveryBoy { get; set; }
    public int? Rating { get; set; }
    public string Review { get; set; } = "";
    public DateTime? RatedAt { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string RejectedRiderIds { get; set; } = "";
    public bool PreferSameRider { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}
