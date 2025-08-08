using Microsoft.EntityFrameworkCore;

public class BookDb : DbContext
{
    public BookDb(DbContextOptions<BookDb> options)
        : base(options) { }

    public DbSet<Book> Books => Set<Book>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Book>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
        });
    }
}


