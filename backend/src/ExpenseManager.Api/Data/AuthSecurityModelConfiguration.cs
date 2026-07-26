using ExpenseManager.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Data;

public static class AuthSecurityModelConfiguration
{
    /// <summary>
    /// Called from AppDbContext.OnModelCreating to register auth-security
    /// entities kept in their own source file.
    /// </summary>
    public static void ConfigureAuthSecurity(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(x => x.Id).HasName("pk_refresh_tokens");
            entity.HasIndex(x => x.TokenHash)
                .IsUnique()
                .HasDatabaseName("ix_refresh_tokens_token_hash");
            entity.HasIndex(x => new { x.UserId, x.ExpiresAt })
                .HasDatabaseName("ix_refresh_tokens_user_id_expires_at");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            entity.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            entity.Property(x => x.RevokedReason).HasColumnName("revoked_reason").HasMaxLength(100);
            entity.Property(x => x.ReplacedByTokenId).HasColumnName("replaced_by_token_id");
            entity.Property(x => x.CreatedByIp).HasColumnName("created_by_ip").HasMaxLength(64);
            entity.Property(x => x.RevokedByIp).HasColumnName("revoked_by_ip").HasMaxLength(64);
            entity.Property(x => x.ConcurrencyStamp)
                .HasColumnName("concurrency_stamp")
                .IsConcurrencyToken();
            entity.HasOne(x => x.User)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_refresh_tokens_users_user_id");
            entity.HasOne(x => x.ReplacedByToken)
                .WithMany()
                .HasForeignKey(x => x.ReplacedByTokenId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_refresh_tokens_refresh_tokens_replaced_by_token_id");
        });

        modelBuilder.Entity<AccountVerificationCode>(entity =>
        {
            entity.ToTable("account_verification_codes");
            entity.HasKey(x => x.Id).HasName("pk_account_verification_codes");
            entity.HasIndex(x => new { x.UserId, x.Purpose, x.CreatedAt })
                .HasDatabaseName("ix_account_verification_codes_user_id_purpose_created_at");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Purpose)
                .HasColumnName("purpose")
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(x => x.CodeHash).HasColumnName("code_hash").HasMaxLength(64);
            entity.Property(x => x.PendingEmail).HasColumnName("pending_email").HasMaxLength(320);
            entity.Property(x => x.FailedAttempts).HasColumnName("failed_attempts");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            entity.Property(x => x.UsedAt).HasColumnName("used_at");
            entity.Property(x => x.ConcurrencyStamp)
                .HasColumnName("concurrency_stamp")
                .IsConcurrencyToken();
            entity.HasOne(x => x.User)
                .WithMany(x => x.VerificationCodes)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_account_verification_codes_users_user_id");
        });
    }
}
