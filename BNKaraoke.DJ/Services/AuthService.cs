#nullable enable
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using BNKaraoke.DJ.Models;

namespace BNKaraoke.DJ.Services;

public class AuthService : IAuthService
{
    private readonly HttpClient _client;

    public AuthService(string baseAddress)
    {
        _client = new HttpClient { BaseAddress = new Uri(baseAddress) };
    }

    public async Task<LoginResult?> LoginAsync(string phone, string password)
    {
        var payload = new { userName = phone, password };
        Console.WriteLine($"[LOGIN DEBUG] Sending login for: {phone}");
        var response = await _client.PostAsJsonAsync("api/Auth/login", payload);

        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            Console.WriteLine("[LOGIN DEBUG] Raw JSON Response:\n" + json);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            root.TryGetProperty("token", out var tokenProp);
            root.TryGetProperty("firstName", out var firstNameProp);
            root.TryGetProperty("lastName", out var lastNameProp);
            root.TryGetProperty("userName", out var userNameProp);

            string[] roles = Array.Empty<string>();
            if (root.TryGetProperty("roles", out var rolesProp) && rolesProp.ValueKind == JsonValueKind.Array)
            {
                roles = rolesProp.EnumerateArray()
                                 .Where(r => r.ValueKind == JsonValueKind.String)
                                 .Select(r => r.GetString() ?? "")
                                 .ToArray();
            }

            var token = tokenProp.ValueKind == JsonValueKind.String ? tokenProp.GetString() ?? "" : "";
            var firstName = firstNameProp.ValueKind == JsonValueKind.String ? firstNameProp.GetString() ?? "" : "";
            var lastName = lastNameProp.ValueKind == JsonValueKind.String ? lastNameProp.GetString() ?? "" : "";
            var phoneNumber = userNameProp.ValueKind == JsonValueKind.String ? userNameProp.GetString() ?? "" : "";

            Console.WriteLine("[LOGIN DEBUG] Parsed Token: " + token);
            Console.WriteLine("[LOGIN DEBUG] Parsed FirstName: " + firstName);
            Console.WriteLine("[LOGIN DEBUG] Parsed LastName: " + lastName);
            Console.WriteLine("[LOGIN DEBUG] Parsed PhoneNumber: " + phoneNumber);
            foreach (var role in roles)
                Console.WriteLine("[LOGIN DEBUG] Parsed Role: " + role);

            return new LoginResult
            {
                Token = token,
                FirstName = firstName,
                LastName = lastName,
                PhoneNumber = phoneNumber,
                Roles = roles
            };
        }

        Console.WriteLine("[LOGIN DEBUG] Login failed. Status code: " + response.StatusCode);
        return null;
    }
}
