#nullable enable
namespace BNKaraoke.DJ.Services;

public interface IUserSessionService
{
    string? Token { get; }
    string? FirstName { get; }
    string? LastName { get; }
    string? PhoneNumber { get; }
    string[] Roles { get; }

    void SetSession(string token, string firstName, string lastName, string phoneNumber, string[] roles);
    void ClearSession();
}
