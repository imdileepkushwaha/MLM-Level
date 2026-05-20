using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MLM_Level.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var mvc = builder.Services.AddControllersWithViews();
if (builder.Environment.IsDevelopment())
{
    mvc.AddRazorRuntimeCompilation();
}

// Register DbContext with MS SQL Server Connection String
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register custom services
builder.Services.AddScoped<MLM_Level.Services.IEmailService, MLM_Level.Services.EmailService>();
builder.Services.AddScoped<MLM_Level.Services.IAdminNotificationService, MLM_Level.Services.AdminNotificationService>();
builder.Services.AddScoped<MLM_Level.Services.IMemberNotificationService, MLM_Level.Services.MemberNotificationService>();
builder.Services.AddHostedService<MLM_Level.Services.RoiDistributionService>();
builder.Services.AddHostedService<MLM_Level.Services.DailyClosingService>();

// Configure Cookie Authentication for Dual Roles (Admin & User)
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "UserAuth";
    })
    .AddCookie("AdminAuth", options =>
    {
        options.Cookie.Name = ".MLM.Admin";
        options.LoginPath = "/Admin/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    })
    .AddCookie("UserAuth", options =>
    {
        options.Cookie.Name = ".MLM.User";
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

var app = builder.Build();

// Auto-migrate & seed database on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        DbInitializer.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while creating or seeding the database.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Ensure static files can be served
app.UseStaticFiles();

app.UseRouting();

// Authentication MUST be called before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
