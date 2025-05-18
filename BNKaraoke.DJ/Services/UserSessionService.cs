using System;

namespace BNKaraoke.DJ.Services;

public class UserSessionService : IUserSessionService
{
    public string Token { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string[] Roles { get; private set; } = Array.Empty<string>();

    public void SetSession(string token, string firstName, string lastName, string phoneNumber, string[] roles)
    {
        Token = token;
        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
        Roles = roles;
    }

    public void ClearSession()
    {
        Token = string.Empty;
        FirstName = string.Empty;
        LastName = string.Empty;
        PhoneNumber = string.Empty;
        Roles = Array.Empty<string>();
    }
}