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
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<BotConfig> BotConfigs => Set<BotConfig>();
    public DbSet<BotIntent> BotIntents => Set<BotIntent>();
    public DbSet<BotKnowledge> BotKnowledges => Set<BotKnowledge>();
    public DbSet<ConversationFlow> ConversationFlows => Set<ConversationFlow>();
    public DbSet<ChannelConfig> ChannelConfigs => Set<ChannelConfig>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<OnboardingProgress> OnboardingProgresses => Set<OnboardingProgress>();
    public DbSet<BusinessUser> BusinessUsers => Set<BusinessUser>();
    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();
    public DbSet<NotificationEmail> NotificationEmails => Set<NotificationEmail>();
    public DbSet<ReminderTemplate> ReminderTemplates => Set<ReminderTemplate>();
    public DbSet<VendorConfig> VendorConfigs => Set<VendorConfig>();
    public DbSet<KnowledgeEntry> KnowledgeEntries => Set<KnowledgeEntry>();
    public DbSet<DeliveryZone> DeliveryZones => Set<DeliveryZone>();
    public DbSet<PaymentImage> PaymentImages => Set<PaymentImage>();
    public DbSet<ConversationLabel> ConversationLabels => Set<ConversationLabel>();

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
            e.Property(p => p.PriceOriginal).HasColumnType("decimal(18,2)");
            e.Property(p => p.Name).HasMaxLength(300).IsRequired();
        });

        builder.Entity<ProductVariant>(e =>
        {
            e.Property(v => v.PriceModifier).HasColumnType("decimal(18,2)");
        });

        builder.Entity<PaymentMethod>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(150).IsRequired();
            e.HasOne(p => p.Business).WithMany(b => b.PaymentMethods)
                .HasForeignKey(p => p.BusinessId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Order>(e =>
        {
            e.Property(o => o.Subtotal).HasColumnType("decimal(18,2)");
            e.Property(o => o.Discount).HasColumnType("decimal(18,2)");
            e.Property(o => o.ShippingCost).HasColumnType("decimal(18,2)");
            e.Property(o => o.Total).HasColumnType("decimal(18,2)");
            e.Property(o => o.OrderNumber).HasMaxLength(50).IsRequired();
            e.HasOne(o => o.Business).WithMany(b => b.Orders)
                .HasForeignKey(o => o.BusinessId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(o => o.Contact).WithMany(c => c.Orders)
                .HasForeignKey(o => o.ContactId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.Conversation).WithMany(c => c.Orders)
                .HasForeignKey(o => o.ConversationId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<OrderItem>(e =>
        {
            e.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
            e.Property(i => i.Subtotal).HasColumnType("decimal(18,2)");
            e.HasOne(i => i.Product).WithMany(p => p.OrderItems)
                .HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Reminder>(e =>
        {
            e.HasOne(r => r.Business).WithMany(b => b.Reminders)
                .HasForeignKey(r => r.BusinessId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(r => r.Contact).WithMany(c => c.Reminders)
                .HasForeignKey(r => r.ContactId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.CreatedByAgent).WithMany()
                .HasForeignKey(r => r.CreatedByAgentId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<OnboardingProgress>(e =>
        {
            e.HasOne(o => o.Business).WithOne(b => b.OnboardingProgress)
                .HasForeignKey<OnboardingProgress>(o => o.BusinessId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Contact>(e =>
        {
            e.Property(c => c.TotalSpent).HasColumnType("decimal(18,2)");
        });

        builder.Entity<BotConfig>(e =>
        {
            e.HasOne(b => b.Business).WithOne(biz => biz.BotConfig)
                .HasForeignKey<BotConfig>(b => b.BusinessId).OnDelete(DeleteBehavior.Cascade);

            e.Property(b => b.DiscountLevel1).HasColumnType("decimal(5,2)");
            e.Property(b => b.DiscountLevel2).HasColumnType("decimal(5,2)");
            e.Property(b => b.DiscountLevel3).HasColumnType("decimal(5,2)");
            e.Property(b => b.MaxDiscountPercent).HasColumnType("decimal(5,2)");
            e.Property(b => b.MinMarginPercent).HasColumnType("decimal(5,2)");
            e.Property(b => b.FreeShippingThreshold).HasColumnType("decimal(10,2)");
            e.Property(b => b.BundleDiscount).HasColumnType("decimal(5,2)");
            e.Property(b => b.LoyaltyDiscount).HasColumnType("decimal(5,2)");
        });

        builder.Entity<Contact>(e =>
        {
            e.HasIndex(c => new { c.BusinessId, c.ChannelType, c.ExternalId }).IsUnique();
            // Evita múltiples rutas de cascada hacia Conversations
            e.HasMany(c => c.Conversations).WithOne(cv => cv.Contact)
                .HasForeignKey(cv => cv.ContactId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Conversation>(e =>
        {
            e.HasOne(c => c.AssignedAgent).WithMany()
                .HasForeignKey(c => c.AssignedAgentId).OnDelete(DeleteBehavior.SetNull);
            // Business ? Conversation: NoAction para no crear ciclo con Business ? Contact ? Conversation
            e.HasOne(c => c.Business).WithMany(b => b.Conversations)
                .HasForeignKey(c => c.BusinessId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<ChannelConfig>(e =>
        {
            e.HasIndex(c => new { c.BusinessId, c.ChannelType }).IsUnique();
        });

        builder.Entity<VendorConfig>(e =>
        {
            e.HasOne(v => v.Business).WithOne(b => b.VendorConfig)
                .HasForeignKey<VendorConfig>(v => v.BusinessId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<KnowledgeEntry>(e =>
        {
            e.HasOne(k => k.Business).WithMany(b => b.KnowledgeEntries)
                .HasForeignKey(k => k.BusinessId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DeliveryZone>(e =>
        {
            e.Property(d => d.Cost).HasColumnType("decimal(18,2)");
            e.HasOne(d => d.Business).WithMany(b => b.DeliveryZones)
                .HasForeignKey(d => d.BusinessId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PaymentImage>(e =>
        {
            e.HasOne(p => p.Business).WithMany(b => b.PaymentImages)
                .HasForeignKey(p => p.BusinessId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BusinessUser>(e =>
        {
            e.HasOne(bu => bu.Business).WithMany(b => b.BusinessUsers)
                .HasForeignKey(bu => bu.BusinessId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(bu => bu.User).WithMany()
                .HasForeignKey(bu => bu.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserInvitation>(e =>
        {
            e.HasIndex(i => i.Token).IsUnique();
            e.HasOne(i => i.Business).WithMany(b => b.UserInvitations)
                .HasForeignKey(i => i.BusinessId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<NotificationEmail>(e =>
        {
            e.HasOne(n => n.Business).WithMany(b => b.NotificationEmails)
                .HasForeignKey(n => n.BusinessId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ReminderTemplate>(e =>
        {
            e.Property(r => r.Message).HasMaxLength(500);
            e.HasOne(r => r.Business).WithMany(b => b.ReminderTemplates)
                .HasForeignKey(r => r.BusinessId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ConversationLabel>(e =>
        {
            e.HasOne(cl => cl.Business).WithMany(b => b.ConversationLabels)
                .HasForeignKey(cl => cl.BusinessId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(cl => cl.Conversations).WithOne(c => c.Label)
                .HasForeignKey(c => c.LabelId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
