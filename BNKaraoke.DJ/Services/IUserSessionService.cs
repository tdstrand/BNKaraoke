using System;
using System.Collections.Generic;

namespace BNKaraoke.DJ.Services;

public interface IUserSessionService
{
    bool IsAuthenticated { get; }
    string? Token { get; }
    string? FirstName { get; }
    string? LastName { get; }
    string? UserId { get; }
    string? PhoneNumber { get; }
    List<string>? Roles { get; }

    event EventHandler? SessionChanged;

    void SetSession(Models.LoginResult loginResult);
    void ClearSession();
}