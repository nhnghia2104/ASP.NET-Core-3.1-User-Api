using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShopApi.Entity;
using ShopApi.Entity.Models;
using System;

namespace ShopApi.Helpers
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<AuthenticationProvider> AuthenticationProviders { get; set; }
        public DbSet<UserAccount> UserAccounts { get; set; }

        public AppDbContext(IServiceProvider services, DbContextOptions dbOptions)
        {
        }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();
            optionsBuilder.UseSqlServer(configuration.GetConnectionString("Default"));
        }
            
    }
}
