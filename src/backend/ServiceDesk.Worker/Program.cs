using ServiceDesk.Worker;

// Khởi tạo và chạy background worker service
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWorkerServices(builder.Configuration);
var host = builder.Build();
host.Run();
