// API- Project

using API_Electronic.Data;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using API_Electronic.Services;
using Microsoft.Extensions.FileProviders;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ? Load configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// ? Add DbContext
builder.Services.AddDbContext<AuthDbContext>(options =>
   options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ??
       throw new InvalidOperationException("Connection string 'DefaultConnection' not found.")));

// ? Configure Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
   options.Password.RequireDigit = true;
   options.Password.RequiredLength = 8;
   options.Password.RequireNonAlphanumeric = false;
})
   .AddEntityFrameworkStores<AuthDbContext>()
   .AddDefaultTokenProviders();

// ? Configure JWT Authentication
var jwtKey = builder.Configuration["JwtSettings:Key"]
   ?? throw new InvalidOperationException("JWT Key is missing.");

if (jwtKey.Length < 32)
   throw new InvalidOperationException("JWT Key must be at least 32 characters long.");

var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
   options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
   options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
   options.RequireHttpsMetadata = builder.Environment.IsProduction();
   options.SaveToken = true;
   options.TokenValidationParameters = new TokenValidationParameters
   {
       ValidateIssuerSigningKey = true,
       IssuerSigningKey = new SymmetricSecurityKey(key),
       ValidateIssuer = true,
       ValidIssuer = builder.Configuration["JwtSettings:Issuer"]
           ?? throw new InvalidOperationException("JWT Issuer is missing."),
       ValidateAudience = true,
       ValidAudience = builder.Configuration["JwtSettings:Audience"]
           ?? throw new InvalidOperationException("JWT Audience is missing."),
       ValidateLifetime = true,
       ClockSkew = TimeSpan.FromSeconds(5) // Reduce for stricter validation
   };
});

builder.Services.AddAuthorization();

// ? Add Controllers & FluentValidation
builder.Services.AddControllers()
   .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<Program>());

// ? Register Services & Repositories
builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<CategoryRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<OrderRepository>();
builder.Services.AddScoped<OrderItemsRepository>();
builder.Services.AddScoped<PaymentRepository>();
builder.Services.AddScoped<CartRepository>();
builder.Services.AddScoped<JwtService>();

// ? Swagger Configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
   options.SwaggerDoc("v1", new OpenApiInfo { Title = "Auth Demo", Version = "v1" });
   options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
   {
       In = ParameterLocation.Header,
       Description = "Please enter JWT with 'Bearer ' prefix",
       Name = "Authorization",
       Type = SecuritySchemeType.Http,
       BearerFormat = "JWT",
       Scheme = "bearer"
   });
   options.AddSecurityRequirement(new OpenApiSecurityRequirement
   {
       {
           new OpenApiSecurityScheme
           {
               Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
           },
           Array.Empty<string>()
       }
   });
});

var app = builder.Build();

// ? Middleware Pipeline
if (app.Environment.IsDevelopment())
{
   app.UseSwagger();
   app.UseSwaggerUI();
}

// ? Serve Static Images
var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "Images");
Directory.CreateDirectory(imagesPath); // Ensure folder exists

app.UseStaticFiles(new StaticFileOptions
{
   FileProvider = new PhysicalFileProvider(imagesPath),
   RequestPath = "/images"
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
