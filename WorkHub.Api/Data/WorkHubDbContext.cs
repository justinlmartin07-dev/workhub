using Microsoft.EntityFrameworkCore;
using WorkHub.Api.Models;

namespace WorkHub.Api.Data;

public class WorkHubDbContext : DbContext
{
    public WorkHubDbContext(DbContextOptions<WorkHubDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerPhoto> CustomerPhotos => Set<CustomerPhoto>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobNote> JobNotes => Set<JobNote>();
    public DbSet<JobPhoto> JobPhotos => Set<JobPhoto>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<JobInventory> JobInventories => Set<JobInventory>();
    public DbSet<JobAdhocItem> JobAdhocItems => Set<JobAdhocItem>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<CalendarEventAssignment> CalendarEventAssignments => Set<CalendarEventAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Users
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(200).IsRequired();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(200).IsRequired();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(x => x.ProfilePhotoR2Key).HasColumnName("profile_photo_r2_key").HasMaxLength(500);
            e.Property(x => x.FailedLoginAttempts).HasColumnName("failed_login_attempts").HasDefaultValue(0);
            e.Property(x => x.LockedUntil).HasColumnName("locked_until");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("idx_users_email");
        });

        // Refresh Tokens
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(200).IsRequired();
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.UserId).HasDatabaseName("idx_refresh_tokens_user_id");
            e.HasIndex(x => x.ExpiresAt).HasDatabaseName("idx_refresh_tokens_expires_at");
        });

        // Customers
        modelBuilder.Entity<Customer>(e =>
        {
            e.ToTable("customers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(50);
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(200);
            e.Property(x => x.Address).HasColumnName("address");
            e.Property(x => x.NormalizedAddress).HasColumnName("normalized_address").HasMaxLength(500);
            e.Property(x => x.Notes).HasColumnName("notes");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.DeletedAt).HasFilter("deleted_at IS NULL").HasDatabaseName("idx_customers_deleted_at");
            e.HasIndex(x => x.NormalizedAddress).HasDatabaseName("idx_customers_normalized_address");
        });

        // Customer Photos
        modelBuilder.Entity<CustomerPhoto>(e =>
        {
            e.ToTable("customer_photos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.R2ObjectKey).HasColumnName("r2_object_key").HasMaxLength(500).IsRequired();
            e.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
            e.Property(x => x.AddressTag).HasColumnName("address_tag").HasMaxLength(500);
            e.Property(x => x.UploadedAt).HasColumnName("uploaded_at");
            e.HasOne(x => x.Customer).WithMany(c => c.Photos).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.CustomerId, x.UploadedAt }).IsDescending(false, true).HasDatabaseName("idx_customer_photos_uploaded_at");
            e.HasIndex(x => x.AddressTag).HasDatabaseName("idx_customer_photos_address_tag");
        });

        // Jobs
        modelBuilder.Entity<Job>(e =>
        {
            e.ToTable("jobs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("Pending");
            e.Property(x => x.Priority).HasColumnName("priority").HasMaxLength(50).HasDefaultValue("Normal");
            e.Property(x => x.ScopeNotes).HasColumnName("scope_notes");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            e.HasOne(x => x.Customer).WithMany(c => c.Jobs).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.DeletedAt).HasFilter("deleted_at IS NULL").HasDatabaseName("idx_jobs_deleted_at");
            e.HasIndex(x => x.CustomerId).HasDatabaseName("idx_jobs_customer_id");
            e.HasIndex(x => x.Status).HasDatabaseName("idx_jobs_status");
        });

        // Job Notes
        modelBuilder.Entity<JobNote>(e =>
        {
            e.ToTable("job_notes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.JobId).HasColumnName("job_id");
            e.Property(x => x.Content).HasColumnName("content").IsRequired();
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(x => x.Job).WithMany(j => j.Notes).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.JobId).HasDatabaseName("idx_job_notes_job_id");
        });

        // Job Photos
        modelBuilder.Entity<JobPhoto>(e =>
        {
            e.ToTable("job_photos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.JobId).HasColumnName("job_id");
            e.Property(x => x.R2ObjectKey).HasColumnName("r2_object_key").HasMaxLength(500).IsRequired();
            e.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
            e.Property(x => x.AddressTag).HasColumnName("address_tag").HasMaxLength(500);
            e.Property(x => x.UploadedAt).HasColumnName("uploaded_at");
            e.HasOne(x => x.Job).WithMany(j => j.Photos).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.JobId, x.UploadedAt }).IsDescending(false, true).HasDatabaseName("idx_job_photos_uploaded_at");
            e.HasIndex(x => x.AddressTag).HasDatabaseName("idx_job_photos_address_tag");
        });

        // Inventory Items
        modelBuilder.Entity<InventoryItem>(e =>
        {
            e.ToTable("inventory_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.PartNumber).HasColumnName("part_number").HasMaxLength(100);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // Job Inventory (library-linked items)
        modelBuilder.Entity<JobInventory>(e =>
        {
            e.ToTable("job_inventory");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.JobId).HasColumnName("job_id");
            e.Property(x => x.InventoryItemId).HasColumnName("inventory_item_id");
            e.Property(x => x.Quantity).HasColumnName("quantity").HasDefaultValue(1);
            e.Property(x => x.ListType).HasColumnName("list_type").HasMaxLength(20).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(x => x.Job).WithMany(j => j.InventoryItems).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.InventoryItem).WithMany(i => i.JobInventories).HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.JobId).HasDatabaseName("idx_job_inventory_job_id");
            e.HasIndex(x => new { x.JobId, x.ListType }).HasDatabaseName("idx_job_inventory_list_type");
        });

        // Job Ad-Hoc Items
        modelBuilder.Entity<JobAdhocItem>(e =>
        {
            e.ToTable("job_adhoc_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.JobId).HasColumnName("job_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.Quantity).HasColumnName("quantity").HasDefaultValue(1);
            e.Property(x => x.ListType).HasColumnName("list_type").HasMaxLength(20).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(x => x.Job).WithMany(j => j.AdhocItems).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.JobId).HasDatabaseName("idx_job_adhoc_items_job_id");
            e.HasIndex(x => new { x.JobId, x.ListType }).HasDatabaseName("idx_job_adhoc_items_list_type");
        });

        // Calendar Events
        modelBuilder.Entity<CalendarEvent>(e =>
        {
            e.ToTable("calendar_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.StartTime).HasColumnName("start_time");
            e.Property(x => x.EndTime).HasColumnName("end_time");
            e.Property(x => x.ReminderMinutes).HasColumnName("reminder_minutes");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.JobId).HasColumnName("job_id");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Job).WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.StartTime).HasDatabaseName("idx_calendar_events_start_time");
        });

        // Calendar Event Assignments
        modelBuilder.Entity<CalendarEventAssignment>(e =>
        {
            e.ToTable("calendar_event_assignments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CalendarEventId).HasColumnName("calendar_event_id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.CalendarEvent).WithMany(ce => ce.Assignments).HasForeignKey(x => x.CalendarEventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.CalendarEventId).HasDatabaseName("idx_calendar_event_assignments_event_id");
            e.HasIndex(x => x.UserId).HasDatabaseName("idx_calendar_event_assignments_user_id");
        });
    }
}
