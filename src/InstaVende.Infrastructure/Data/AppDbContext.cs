using InstaVende.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<BotConfig> BotConfigs => Set<BotConfig>();
    public DbSet<BotIntent> BotIntents => Set<BotIntent>();
    public DbSet<BotKnowledge> BotKnowledges => Set<BotKnowledge>();
    public DbSet<ConversationFlow> ConversationFlows => Set<ConversationFlow>();
    public DbSet<ChannelConfig> ChannelConfigs => Set<ChannelConfig>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Business>(e =>
        {
            e.HasOne(b => b.User).WithOne(u => u.Business)
                .HasForeignKey<Business>(b => b.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(b => b.Name).HasMaxLength(200).IsRequired();
        });

        builder.Entity<Product>(e =>
        {
            e.Property(p => p.Price).HasColumnType("decimal(18,2)");
            e.Property(p => p.Name).HasMaxLength(300).IsRequired();
        });

        builder.Entity<BotConfig>(e =>
        {
            e.HasOne(b => b.Business).WithOne(biz => biz.BotConfig)
                .HasForeignKey<BotConfig>(b => b.BusinessId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Contact>(e =>
        {
            e.HasIndex(c => new { c.BusinessId, c.ChannelType, c.ExternalId }).IsUnique();
        });

        builder.Entity<Conversation>(e =>
        {
            e.HasOne(c => c.AssignedAgent).WithMany()
                .HasForeignKey(c => c.AssignedAgentId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ChannelConfig>(e =>
        {
            e.HasIndex(c => new { c.BusinessId, c.ChannelType }).IsUnique();
        });
    }
}
