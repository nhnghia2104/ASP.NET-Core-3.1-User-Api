using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShopApi.Models.Users
{
    public class ThirdPartyAuthenticateResponse
    {
        public string Id { get; set; }
        public string Token { get; set; }
    }
}
