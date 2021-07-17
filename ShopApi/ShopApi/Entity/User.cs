using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace ShopApi.Entity
{
    [Table("User")]
    public class User
    {
        [Key]
        [StringLength(32)]
        public string Id { get; set; }

        [StringLength(255)]
        public string? Firstname { get; set; }

        [StringLength(255)]
        public string? Lastname { get; set; }


        [NotMapped]
        public string Fullname { 
            get { return Firstname + " " + Lastname; }
        }

        [StringLength(255)]
        public string? Email { get; set; }

        [StringLength(20)]
        public string? Phone { get; set; }
        [StringLength(255)]
        public string? ImageUrl { get; set; }

        public long? Birthday { get; set; }

        public DateTimeOffset Created { get; set; }
        public DateTimeOffset? Updated { get; set; }
        public Role Role { get; set; }
        public string VerificationToken { get; set; }
        public DateTimeOffset? Verified { get; set; }
        public bool IsVerified => Verified.HasValue;
        public string ResetToken { get; set; }
        public DateTime? ResetTokenExpires { get; set; }
        public DateTime? PasswordReset { get; set; }
        public List<RefreshToken> RefreshTokens { get; set; }
        public bool OwnsToken(string token)
        {
            return this.RefreshTokens?.Find(x => x.Token == token) != null;
        }

        // Navigation props

    }
}
