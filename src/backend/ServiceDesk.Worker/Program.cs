using ServiceDesk.Application;
using ServiceDesk.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// --- Layer registration (same as API, minus HTTP-specific services) ---
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var host = builder.Build();
host.Run();
