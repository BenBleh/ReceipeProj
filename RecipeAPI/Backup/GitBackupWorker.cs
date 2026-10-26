namespace RecipeAPI.Backup;

public sealed class GitBackupWorker(GitBackupQueue queue, ILogger<GitBackupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!queue.Enabled)
            return;
        logger.LogInformation("Automatic recipe Git backups enabled; status is server-only");
        try { await Task.Run(queue.CheckAccess, stoppingToken); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Backup access check could not write to the private queue");
        }
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try { await Task.Run(() => queue.ProcessPending(stoppingToken), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogError(exception, "Backup queue storage is unavailable; server-side intervention is required");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
