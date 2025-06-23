// File: Models/UserInfo.cs
using System.Collections.Generic;

namespace BNKaraoke.DJ.Models
{
    public class UserInfo
    {
        public string UserName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
    }
}
