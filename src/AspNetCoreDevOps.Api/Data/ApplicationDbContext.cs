using Microsoft.EntityFrameworkCore;
using AspNetCoreDevOps.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace AspNetCoreDevOps.Api.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Person> People { get; set; }
    }
}