using System.Threading;
using System.Threading.Tasks;

namespace IoT.Simulator.Core.Interfaces;

public interface IDataSender
{
    string Protocol { get; } 
    
    Task SendAsync(string payload, CancellationToken cancellationToken);
}