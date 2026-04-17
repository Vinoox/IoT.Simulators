using System.Threading;
using System.Threading.Tasks;

namespace IoT.Simulator.Core.Interfaces;

public interface IDataProvider
{
    Task<string> GetNextPayloadAsync(CancellationToken cancellationToken);
}