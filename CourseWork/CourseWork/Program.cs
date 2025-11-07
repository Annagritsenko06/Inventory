
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using System.Globalization;
using System.IO;
using CourseWork.Models;
using CourseWork.Services;
using CourseWork.Controllers;

var builder = WebApplication.CreateBuilder(args);

// ---------------- AUTH ----------------
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie()
.AddGoogle(googleOptions =>
{
    googleOptions.ClientId = builder.Configuration["Authentication:Google:ClientId"];
    googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    googleOptions.CallbackPath = "/signin-google";
    googleOptions.SaveTokens = true;
})
.AddFacebook(facebookOptions =>
{
    facebookOptions.AppId = builder.Configuration["Authentication:Facebook:AppId"];
    facebookOptions.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"];
    facebookOptions.CallbackPath = "/signin-facebook";
});

// ---------------- Cookie Settings ----------------
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "Identity.Application";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;
});


// ---------------- Data Protection ----------------
var keysFolder = Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys");
Directory.CreateDirectory(keysFolder);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysFolder))
    .SetApplicationName("InventoryApp");
// Проверка, что папка для ключей существует и доступна для записи
if (!Directory.Exists(keysFolder))
{
    Console.WriteLine($"[ERROR] Папка для ключей не существует: {keysFolder}");
}
else
{
    Console.WriteLine($"[OK] Папка для ключей существует: {keysFolder}");

    try
    {
        var testFile = Path.Combine(keysFolder, "test.txt");
        File.WriteAllText(testFile, "test");
        File.Delete(testFile);
        Console.WriteLine("[OK] Папка доступна для записи");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Папка недоступна для записи: {ex.Message}");
    }
}


// ---------------- HTTPS ----------------
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
    options.HttpsPort = 443;
});

builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(60);
});

// ---------------- MVC + Localization ----------------
builder.Services.AddControllersWithViews()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization()
    .AddSessionStateTempDataProvider();

// ---------------- EF + Identity ----------------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        })
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
);

builder.Services.AddIdentity<User, IdentityRole<Guid>>(options =>
{
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ ";
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ---------------- Session ----------------
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();

// ---------------- Localization ----------------
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "en-US", "ru-RU" };
    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();
    options.SupportedUICultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();
    options.RequestCultureProviders = new[] { new CookieRequestCultureProvider() };
});

// ---------------- Build ----------------
var app = builder.Build();

// ---------------- Middleware ----------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync("Произошла ошибка аутентификации. Попробуйте снова.");
    });
});
// 1️⃣ Forwarded Headers - до всего, чтобы HTTPS и Scheme корректно работали
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    KnownProxies = { },
    KnownNetworks = { }
});

app.UseHttpsRedirection();
app.UseStaticFiles();

// 2️⃣ Localization
var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(locOptions);

app.UseRouting();

// 3️⃣ Session перед Authentication
app.UseSession();

// 4️⃣ Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// ---------------- Ensure Admin ----------------
using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<RegistrationController>>();

    var registrationController = new RegistrationController(userManager, null, roleManager, logger);
    await registrationController.EnsureAdminExists();
}

// ---------------- Routes ----------------
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<CourseWork.Hubs.DiscussionHub>("/discussionHub");

app.Run();
