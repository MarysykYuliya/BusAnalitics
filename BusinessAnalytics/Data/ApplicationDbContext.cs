using BusinessAnalytics.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BusinessAnalytics.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // 🟢 DbSet-и
        public DbSet<BusinessAccount> BusinessAccounts { get; set; } = null!;
        public DbSet<ProductCategory> ProductCategories { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Transaction> Transactions { get; set; } = null!;
        public DbSet<ExpenseCategory> ExpenseCategories { get; set; } = null!;
        public DbSet<Premises> Premises { get; set; } = null!;
        public DbSet<UtilityType> UtilityTypes { get; set; } = null!;
        public DbSet<UtilityRecord> UtilityRecords { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---------- USER -> BUSINESS ----------
            modelBuilder.Entity<BusinessAccount>()
                .HasOne(b => b.Owner)
                .WithMany(u => u.BusinessAccounts)
                .HasForeignKey(b => b.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            // ---------- BUSINESS -> PRODUCT ----------
            modelBuilder.Entity<Product>()
                .HasOne(p => p.BusinessAccount)
                .WithMany(b => b.Products)
                .HasForeignKey(p => p.BusinessAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- BUSINESS -> PRODUCT CATEGORY ----------
            modelBuilder.Entity<ProductCategory>()
                .HasOne(pc => pc.BusinessAccount)
                .WithMany(b => b.ProductCategories)
                .HasForeignKey(pc => pc.BusinessAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- PRODUCT -> CATEGORY ----------
            modelBuilder.Entity<Product>()
                .HasOne(p => p.ProductCategory)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.ProductCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            // ---------- BUSINESS -> EXPENSE CATEGORY ----------
            modelBuilder.Entity<ExpenseCategory>()
                .HasOne(ec => ec.BusinessAccount)
                .WithMany(b => b.ExpenseCategories)
                .HasForeignKey(ec => ec.BusinessAccountId)
                .OnDelete(DeleteBehavior.Restrict); // 🟡 змінили з Cascade

            // ---------- TRANSACTION ----------
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.BusinessAccount)
                .WithMany(b => b.Transactions)
                .HasForeignKey(t => t.BusinessAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Product)
                .WithMany()
                .HasForeignKey(t => t.ProductId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.ExpenseCategory)
                .WithMany(ec => ec.Transactions)
                .HasForeignKey(t => t.ExpenseCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            // ---------- Індекси ----------
            modelBuilder.Entity<Product>()
                .HasIndex(p => new { p.BusinessAccountId, p.Name })
                .IsUnique(false);

            modelBuilder.Entity<ProductCategory>()
                .HasIndex(c => new { c.BusinessAccountId, c.Name })
                .IsUnique(false);

            // ---------- Обмеження довжин ----------
            modelBuilder.Entity<BusinessAccount>()
                .Property(b => b.Name)
                .HasMaxLength(150);

            // ---------- BUSINESS -> PREMISES ----------
            modelBuilder.Entity<Premises>()
                .HasOne(p => p.BusinessAccount)
                .WithMany(b => b.Premises)
                .HasForeignKey(p => p.BusinessAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // ---------- BUSINESS -> UTILITY TYPE ----------
            modelBuilder.Entity<UtilityType>()
                .HasOne(u => u.BusinessAccount)
                .WithMany(b => b.UtilityTypes)
                .HasForeignKey(u => u.BusinessAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // ---------- UTILITY RECORD ----------
            modelBuilder.Entity<UtilityRecord>()
                .HasOne(r => r.Premises)
                .WithMany(p => p.UtilityRecords)
                .HasForeignKey(r => r.PremisesId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UtilityRecord>()
                .HasOne(r => r.UtilityType)
                .WithMany(u => u.UtilityRecords)
                .HasForeignKey(r => r.UtilityTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UtilityRecord>()
                .HasIndex(r => new { r.PremisesId, r.UtilityTypeId, r.Year, r.Month })
                .IsUnique();
        }
    }
}
