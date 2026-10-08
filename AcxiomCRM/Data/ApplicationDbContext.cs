using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;

namespace AcxiomCRM.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Customer
        builder.Entity<Customer>(entity =>
        {
            entity.HasIndex(c => c.Email).IsUnique();
            entity.HasIndex(c => c.Phone).IsUnique();
            entity.HasIndex(c => c.OwnerId);
            entity.HasIndex(c => c.Status);
            entity.HasIndex(c => c.CreatedDate);

            entity.HasOne(c => c.Owner)
                  .WithMany(u => u.OwnedCustomers)
                  .HasForeignKey(c => c.OwnerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Lead
        builder.Entity<Lead>(entity =>
        {
            entity.HasIndex(l => l.Email);
            entity.HasIndex(l => l.Phone);
            entity.HasIndex(l => l.AssignedTo);
            entity.HasIndex(l => l.Status);
            entity.HasIndex(l => l.CreatedDate);

            entity.HasOne(l => l.AssignedUser)
                  .WithMany(u => u.AssignedLeads)
                  .HasForeignKey(l => l.AssignedTo)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.ConvertedCustomer)
                  .WithMany(c => c.ConvertedFromLeads)
                  .HasForeignKey(l => l.ConvertedCustomerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(l => l.ConvertedOpportunity)
                  .WithMany()
                  .HasForeignKey(l => l.ConvertedOpportunityId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Opportunity
        builder.Entity<Opportunity>(entity =>
        {
            entity.HasIndex(o => o.CustomerId);
            entity.HasIndex(o => o.OwnerId);
            entity.HasIndex(o => o.Stage);
            entity.HasIndex(o => o.Status);
            entity.HasIndex(o => o.ExpectedCloseDate);
            entity.HasIndex(o => o.CreatedDate);

            entity.HasOne(o => o.Customer)
                  .WithMany(c => c.Opportunities)
                  .HasForeignKey(o => o.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.Lead)
                  .WithMany()
                  .HasForeignKey(o => o.LeadId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.Owner)
                  .WithMany(u => u.OwnedOpportunities)
                  .HasForeignKey(o => o.OwnerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // FollowUp
        builder.Entity<FollowUp>(entity =>
        {
            entity.HasIndex(f => f.AssignedTo);
            entity.HasIndex(f => f.Status);
            entity.HasIndex(f => f.FollowUpDate);
            entity.HasIndex(f => f.CustomerId);
            entity.HasIndex(f => f.LeadId);
            entity.HasIndex(f => f.OpportunityId);

            entity.HasOne(f => f.AssignedUser)
                  .WithMany(u => u.AssignedFollowUps)
                  .HasForeignKey(f => f.AssignedTo)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(f => f.Customer)
                  .WithMany(c => c.FollowUps)
                  .HasForeignKey(f => f.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(f => f.Lead)
                  .WithMany(l => l.FollowUps)
                  .HasForeignKey(f => f.LeadId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(f => f.Opportunity)
                  .WithMany(o => o.FollowUps)
                  .HasForeignKey(f => f.OpportunityId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Activity
        builder.Entity<Activity>(entity =>
        {
            entity.HasIndex(a => a.AssignedTo);
            entity.HasIndex(a => a.ActivityType);
            entity.HasIndex(a => a.Status);
            entity.HasIndex(a => a.ActivityDate);
            entity.HasIndex(a => a.CustomerId);
            entity.HasIndex(a => a.LeadId);
            entity.HasIndex(a => a.OpportunityId);

            entity.HasOne(a => a.AssignedUser)
                  .WithMany(u => u.AssignedActivities)
                  .HasForeignKey(a => a.AssignedTo)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Customer)
                  .WithMany(c => c.Activities)
                  .HasForeignKey(a => a.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Lead)
                  .WithMany(l => l.Activities)
                  .HasForeignKey(a => a.LeadId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Opportunity)
                  .WithMany(o => o.Activities)
                  .HasForeignKey(a => a.OpportunityId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // AuditLog
        builder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(a => a.UserId);
            entity.HasIndex(a => a.Action);
            entity.HasIndex(a => a.Module);
            entity.HasIndex(a => a.EntityName);
            entity.HasIndex(a => a.Result);
            entity.HasIndex(a => a.CreatedDate);

            entity.HasOne(a => a.User)
                  .WithMany()
                  .HasForeignKey(a => a.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
