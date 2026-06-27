using Calcifer.Cathedra.Logging;
using Calcifer.Cathedra.Modules;
using Calcifer.Cathedra.Persistence;
using Calcifer.Public;
using Calcifer.Sample.Api;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Console logs print the request correlation id inline ([cid:...]) on every line.
builder.Logging.AddCathedraConsole();

// --- Host-supplied services the kernel depends on ------------------------------------------
// Identity for audit stamping (anonymous until the Auth module exists).
builder.Services.AddScoped<ICurrentUser, AnonymousCurrentUser>();

// The shared DbContext, backed by the in-memory provider so the sample runs with no database.
builder.Services.AddDbContext<CathedraDbContext>(options =>
    options.UseInMemoryDatabase("Calcifer"));

// --- The core + all discovered modules -----------------------------------------------------
builder.AddCathedra(options =>
    options.AddModuleAssemblyContaining<PublicModule>());

// File-based logging: ILogWriter now writes to BOTH the console and a daily-rotated file under
// logs/cathedra/. Settings are read from the "Cathedra:FileLogging" section of appsettings.json.
// Called AFTER AddCathedra so it replaces the kernel's default console-only ILogWriter.
builder.Services.AddFileLogging(builder.Configuration);

var app = builder.Build();

// Wire the request pipeline (logging -> exception handler -> module endpoints) and run
// each module's async lifecycle.
await app.UseCathedraAsync();

app.Run();
