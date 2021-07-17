using ShopApi.Entity.Models;

namespace ShopApi.Models.Users
{
    public class ThirdPartyAuthenticateRequest
    {
        public string KeyProvided { get; set; }

        public ProviderType ProviderType { get; set; }
    }
}
