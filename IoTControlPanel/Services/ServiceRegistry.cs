using IoT.Simulator.Core.Models;
using System.Collections.Concurrent;

namespace IoTControlPanel.Services;

public class ServiceRegistry
{
    private readonly ConcurrentDictionary<string, ServiceRegistrationDto> _services = new();

    public void UpdateService(ServiceRegistrationDto dto)
    {
        _services.AddOrUpdate(dto.ServiceId, dto, (key, existing) => dto);
    }

    public IEnumerable<ServiceRegistrationDto> GetAllServices()
    {
        return _services.Values.OrderBy(s => s.ServiceId);
    }
}