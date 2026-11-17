using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Users;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(refreshToken => refreshToken.Id);
        builder.Property(refreshToken => refreshToken.Id).ValueGeneratedNever();

        builder.Property(refreshToken => refreshToken.TokenHash)
            .HasMaxLength(RefreshToken.TokenHashMaxLength)
            .IsRequired();

        builder.HasIndex(refreshToken => refreshToken.TokenHash).IsUnique();

        builder.HasOne<User>().WithMany().HasForeignKey(refreshToken => refreshToken.UserId);
    }
}
