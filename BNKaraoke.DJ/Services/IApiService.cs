using System.Threading.Tasks;

namespace BNKaraoke.DJ.Services;

public interface IApiService
{
    Task<string> GetDiagnosticAsync();
}
