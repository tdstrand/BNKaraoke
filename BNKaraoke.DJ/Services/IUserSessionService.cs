using System;
using BNKaraoke.DJ.Models;

namespace BNKaraoke.DJ.Services
{
    public interface IUserSessionService
    {
        bool IsAuthenticated { get; }
        string? Token { get; }
        string? FirstName { get; }
        string? PhoneNumber { get; }
        event EventHandler SessionChanged;
        void SetSession(LoginResult loginResult);
        void ClearSession();
    }
}