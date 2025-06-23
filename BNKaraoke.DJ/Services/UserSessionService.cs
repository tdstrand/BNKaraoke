// File: Services/UserSessionService.cs
using System.Collections.Generic;
using BNKaraoke.DJ.Models;

namespace BNKaraoke.DJ.Services
{
    public class UserSessionService : IUserSessionService
    {
        public string Token { get; private set; } = string.Empty;
        public string UserId { get; private set; } = string.Empty;
        public string FirstName { get; private set; } = string.Empty;
        public string LastName { get; private set; } = string.Empty;
        public List<string> Roles { get; private set; } = new();

        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Token);

        public void SetSession(UserSession session)
        {
            Token = session.Token;
            UserId = session.UserId;
            FirstName = session.FirstName;
            LastName = session.LastName;
            Roles = session.Roles;
        }

        public void ClearSession()
        {
            Token = string.Empty;
            UserId = string.Empty;
            FirstName = string.Empty;
            LastName = string.Empty;
            Roles.Clear();
        }
    }

    public interface IUserSessionService
    {
        string Token { get; }
        string UserId { get; }
        string FirstName { get; }
        string LastName { get; }
        List<string> Roles { get; }
        bool IsAuthenticated { get; }
        void SetSession(UserSession session);
        void ClearSession();
    }

    public class UserSession
    {
        public string Token { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
    }
}
