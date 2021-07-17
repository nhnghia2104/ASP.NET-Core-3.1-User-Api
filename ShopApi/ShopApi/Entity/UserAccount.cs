using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace ShopApi.Entity
{
    [Table("UserAccount")]
    public class UserAccount
    {
        [Key]
        [StringLength(34)]
        public string Id { get; set; }

        [ForeignKey("User")]
        [StringLength(32)]
        public string UserId { get; set; }

        [Required]
        [StringLength(255)]
        public string Username { get; set; }

        [Required]
        [StringLength(255)]
        public string PasswordHash { get; set; }

        [Required]
        [StringLength(255)]
        public string PasswordSalt { get; set; }

        public DateTimeOffset Created { get; set; }

        // Navigation props
        public virtual User User { get; set; }
    }
}
