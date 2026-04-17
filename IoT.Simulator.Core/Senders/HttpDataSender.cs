using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IoT.Simulator.Core.Configuration;
using IoT.Simulator.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace IoT.Simulator.Core.Senders;

public class HttpDataSender : IDataSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SimulatorConfig _config;
    private readonly ILogger<HttpDataSender> _logger;

    // POPRAWKA: Wstrzykujemy bezpośrednio nasz Singleton (SimulatorConfig)
    public HttpDataSender(
        IHttpClientFactory httpClientFactory,
        SimulatorConfig config,
        ILogger<HttpDataSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config; // Przypisujemy bezpośrednio
        _logger = logger;
    }

    public string Protocol => "HTTP";

    public async Task SendAsync(string payload, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();

        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        // Ponieważ ta linijka wykonuje się w każdej iteracji pętli, 
        // automatycznie i błyskawicznie złapie nowy adres, jeśli zmienisz go przez Panel Sterowania!
        var url = $"{_config.TargetAddress.TrimEnd('/')}/{_config.TopicOrPath.TrimStart('/')}";

        var response = await client.PostAsync(url, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Błąd HTTP {StatusCode} podczas wysyłania na adres {Url}", response.StatusCode, url);
        }
    }
}