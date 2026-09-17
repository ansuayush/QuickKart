using GroceryDelivery.Api.Data;
using GroceryDelivery.Api.Models;
using GroceryDelivery.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "JWT Bearer token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddScoped<TokenService>();
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddHostedService<DeliveryProgressService>();

builder.Services.AddCors(o => o.AddPolicy("ReactApp", p =>
    p.SetIsOriginAllowed(origin => origin.StartsWith("http://localhost:") || origin.StartsWith("https://localhost:"))
     .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("ReactApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    TryAddColumn(db, "Addresses", "Latitude", "float NOT NULL CONSTRAINT DF_Addresses_Lat DEFAULT 12.9716");
    TryAddColumn(db, "Addresses", "Longitude", "float NOT NULL CONSTRAINT DF_Addresses_Lng DEFAULT 77.5946");
    TryAddColumn(db, "Orders", "PaymentRef", "nvarchar(100) NOT NULL CONSTRAINT DF_Orders_PayRef DEFAULT ''");
    TryAddColumn(db, "Orders", "OutForDeliveryAt", "datetime2 NULL");
    TryAddColumn(db, "Orders", "StoreLat", "float NOT NULL CONSTRAINT DF_Orders_StoreLat DEFAULT 12.9352");
    TryAddColumn(db, "Orders", "StoreLng", "float NOT NULL CONSTRAINT DF_Orders_StoreLng DEFAULT 77.6245");
    db.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.DeliveryBoys', N'U') IS NULL
BEGIN
    CREATE TABLE [DeliveryBoys](
        [Id] int NOT NULL IDENTITY PRIMARY KEY,
        [Name] nvarchar(max) NOT NULL,
        [Phone] nvarchar(max) NOT NULL,
        [Vehicle] nvarchar(max) NOT NULL,
        [PhotoUrl] nvarchar(max) NOT NULL,
        [IsAvailable] bit NOT NULL,
        [RatingAverage] float NOT NULL,
        [RatingCount] int NOT NULL
    );
END");
    TryAddColumn(db, "Users", "DeliveryBoyId", "int NULL");
    TryAddColumn(db, "Orders", "AssignedAt", "datetime2 NULL");
    TryAddColumn(db, "Orders", "PickedUpAt", "datetime2 NULL");
    TryAddColumn(db, "Orders", "DeliveredAt", "datetime2 NULL");
    TryAddColumn(db, "Orders", "RejectedRiderIds", "nvarchar(max) NOT NULL CONSTRAINT DF_Orders_Rejected DEFAULT ''");
    TryAddColumn(db, "DeliveryBoys", "VehicleType", "nvarchar(max) NOT NULL CONSTRAINT DF_Boys_VType DEFAULT 'Bike'");
    TryAddColumn(db, "DeliveryBoys", "VehicleNumber", "nvarchar(max) NOT NULL CONSTRAINT DF_Boys_VNum DEFAULT ''");
    TryAddColumn(db, "DeliveryBoys", "DutyStatus", "nvarchar(50) NOT NULL CONSTRAINT DF_Boys_Duty DEFAULT 'Available'");
    TryAddColumn(db, "DeliveryBoys", "CurrentLatitude", "float NOT NULL CONSTRAINT DF_Boys_Lat DEFAULT 12.9352");
    TryAddColumn(db, "DeliveryBoys", "CurrentLongitude", "float NOT NULL CONSTRAINT DF_Boys_Lng DEFAULT 77.6245");
    TryAddColumn(db, "DeliveryBoys", "StoreId", "int NOT NULL CONSTRAINT DF_Boys_Store DEFAULT 1");
    TryAddColumn(db, "DeliveryBoys", "IsActive", "bit NOT NULL CONSTRAINT DF_Boys_Active DEFAULT 1");
    TryAddColumn(db, "Users", "PreferredDeliveryBoyId", "int NULL");
    TryAddColumn(db, "Orders", "PreferSameRider", "bit NOT NULL CONSTRAINT DF_Orders_PreferSame DEFAULT 0");
    TryAddColumn(db, "Orders", "DeliveryBoyId", "int NULL");
    TryAddColumn(db, "Orders", "Rating", "int NULL");
    TryAddColumn(db, "Orders", "Review", "nvarchar(max) NOT NULL CONSTRAINT DF_Orders_Review DEFAULT ''");
    TryAddColumn(db, "Orders", "RatedAt", "datetime2 NULL");
    db.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.WishlistItems', N'U') IS NULL
BEGIN
    CREATE TABLE [WishlistItems](
        [Id] int NOT NULL IDENTITY PRIMARY KEY,
        [UserId] int NOT NULL,
        [ProductId] int NOT NULL,
        CONSTRAINT [FK_Wishlist_Users] FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Wishlist_Products] FOREIGN KEY ([ProductId]) REFERENCES [Products]([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_Wishlist_User_Product] ON [WishlistItems]([UserId], [ProductId]);
END");
    db.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.ContactInfos', N'U') IS NULL
BEGIN
    CREATE TABLE [ContactInfos](
        [Id] int NOT NULL IDENTITY PRIMARY KEY,
        [Company] nvarchar(max) NOT NULL,
        [Email] nvarchar(max) NOT NULL,
        [Phone] nvarchar(max) NOT NULL,
        [WhatsApp] nvarchar(max) NOT NULL,
        [Address] nvarchar(max) NOT NULL,
        [Hours] nvarchar(max) NOT NULL,
        [Note] nvarchar(max) NOT NULL
    );
END");
    Seed(db);
}

app.Run();

static void Seed(AppDbContext db)
{
    if (!db.Users.Any())
    {
        db.Users.AddRange(
            new User { Name = "QuickKart Admin", Email = "admin@quickkart.local", Phone = "9999999999", Role = "Admin", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123") },
            new User { Name = "Demo Customer", Email = "customer@quickkart.local", Phone = "8888888888", Role = "Customer", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123") });
        db.SaveChanges();
        db.Addresses.Add(new Address
        {
            UserId = db.Users.First(u => u.Role == "Customer").Id,
            Label = "Home",
            Line1 = "12 MG Road",
            City = "Bengaluru",
            Pincode = "560001",
            Phone = "8888888888",
            IsDefault = true,
            Latitude = 12.9750,
            Longitude = 77.6050
        });
        db.SaveChanges();
    }

    if (!db.Categories.Any())
    {
        db.Categories.AddRange(
            new Category { Name = "Fruits", Icon = "🍌" },
            new Category { Name = "Vegetables", Icon = "🥦" },
            new Category { Name = "Dairy", Icon = "🥛" },
            new Category { Name = "Bakery", Icon = "🍞" },
            new Category { Name = "Snacks", Icon = "🍿" },
            new Category { Name = "Beverages", Icon = "🧃" },
            new Category { Name = "Pharmacy", Icon = "💊" },
            new Category { Name = "Personal Care", Icon = "🧴" });
        db.SaveChanges();
    }

    if (!db.Products.Any())
    {
        int GroceryCat(string name) => db.Categories.First(c => c.Name == name).Id;
        db.Products.AddRange(
            new Product { Name = "Banana", Description = "Fresh bananas", Price = 40, Unit = "1 dozen", Stock = 80, CategoryId = GroceryCat("Fruits"), ImageUrl = "/images/banana.svg" },
            new Product { Name = "Apple", Description = "Shimla apples", Price = 120, Unit = "1 kg", Stock = 50, CategoryId = GroceryCat("Fruits"), ImageUrl = "/images/apple.svg" },
            new Product { Name = "Tomato", Description = "Farm fresh tomatoes", Price = 30, Unit = "1 kg", Stock = 90, CategoryId = GroceryCat("Vegetables"), ImageUrl = "/images/tomato.svg" },
            new Product { Name = "Fresh Milk", Description = "Full cream milk", Price = 60, Unit = "1 litre", Stock = 40, CategoryId = GroceryCat("Dairy"), ImageUrl = "/images/milk.svg" },
            new Product { Name = "Brown Bread", Description = "Fresh bakery bread", Price = 45, Unit = "400 g", Stock = 35, CategoryId = GroceryCat("Bakery"), ImageUrl = "/images/bread.svg" },
            new Product { Name = "Potato Chips", Description = "Crunchy salted chips", Price = 20, Unit = "50 g", Stock = 120, CategoryId = GroceryCat("Snacks"), ImageUrl = "/images/chips.svg" },
            new Product { Name = "Orange Juice", Description = "No added sugar", Price = 90, Unit = "1 litre", Stock = 25, CategoryId = GroceryCat("Beverages"), ImageUrl = "/images/juice.svg" });
        db.SaveChanges();
    }

    foreach (var extra in new[] { ("Pharmacy", "💊"), ("Personal Care", "🧴") })
    {
        if (!db.Categories.Any(c => c.Name == extra.Item1))
            db.Categories.Add(new Category { Name = extra.Item1, Icon = extra.Item2 });
    }
    db.SaveChanges();

    int Cat(string name) => db.Categories.First(c => c.Name == name).Id;
    void AddIfMissing(string name, string desc, decimal price, string unit, int stock, string cat, string img)
    {
        if (db.Products.Any(p => p.Name == name)) return;
        db.Products.Add(new Product { Name = name, Description = desc, Price = price, Unit = unit, Stock = stock, CategoryId = Cat(cat), ImageUrl = img, IsActive = true });
    }
    AddIfMissing("Paracetamol 500mg", "Generic fever and pain relief tablets", 28, "15 tablets", 200, "Pharmacy", "/images/medicine.svg");
    AddIfMissing("Cetirizine 10mg", "Generic anti-allergy tablets", 35, "10 tablets", 160, "Pharmacy", "/images/medicine.svg");
    AddIfMissing("Antiseptic Cream", "For minor cuts and skin irritation", 65, "20 g", 90, "Pharmacy", "/images/cream.svg");
    AddIfMissing("Moisturising Cream", "Daily body cream for dry skin", 99, "100 ml", 70, "Personal Care", "/images/cream.svg");
    AddIfMissing("Body Spray", "Fresh fragrance body spray", 149, "150 ml", 55, "Personal Care", "/images/spray.svg");
    AddIfMissing("Hand Sanitizer", "Alcohol-based sanitizer", 45, "100 ml", 120, "Personal Care", "/images/spray.svg");
    db.SaveChanges();

    if (!db.ContactInfos.Any())
    {
        db.ContactInfos.Add(new ContactInfo());
        db.SaveChanges();
    }

    if (!db.DeliveryBoys.Any())
    {
        db.DeliveryBoys.AddRange(
            new DeliveryBoy { Name = "Ravi Kumar", Phone = "9876500001", Vehicle = "Bike · TS09 AB 1122", VehicleType = "Bike", VehicleNumber = "TS09 AB 1122", DutyStatus = "Available", CurrentLatitude = 12.9360, CurrentLongitude = 77.6250, StoreId = 1, IsActive = true, PhotoUrl = "/images/rider.svg", IsAvailable = true, RatingAverage = 4.8, RatingCount = 42 },
            new DeliveryBoy { Name = "Suresh Naik", Phone = "9876500002", Vehicle = "Scooter · TS07 CD 3344", VehicleType = "Scooter", VehicleNumber = "TS07 CD 3344", DutyStatus = "Available", CurrentLatitude = 12.9410, CurrentLongitude = 77.6310, StoreId = 1, IsActive = true, PhotoUrl = "/images/rider.svg", IsAvailable = true, RatingAverage = 4.6, RatingCount = 31 },
            new DeliveryBoy { Name = "Amit Singh", Phone = "9876500003", Vehicle = "Bike · TS10 EF 5566", VehicleType = "Bike", VehicleNumber = "TS10 EF 5566", DutyStatus = "Available", CurrentLatitude = 12.9280, CurrentLongitude = 77.6180, StoreId = 1, IsActive = true, PhotoUrl = "/images/rider.svg", IsAvailable = true, RatingAverage = 4.9, RatingCount = 58 });
        db.SaveChanges();
    }
    else
    {
        var offsets = new (string Name, double Lat, double Lng, string Type, string Num)[]
        {
            ("Ravi Kumar", 12.9360, 77.6250, "Bike", "TS09 AB 1122"),
            ("Suresh Naik", 12.9410, 77.6310, "Scooter", "TS07 CD 3344"),
            ("Amit Singh", 12.9280, 77.6180, "Bike", "TS10 EF 5566")
        };
        foreach (var o in offsets)
        {
            var boy = db.DeliveryBoys.FirstOrDefault(b => b.Name == o.Name);
            if (boy is null) continue;
            if (boy.CurrentLatitude == 0) { boy.CurrentLatitude = o.Lat; boy.CurrentLongitude = o.Lng; }
            if (string.IsNullOrWhiteSpace(boy.VehicleNumber)) { boy.VehicleType = o.Type; boy.VehicleNumber = o.Num; }
            if (string.IsNullOrWhiteSpace(boy.DutyStatus)) boy.DutyStatus = boy.IsAvailable ? "Available" : "Assigned";
            boy.IsActive = true;
        }
        db.SaveChanges();
    }

    void EnsureRiderUser(string name, string email, string phone, string riderName)
    {
        var boy = db.DeliveryBoys.FirstOrDefault(b => b.Name == riderName);
        if (boy is null) return;
        var user = db.Users.FirstOrDefault(u => u.Email == email);
        if (user is null)
        {
            db.Users.Add(new User
            {
                Name = name,
                Email = email,
                Phone = phone,
                Role = "DeliveryBoy",
                DeliveryBoyId = boy.Id,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Rider@123")
            });
        }
        else
        {
            user.Role = "DeliveryBoy";
            user.DeliveryBoyId = boy.Id;
        }
    }
    EnsureRiderUser("Ravi Kumar", "ravi@quickkart.local", "9876500001", "Ravi Kumar");
    db.SaveChanges();

    var pictures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Banana"] = "/images/banana.svg",
        ["Apple"] = "/images/apple.svg",
        ["Tomato"] = "/images/tomato.svg",
        ["Fresh Milk"] = "/images/milk.svg",
        ["Brown Bread"] = "/images/bread.svg",
        ["Potato Chips"] = "/images/chips.svg",
        ["Orange Juice"] = "/images/juice.svg"
    };
    foreach (var product in db.Products)
    {
        if (pictures.TryGetValue(product.Name, out var url))
            product.ImageUrl = url;
        else if (string.IsNullOrWhiteSpace(product.ImageUrl) || product.ImageUrl.Contains("unsplash.com"))
            product.ImageUrl = "/images/grocery.svg";
    }
    db.SaveChanges();
}

static void TryAddColumn(AppDbContext db, string table, string column, string sqlType)
{
    db.Database.ExecuteSqlRaw($@"
IF COL_LENGTH('{table}', '{column}') IS NULL
    ALTER TABLE [{table}] ADD [{column}] {sqlType};");
}
