using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MLM_Level.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddMemoryCache();
var mvc = builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<MLM_Level.Filters.MaintenanceModeFilter>();
});
if (builder.Environment.IsDevelopment())
{
    mvc.AddRazorRuntimeCompilation();
}

// Register DbContext with MS SQL Server Connection String
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
            sqlOptions.CommandTimeout(60);
        }));

// Register custom services
builder.Services.AddScoped<MLM_Level.Services.IEmailService, MLM_Level.Services.EmailService>();
builder.Services.AddScoped<MLM_Level.Services.IMaintenanceModeService, MLM_Level.Services.MaintenanceModeService>();
builder.Services.AddScoped<MLM_Level.Services.IAdminNotificationService, MLM_Level.Services.AdminNotificationService>();
builder.Services.AddScoped<MLM_Level.Services.IMemberNotificationService, MLM_Level.Services.MemberNotificationService>();
builder.Services.AddScoped<MLM_Level.Services.IMemberIdService, MLM_Level.Services.MemberIdService>();
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

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = false;
});

var app = builder.Build();

// Auto-migrate & seed database on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<ApplicationDbContext>();

    for (var attempt = 1; attempt <= 3; attempt++)
    {
        try
        {
            DbInitializer.Initialize(context);
            break;
        }
        catch (Exception ex) when (attempt < 3)
        {
            logger.LogWarning(ex, "Database init attempt {Attempt} failed. Retrying...", attempt);
            await Task.Delay(TimeSpan.FromSeconds(5 * attempt));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialization failed after {Attempt} attempts.", attempt);
        }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseHsts();
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
