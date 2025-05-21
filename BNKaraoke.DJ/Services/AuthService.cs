using BNKaraoke.DJ.Models;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace BNKaraoke.DJ.Services;

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthService> _logger;

    public AuthService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:7290")
        };
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<AuthService>();
    }

    public async Task<LoginResult> LoginAsync(string phoneNumber, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", new { UserName = phoneNumber, Password = password });
            Log.Information("[AUTH] Login response status: {StatusCode}", response.StatusCode);
            response.EnsureSuccessStatusCode();

            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Log.Information("[AUTH] Login response: Token={Token}, UserId={UserId}, FirstName={FirstName}, LastName={LastName}, PhoneNumber={PhoneNumber}, Roles={Roles}",
                loginResponse?.Token?.Substring(0, 10) ?? "null", loginResponse?.UserId, loginResponse?.FirstName,
                loginResponse?.LastName, loginResponse?.PhoneNumber,
                loginResponse?.Roles != null ? string.Join(",", loginResponse.Roles) : "null");

            if (loginResponse == null || string.IsNullOrEmpty(loginResponse.Token))
            {
                _logger.LogWarning("Login failed: No token received.");
                Log.Warning("[AUTH] Login failed: No token received for phoneNumber={PhoneNumber}", phoneNumber);
                throw new Exception("Login failed: Invalid response from server.");
            }

            var result = new LoginResult
            {
                Token = loginResponse.Token,
                UserId = loginResponse.UserId,
                FirstName = loginResponse.FirstName,
                LastName = loginResponse.LastName,
                PhoneNumber = loginResponse.PhoneNumber,
                Roles = loginResponse.Roles?.ToList()
            };

            _logger.LogInformation("Login successful for user: {PhoneNumber}", phoneNumber);
            Log.Information("[AUTH] Login successful: Token={Token}, FirstName={FirstName}, UserId={UserId}, PhoneNumber={PhoneNumber}, Roles={Roles}",
                result.Token?.Substring(0, 10) ?? "null", result.FirstName, result.UserId, result.PhoneNumber,
                result.Roles != null ? string.Join(",", result.Roles) : "null");

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Login failed due to network error.");
            Log.Error("[AUTH] Login failed due to network error for phoneNumber={PhoneNumber}: {Message}", phoneNumber, ex.Message);
            throw new Exception("Login failed: Unable to connect to the server.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed.");
            Log.Error("[AUTH] Login failed for phoneNumber={PhoneNumber}: {Message}", phoneNumber, ex.Message);
            throw new Exception("Login failed: An unexpected error occurred.", ex);
        }
    }
}

internal class LoginResponse
{
    public string? Token { get; set; }
    public string? UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string[]? Roles { get; set; }
}