using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;

namespace WileyWidget.Data
{
    public class DatabaseSeeder
    {
        private readonly AppDbContext _context;

        public DatabaseSeeder(AppDbContext context)
        {
            _context = context;
        }

        public async Task SeedAsync()
        {
            // Database seeding is now handled in AppDbContext.OnModelCreating
            // This method is kept for compatibility with existing code
            await _context.Database.EnsureCreatedAsync();
        }
    }
}
