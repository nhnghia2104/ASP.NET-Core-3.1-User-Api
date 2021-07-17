using ShopApi.Entity;

namespace ShopApi.Models.Users
{
    public class UpdateUserRequest
    {

        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? ImageUrl { get; set; }
        public long? Birthday { get; set; }
        public Role? Role { get; set; }
    }
}
