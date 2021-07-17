using ShopApi.Entity.Models;

namespace ShopApi.Models.Users
{
    public class AuthenticationProviderRequest
    {
        public string KeyProvided { get; set; }

        public ProviderType ProviderType { get; set; }
    }
}
