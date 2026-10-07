using Microsoft.EntityFrameworkCore;
using PetAdoption.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDb>((sp, options) =>
    options.UseSqlite($"Data Source={DbPaths.File(sp.GetRequiredService<IConfiguration>())};Foreign Keys=True"));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection("Admin"));
builder.Services.AddSingleton<AdminSessions>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

Directory.CreateDirectory(Path.GetDirectoryName(DbPaths.File(app.Configuration))!);
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDb>().Database.EnsureCreated();
}

app.UseCors();
app.UseAppToken();
app.MapApi();

app.Run();

public partial class Program { }
