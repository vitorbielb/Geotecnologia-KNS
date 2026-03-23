using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Configurar a conexão com o banco de dados
builder.Configuration.AddJsonFile("appsettings.json");

// Adicionar serviços ao contêiner
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("GeotecnologiaKNS"));
}, ServiceLifetime.Scoped);

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>()
                .AddUserManager<UserManager<ApplicationUser>>()
                .AddRoles<ApplicationRole>()
                .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    // Cookie settings
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);

    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy("UserCanUpdateSolicitacoes", policy => policy.RequireOperation(x => x.Solicitacao.Update));
    options.AddPolicy("UserCanUpdateCartografias", policy => policy.RequireOperation(x => x.Cartografia.Update));
    options.AddPolicy("UserCanTenantCreate", policy => policy.RequireOperation(x => x.Tenant.Create));
    options.AddPolicy("UserCanUserCreate", policy => policy.RequireOperation(x => x.User.Create));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IIndustriaRepository, IndustriaRepository>();
builder.Services.AddScoped<ISolicitacaoRepository, SolicitacaoRepository>();
builder.Services.AddScoped<IProdutorRepository, ProdutorRepository>();
builder.Services.AddScoped<IPropriedadeRepository, PropriedadeRepository>();
builder.Services.AddScoped<ICartografiaRepository, CartografiaRepository>();
builder.Services.AddControllersWithViews();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining(typeof(Program));
builder.Services.AddAdminPanel();
builder.Services.AddScoped<ImageLoader>();
builder.Services.AddScoped<IUserContext, UserContext>();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
var defaultCultureInfo = new CultureInfo("pt-BR");
defaultCultureInfo.NumberFormat.NumberDecimalSeparator = ".";
defaultCultureInfo.NumberFormat.CurrencyDecimalSeparator = ".";

app.UseRequestLocalization(options =>
{
    options.DefaultRequestCulture = new RequestCulture(defaultCultureInfo);

    options.SupportedCultures = new []
    {
        defaultCultureInfo,
    };

    options.SupportedUICultures = new []
    {
        defaultCultureInfo,
    };
});

await app.UpdateDatabaseAsync();
await app.SeedRoleClaimsAsync();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();
app.Run();



// Optimization pass: 2025-04-07 01:23:53

// Optimization pass: 2025-04-10 22:09:08

// Optimization pass: 2025-04-11 16:37:24

// Optimization pass: 2025-04-16 09:18:58

// Optimization pass: 2025-04-17 09:25:14

// Optimization pass: 2025-05-02 21:26:33

// Optimization pass: 2025-05-09 01:27:19

// Optimization pass: 2025-05-15 00:16:29

// Optimization pass: 2025-05-21 06:53:56

// Optimization pass: 2025-05-21 21:54:51

// Optimization pass: 2025-05-29 10:53:01

// Optimization pass: 2025-08-04 22:19:24

// Optimization pass: 2025-08-11 05:33:46

// Optimization pass: 2025-08-13 22:33:47

// Optimization pass: 2025-08-15 18:32:33

// Optimization pass: 2025-08-19 11:22:43

// Optimization pass: 2025-08-20 19:12:10

// Optimization pass: 2025-08-25 05:34:09

// Optimization pass: 2025-09-03 05:44:35

// Optimization pass: 2025-09-04 19:33:49

// Optimization pass: 2025-09-05 22:27:35

// Optimization pass: 2025-09-11 14:54:12

// Optimization pass: 2025-09-15 19:45:35

// Optimization pass: 2025-09-18 06:28:00

// Optimization pass: 2025-09-22 23:24:48

// Optimization pass: 2025-09-25 13:03:56

// Optimization pass: 2025-09-29 05:05:31

// Optimization pass: 2025-10-01 04:50:36

// Optimization pass: 2025-10-08 12:33:07

// Optimization pass: 2025-10-10 12:08:32

// Optimization pass: 2025-10-14 09:18:45

// Optimization pass: 2025-10-16 16:55:47

// Optimization pass: 2025-10-20 02:57:13

// Optimization pass: 2025-10-22 20:55:30

// Optimization pass: 2025-10-24 11:37:21
