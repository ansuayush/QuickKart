namespace GroceryDelivery.Api.Models;

public class ContactInfo
{
    public int Id { get; set; }
    public string Company { get; set; } = "QuickKart Commerce Private Limited";
    public string Email { get; set; } = "support@quickkart.local";
    public string Phone { get; set; } = "1800-202-2026";
    public string WhatsApp { get; set; } = "9999999999";
    public string Address { get; set; } = "Plot 12, HITEC City, Hyderabad, Telangana 500081";
    public string Hours { get; set; } = "Everyday, 7:00 AM – 11:00 PM";
    public string Note { get; set; } = "We typically reply within a few minutes during store hours.";
}

public record ContactRequest(string Company, string Email, string Phone, string WhatsApp, string Address, string Hours, string Note);
