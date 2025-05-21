using System;
using System.Collections.Generic;
using BNKaraoke.DJ.Models;
using Serilog;

namespace BNKaraoke.DJ.Services;

public class UserSessionService : IUserSessionService
{
    private static readonly Lazy<UserSessionService> _instance = new Lazy<UserSessionService>(() => new UserSessionService());
    public static UserSessionService Instance => _instance.Value;

    public bool IsAuthenticated { get; private set; }
    public string? Token { get; private set; }
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? UserId { get; private set; }
    public string? PhoneNumber { get; private set; }
    public List<string>? Roles { get; private set; }

    public event EventHandler? SessionChanged;

    private UserSessionService()
    {
        Log.Information("[SESSION] Singleton instance created: {InstanceId}", GetHashCode());
    }

    public void SetSession(LoginResult loginResult)
    {
        IsAuthenticated = !string.IsNullOrEmpty(loginResult.Token);
        Token = loginResult.Token;
        FirstName = loginResult.FirstName;
        LastName = loginResult.LastName;
        UserId = loginResult.UserId;
        PhoneNumber = loginResult.PhoneNumber;
        Roles = loginResult.Roles;

        Log.Information("[SESSION] Set session: Token={Token}, FirstName={FirstName}, LastName={LastName}, UserId={UserId}, PhoneNumber={PhoneNumber}, Roles={Roles}, IsAuthenticated={IsAuthenticated}",
            Token?.Substring(0, 10) ?? "null", FirstName, LastName, UserId, PhoneNumber,
            Roles != null ? string.Join(",", Roles) : "null", IsAuthenticated);

        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSession()
    {
        IsAuthenticated = false;
        Token = null;
        FirstName = null;
        LastName = null;
        UserId = null;
        PhoneNumber = null;
        Roles = null;

        Log.Information("[SESSION] Cleared session");
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}