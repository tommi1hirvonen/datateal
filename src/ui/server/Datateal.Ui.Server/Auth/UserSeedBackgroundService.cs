using Datateal.Auth;
using Datateal.Core.Users;
using Datateal.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Datateal.Ui.Server.Auth;

/// <summary>
/// Background service that seeds configuration-defined users (local dev user and admin seed list)
/// to the database on application startup if they do not already exist.
/// </summary>
public class UserSeedBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<AdminUsersOptions> adminOptions,
    IConfiguration configuration,
    ILogger<UserSeedBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting user seeding background service.");

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DatatealDbContext>();

            // Ensure we can connect to the database (migrations will have already run in Program.cs)
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                if (!await dbContext.Database.CanConnectAsync(stoppingToken))
                {
                    throw new InvalidOperationException("Database is not accessible for seeding users.");
                }
            });

            var usersToSeed = new List<(string Email, string DisplayName, List<string> Roles)>();

            // 1. Check for Dev User if Dev authentication provider is enabled
            var authProvider = configuration["Authentication:Provider"] ?? "EntraId";
            if (authProvider.Equals("Dev", StringComparison.OrdinalIgnoreCase))
            {
                var devSection = configuration.GetSection("Authentication:Dev");
                var email = devSection.GetValue<string>("User:Email") ?? "dev@local";
                var displayName = devSection.GetValue<string>("User:DisplayName") ?? "Local Dev User";
                var roles = devSection.GetSection("Roles").Get<List<string>>();

                // If roles are null or empty, default to Admin
                if (roles is null || roles.Count == 0)
                {
                    roles = [DatatealRole.Admin];
                }

                usersToSeed.Add((email, displayName, roles));
            }

            // 2. Add users from the Admin seed list (always parsed from Authorization:AdminUsers)
            var adminEmails = adminOptions.Value.AdminUsers;
            if (adminEmails is not null)
            {
                foreach (var email in adminEmails)
                {
                    if (string.IsNullOrWhiteSpace(email)) continue;

                    var trimmedEmail = email.Trim();
                    var displayName = GetFriendlyNameFromEmail(trimmedEmail);
                    usersToSeed.Add((trimmedEmail, displayName, [DatatealRole.Admin]));
                }
            }

            if (usersToSeed.Count > 0)
            {
                // Fetch existing emails to avoid duplicates
                var existingEmails = await dbContext.AppUsers
                    .Select(u => u.Email)
                    .ToListAsync(stoppingToken);

                var existingEmailSet = existingEmails.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var addedCount = 0;

                foreach (var userDef in usersToSeed)
                {
                    if (existingEmailSet.Contains(userDef.Email))
                    {
                        logger.LogDebug("User '{Email}' already exists in database. Skipping seed.", userDef.Email);
                        continue;
                    }

                    var isUserAdmin = userDef.Roles.Contains(DatatealRole.Admin);
                    var newUser = new UserAccount
                    {
                        Id = Guid.CreateVersion7(),
                        Email = userDef.Email,
                        DisplayName = userDef.DisplayName,
                        Roles = userDef.Roles,
                        HasAllCatalogAccess = isUserAdmin,
                        IsEnabled = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    dbContext.AppUsers.Add(newUser);
                    addedCount++;
                    logger.LogInformation("Seeding user '{Email}' with display name '{DisplayName}' and roles: [{Roles}].",
                        userDef.Email, userDef.DisplayName, string.Join(", ", userDef.Roles));
                }

                if (addedCount > 0)
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Successfully seeded {Count} users to the database.", addedCount);
                }
                else
                {
                    logger.LogInformation("No new users to seed. All configured users already exist in the database.");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding configuration-defined users to the database.");
        }

        logger.LogInformation("User seeding background service completed.");
    }

    private static string GetFriendlyNameFromEmail(string email)
    {
        var part = email.Split('@')[0];
        var words = part.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return "Admin User";
        return string.Join(" ", words.Select(w => char.ToUpper(w[0]) + w[1..]));
    }
}
