using System;

namespace ShopApi.Utils
{
    public static class IdentityUtil
    {
        public static string GenerateId()
        {
            string number = String.Format("{0:d9}{1}", DateTime.Now.Ticks, Math.Abs(Guid.NewGuid().GetHashCode()));
            return number.Substring(0, Math.Min(number.Length, 32));
        }
    }
}
