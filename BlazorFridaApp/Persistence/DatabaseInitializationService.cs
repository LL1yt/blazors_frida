using Microsoft.EntityFrameworkCore;
using Serilog;
using ILogger = Serilog.ILogger;

namespace BlazorFridaApp.Persistence;

public interface IDatabaseInitializationService
{
    Task InitializeDatabaseAsync();
}

public class DatabaseInitializationService : IDatabaseInitializationService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger _logger;
    private const string DbFileName = "gamememory.db";

    public DatabaseInitializationService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
        _logger = Log.ForContext<DatabaseInitializationService>();
    }

    public async Task InitializeDatabaseAsync()
    {
        try
        {
            if (!File.Exists(DbFileName))
            {
                _logger.Information("Database file not found. Creating new database...");
                await _dbContext.Database.EnsureDeletedAsync();
                await _dbContext.Database.MigrateAsync();
                _logger.Information("Database created and migrations applied successfully");
            }
            else if (!await _dbContext.Database.CanConnectAsync())
            {
                _logger.Warning("Database file exists but connection failed. Recreating database...");
                await _dbContext.Database.EnsureDeletedAsync();
                await _dbContext.Database.MigrateAsync();
                _logger.Information("Database recreated and migrations applied successfully");
            }
            else
            {
                _logger.Information("Database exists and is accessible");
                await _dbContext.Database.MigrateAsync();
                _logger.Information("Ensured all migrations are applied");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during database initialization");
            throw;
        }
    }
}