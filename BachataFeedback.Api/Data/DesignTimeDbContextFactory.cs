using BachataFeedback.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BachataFeedback.Api.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        Console.WriteLine("DesignTimeDbContextFactory.CreateDbContext called!");

        var connectionString = "Server=localhost;Port=3306;Database=bachata_feedback;User=bachata_user;Password=bachata_pass;AllowUserVariables=true;UseAffectedRows=false;";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Используем явную версию MySQL без AutoDetect
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

        optionsBuilder.UseMySql(connectionString, serverVersion, options =>
        {
            options.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}