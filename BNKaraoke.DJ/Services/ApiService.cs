using System.Net.Http;
using System.Threading.Tasks;

namespace BNKaraoke.DJ.Services;

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;

    public ApiService(string baseUrl)
    {
        _httpClient = new HttpClient { BaseAddress = new System.Uri(baseUrl) };
    }

    public async Task<string> GetDiagnosticAsync()
    {
        var response = await _httpClient.GetAsync("/api/diagnostic/test");
        return await response.Content.ReadAsStringAsync();
    }
}
