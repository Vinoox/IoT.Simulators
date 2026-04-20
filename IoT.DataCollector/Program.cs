using IoT.Simulator.Core.Configuration;
using IoT.Simulator.Core.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();

builder.Services.AddSingleton(new SimulatorConfig
{
    Protocol = "HTTP Receiver",
    TargetAddress = "localhost:5088",
    TopicOrPath = "/api/collect/{sector}",
    IsRunning = true,
    ProcessedMessages = 0
});

builder.Services.AddSingleton<RegistryClient>();

var app = builder.Build();

app.Lifetime.ApplicationStarted.Register(() =>
{
    var registryClient = app.Services.GetRequiredService<RegistryClient>();
    var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

    _ = Task.Run(async () =>
    {
        await registryClient.PushStateAsync();

        while (await timer.WaitForNextTickAsync())
        {
            await registryClient.PushStateAsync();
        }
    });
});

int _totalPackets = 0;

app.MapPost("/api/collect/{sector}", (string sector, object data, SimulatorConfig config, ILogger<Program> logger) => {

    Interlocked.Increment(ref _totalPackets);
    config.ProcessedMessages = _totalPackets;

    if (_totalPackets % 10 == 0)
    {
        logger.LogInformation(">>> [DATA COLLECTOR] Odebrano ³¹cznie pakietów: {Count}", _totalPackets);
    }

    return Results.Accepted();
});

app.Run();