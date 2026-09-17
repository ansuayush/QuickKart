using GroceryDelivery.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GroceryDelivery.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<DeliveryBoy> DeliveryBoys => Set<DeliveryBoy>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<ContactInfo> ContactInfos => Set<ContactInfo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
        modelBuilder.Entity<Order>().Property(o => o.Subtotal).HasPrecision(18, 2);
        modelBuilder.Entity<Order>().Property(o => o.DeliveryFee).HasPrecision(18, 2);
        modelBuilder.Entity<Order>().Property(o => o.Total).HasPrecision(18, 2);
        modelBuilder.Entity<OrderItem>().Property(i => i.UnitPrice).HasPrecision(18, 2);

        modelBuilder.Entity<Product>().HasOne(p => p.Category).WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CartItem>().HasIndex(c => new { c.UserId, c.ProductId }).IsUnique();
        modelBuilder.Entity<CartItem>().HasOne(c => c.Product).WithMany()
            .HasForeignKey(c => c.ProductId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Order>().HasOne(o => o.Address).WithMany()
            .HasForeignKey(o => o.AddressId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Order>().HasOne(o => o.DeliveryBoy).WithMany(d => d.Orders)
            .HasForeignKey(o => o.DeliveryBoyId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<OrderItem>().HasOne(i => i.Product).WithMany()
            .HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<WishlistItem>().HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();
        modelBuilder.Entity<WishlistItem>().HasOne(w => w.Product).WithMany()
            .HasForeignKey(w => w.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
