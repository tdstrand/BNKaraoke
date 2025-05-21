using BNKaraoke.DJ.Models;
using System;
using System.Collections.Generic;

namespace BNKaraoke.DJ.Services;

public interface IUserSessionService
{
    string? Token { get; }
    string? UserId { get; }
    string? FirstName { get; }
    string? LastName { get; }
    string? PhoneNumber { get; }
    List<string>? Roles { get; }
    bool IsAuthenticated { get; }
    event EventHandler? SessionChanged;
    void SetSession(LoginResult loginResult);
    void ClearSession();
}