using BNKaraoke.DJ.Models;
using Serilog;
using System;
using System.Collections.Generic;

namespace BNKaraoke.DJ.Services;

public class UserSessionService : IUserSessionService
{
    private static readonly UserSessionService _instance = new();
    public static UserSessionService Instance => _instance;

    public event EventHandler? SessionChanged;

    private UserSessionService()
    {
        Log.Information("[SESSION] Singleton instance created: {InstanceId}", GetHashCode());
    }

    public string? Token { get; private set; }
    public string? UserId { get; private set; }
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? PhoneNumber { get; private set; }
    public List<string>? Roles { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public void SetSession(LoginResult loginResult)
    {
        if (loginResult == null || string.IsNullOrEmpty(loginResult.Token))
        {
            Log.Error("[SESSION] SetSession called with invalid loginResult: {LoginResult}", loginResult);
            return;
        }

        Token = loginResult.Token;
        UserId = loginResult.UserId;
        FirstName = loginResult.FirstName;
        LastName = loginResult.LastName;
        PhoneNumber = loginResult.PhoneNumber;
        Roles = loginResult.Roles ?? new List<string>();

        Log.Information("[SESSION] Set session: Token={Token}, FirstName={FirstName}, UserId={UserId}, PhoneNumber={PhoneNumber}, Roles={Roles}, IsAuthenticated={IsAuthenticated}",
            Token?.Substring(0, 10) ?? "null", FirstName, UserId, PhoneNumber,
            Roles != null ? string.Join(",", Roles) : "null", IsAuthenticated);

        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSession()
    {
        Token = null;
        UserId = null;
        FirstName = null;
        LastName = null;
        PhoneNumber = null;
        Roles = null;
        Log.Information("[SESSION] Cleared session: IsAuthenticated={IsAuthenticated}", IsAuthenticated);
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}