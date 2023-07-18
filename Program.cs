using GeotecnologiaKNS.Data;
using GeotecnologiaKNS.Repositories.Interfaces;
using GeotecnologiaKNS.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using GeotecnologiaKNS.Models;

var builder = WebApplication.CreateBuilder(args);

// Configurar a conexão com o banco de dados
builder.Configuration.AddJsonFile("appsettings.json");

// Adicionar serviços ao contêiner
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("GeotecnologiaKNS")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddScoped<IProdutorRepository, ProdutorRepository>();
builder.Services.AddScoped<IPropriedadeRepository, PropriedadeRepository>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configurar o pipeline de solicitação HTTP
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

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


