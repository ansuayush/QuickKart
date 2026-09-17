namespace GroceryDelivery.Api.Models;

public record RegisterRequest(string Name, string Email, string Phone, string Password);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, int UserId, string Name, string Email, string Role);
public record CartItemRequest(int ProductId, int Quantity);
public record AddressRequest(string Label, string Line1, string City, string Pincode, string Phone, bool IsDefault, double? Latitude, double? Longitude);
public record CheckoutRequest(int AddressId, string PaymentMethod, string? PaymentRef);
public record ChargeRequest(string Method, decimal Amount, string? UpiId, string? CardNumber, string? Expiry, string? Cvv);
public record ProductRequest(string Name, string Description, decimal Price, string Unit, string ImageUrl, int Stock, bool IsActive, int CategoryId);
public record AssignRiderRequest(int DeliveryBoyId);
public record RatingRequest(int Stars, string? Review, bool PreferSameRider = false);
public record DeliveryBoyRequest(string Name, string Phone, string VehicleType, string VehicleNumber, string? Email, string? Password, bool IsAvailable);
public record AdminUserRequest(string Name, string Email, string Phone, string? Password);
