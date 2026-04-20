namespace IoT.Simulator.Core.Models;

public class ServiceRegistrationDto
{
    public string ServiceId { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string TargetAddress { get; set; } = string.Empty;
    public string TopicOrPath { get; set; } = string.Empty;
    public int IntervalMilliseconds { get; set; }
    public bool IsRunning { get; set; }
    public DateTime LastUpdated { get; set; }
}