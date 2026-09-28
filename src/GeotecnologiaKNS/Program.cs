using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Não adicionar appsettings.json aqui: CreateBuilder já o carregou, e recarregá-lo
// o coloca no fim da cadeia, com prioridade sobre user-secrets e variáveis de
// ambiente. Era por isso que um valor vazio no arquivo vencia o segredo
// configurado — e, em produção, venceria a variável de ambiente.

// Adicionar serviços ao contêiner
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("GeotecnologiaKNS"));
}, ServiceLifetime.Scoped);

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
                {
                    // O UserName aqui é o nome da pessoa, não um apelido de
                    // login — quem entra no sistema usa o e-mail. O conjunto
                    // padrão do Identity não tem espaço nem acento, e isso
                    // reprovava "Ana Prado" ou "João Gonçalves" na hora de
                    // atribuir o papel, deixando o usuário sem permissão
                    // nenhuma.
                    options.User.AllowedUserNameCharacters =
                        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+" +
                        "áàâãäéèêëíìîïóòôõöúùûüçñÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑ' ";
                })
                .AddUserManager<UserManager<ApplicationUser>>()
                .AddRoles<ApplicationRole>()
                .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.Configure<GoogleMapsOptions>(builder.Configuration.GetSection(GoogleMapsOptions.SectionName));

// Bases geoespaciais de referência (PostGIS). Sem a connection string "Geo"
// configurada, o app segue funcionando com o cadastro manual.
builder.Services.AddGeo(builder.Configuration.GetConnectionString("Geo"));

builder.Services.ConfigureApplicationCookie(options =>
{
    // Cookie settings
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;

    // Em desenvolvimento o perfil de launch "http" não tem HTTPS; exigir o cookie
    // seguro ali impediria o login.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;

    // Com SlidingExpiration a janela é renovada a cada requisição;
    // 5 minutos derrubavam o usuário no meio de um preenchimento de formulário.
    options.ExpireTimeSpan = TimeSpan.FromMinutes(
        builder.Configuration.GetValue<int?>("Identity:CookieExpirationMinutes") ?? 60);

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
builder.Services.AddScoped<IPropriedadeCarService, PropriedadeCarService>();
builder.Services.AddScoped<IMotorDeRegras, MotorDeRegras>();
builder.Services.AddScoped<IPoliticaAnaliseRepository, PoliticaAnaliseRepository>();
builder.Services.AddScoped<IAnaliseAutomaticaService, AnaliseAutomaticaService>();
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
