using Microsoft.EntityFrameworkCore;

class User
{
    public int Id { get; set; }
    public List<int> Deck { get; set; }
    public string Password { get; set; }
    public string Username { get; set; }
}

class DataContext : DbContext
{
    public DataContext(DbContextOptions<DataContext> options) : base(options)
    {

    }

    public DbSet<User> Users => Set<User>();
}
