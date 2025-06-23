// File: DependencyLocator.cs
using System;
using System.Net.Http;
using BNKaraoke.DJ.Models;

namespace BNKaraoke.DJ.Services
{
    public static class DependencyLocator
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:7290") // <-- Dev API base
        };

        public static readonly IApiServices ApiService = new ApiService(_httpClient);
        public static readonly IUserSessionService UserSessionService = new UserSessionService();
    }
}
