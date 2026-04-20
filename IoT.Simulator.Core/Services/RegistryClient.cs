using IoT.Simulator.Core.Configuration;
using IoT.Simulator.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace IoT.Simulator.Core.Services;

public class RegistryClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SimulatorConfig _config;
    private readonly ILogger<RegistryClient> _logger;
    private readonly string _controlPanelUrl;
    private readonly string _serviceId;
    private readonly string _myBaseUrl;

    public RegistryClient(
        IHttpClientFactory httpClientFactory,
        SimulatorConfig config,
        IConfiguration configuration,
        ILogger<RegistryClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;

        // Zmienne z appsettings.json każdego symulatora
        _controlPanelUrl = configuration["ControlPanelUrl"] ?? "http://localhost:5000";
        _serviceId = configuration["ServiceId"] ?? "Unknown";
        _myBaseUrl = configuration["ASPNETCORE_URLS"]?.Split(';').FirstOrDefault() ?? "http://localhost";
    }

    public async Task PushStateAsync()
    {
        try
        {
            var payload = new ServiceRegistrationDto
            {
                ServiceId = _serviceId,
                BaseUrl = _myBaseUrl,
                Protocol = _config.Protocol,
                TargetAddress = _config.TargetAddress,
                TopicOrPath = _config.TopicOrPath,
                IntervalMilliseconds = _config.IntervalMilliseconds,
                IsRunning = _config.IsRunning,
                LastUpdated = DateTime.UtcNow
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3); // Fail-fast

            var response = await client.PostAsJsonAsync($"{_controlPanelUrl}/api/registry/push", payload);
            response.EnsureSuccessStatusCode();

            _logger.LogDebug("Stan zarejestrowany w Control Panel.");
        }
        catch (Exception ex)
        {
            // Nie rzucamy błędu dalej, aby nie wywrócić symulatora
            _logger.LogWarning("Nie udało się zaktualizować stanu w Control Panel: {Message}", ex.Message);
        }
    }
}