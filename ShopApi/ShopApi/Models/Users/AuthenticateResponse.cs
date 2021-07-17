using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShopApi.Models.Users
{
    public class AuthenticateResponse
    {
        //public string Id { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? Email { get; set; }
        public long? Birthday { get; set; }
        public string? Phone { get; set; }
        public string? ImageUrl { get; set; }
        public string Token { get; set; }
        public bool IsVerified { get; set; }
        [JsonIgnore] // refresh token is returned in http only cookie
        public string RefreshToken { get; set; }
    }   
}
