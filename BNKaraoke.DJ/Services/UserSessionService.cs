using System;
using Serilog;
using BNKaraoke.DJ.Models;

namespace BNKaraoke.DJ.Services
{
    public class UserSessionService : IUserSessionService
    {
        private static UserSessionService _instance = new UserSessionService();
        public static UserSessionService Instance => _instance;

        public event EventHandler? SessionChanged;

        public bool IsAuthenticated { get; private set; }
        public string? Token { get; private set; }
        public string? FirstName { get; private set; }
        public string? PhoneNumber { get; private set; }

        private UserSessionService()
        {
            Log.Information("[SESSION] Singleton instance created: {InstanceId}", GetHashCode());
        }

        public void SetSession(LoginResult loginResult)
        {
            try
            {
                Log.Information("[SESSION] Setting session: Token={Token}, FirstName={FirstName}, PhoneNumber={PhoneNumber}",
                    loginResult?.Token, loginResult?.FirstName, loginResult?.PhoneNumber);
                Token = loginResult?.Token;
                FirstName = loginResult?.FirstName;
                PhoneNumber = loginResult?.PhoneNumber;
                IsAuthenticated = !string.IsNullOrEmpty(Token);
                SessionChanged?.Invoke(this, EventArgs.Empty);
                Log.Information("[SESSION] Session set: IsAuthenticated={IsAuthenticated}", IsAuthenticated);
            }
            catch (Exception ex)
            {
                Log.Error("[SESSION] Failed to set session: {Message}", ex.Message);
            }
        }

        public void ClearSession()
        {
            try
            {
                Log.Information("[SESSION] Clearing session");
                Token = null;
                FirstName = null;
                PhoneNumber = null;
                IsAuthenticated = false;
                SessionChanged?.Invoke(this, EventArgs.Empty);
                Log.Information("[SESSION] Session cleared: IsAuthenticated={IsAuthenticated}", IsAuthenticated);
            }
            catch (Exception ex)
            {
                Log.Error("[SESSION] Failed to clear session: {Message}", ex.Message);
            }
        }
    }
}