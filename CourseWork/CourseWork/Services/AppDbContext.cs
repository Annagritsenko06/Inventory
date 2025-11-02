using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CourseWork.Models;
namespace CourseWork.Services
{
    public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<Inventories> Inventories => Set<Inventories>();
        public DbSet<InventoryField> InventoryFields => Set<InventoryField>();
        public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
        public DbSet<InventoryTag> InventoryTags { get; set; }
        public DbSet<AccessInventory> AccessInventories { get; set; }
        public DbSet<ItemLike> ItemLikes { get; set; }
        public DbSet<InventoryDiscussion> InventoryDiscussions { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(b =>
            {
                b.ToTable("users");
            });
            modelBuilder.Entity<InventoryField>()
    .Property(f => f.Id)
    .UseIdentityAlwaysColumn(); // или UseSerialColumn() для PostgreSQL

            modelBuilder.Entity<InventoryItem>(b =>
            {
                b.HasIndex(i => new { i.InventoryId, i.CustomId }).IsUnique();
                b.ToTable("inventory_items");
            });
            modelBuilder.Entity<Inventories>().ToTable("inventories");
            modelBuilder.Entity<InventoryField>().ToTable("inventory_fields");
            modelBuilder.Entity<AccessInventory>()
       .HasOne(ai => ai.inventory_template)
       .WithMany(i => i.access_list)
       .HasForeignKey(ai => ai.inventory_template_id);

            modelBuilder.Entity<AccessInventory>(b =>
            {
                b.ToTable("access_inventory");
            });
            modelBuilder.Entity<Inventories>()
      .Property(b => b.CustomIdFormatJson)
      .HasColumnName("custom_id_format_json")
      .HasColumnType("jsonb");


        //    modelBuilder.Entity<Inventories>()
        //.HasMany(i => i.Tags)
        //.WithMany(t => t.Inventories)
        //.UsingEntity<Dictionary<string, object>>(
        //    "inventory_tag_links",
        //    j => j.HasOne<InventoryTag>().WithMany().HasForeignKey("tag_id"),
        //    j => j.HasOne<Inventories>().WithMany().HasForeignKey("inventory_id")
        //    );
            modelBuilder.Entity<Inventories>()
        .HasMany(i => i.Tags)
        .WithMany(t => t.Inventories)
        .UsingEntity(j => j.ToTable("inventory_tag_links"));

            //    // ItemLike configuration
            modelBuilder.Entity<ItemLike>(b =>
            {
                b.ToTable("item_likes");
                b.HasIndex(i => new { i.ItemId, i.UserId }).IsUnique();
            });

            // InventoryDiscussion configuration
            modelBuilder.Entity<InventoryDiscussion>(b =>
            {
                b.ToTable("inventory_discussions");
            });
            modelBuilder.Entity<Inventories>()
    .HasMany(i => i.AllowedUsers)
    .WithMany(u => u.AccessibleInventories)
    .UsingEntity(j => j.ToTable("InventoriesUser")); // имя таблицы

            modelBuilder.Entity<AccessInventory>()
    .HasOne(a => a.user)
    .WithMany()
    .HasForeignKey(a => a.user_id)
    .OnDelete(DeleteBehavior.Cascade);

        }
    }
}