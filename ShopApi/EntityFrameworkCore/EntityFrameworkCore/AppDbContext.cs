using Microsoft.EntityFrameworkCore;
using ShopApi.Models.User;
using System;
using System.Collections.Generic;
using System.Text;

namespace EntityFrameworkCore.EntityFrameworkCore
{
    class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<AuthenticationProvider> AuthenticationProviders { get; set; }
        public DbSet<UserAccount> UserAccounts { get; set; }

        private string _connectionString = "";

        public AppDbContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseSqlServer(_connectionString);
        }
    }
}
