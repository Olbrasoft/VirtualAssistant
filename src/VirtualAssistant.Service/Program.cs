using Olbrasoft.VirtualAssistant.Service.Extensions;

namespace Olbrasoft.VirtualAssistant.Service;

public class Program
{
    public static async Task Main(string[] args)
    {
        // DELIBERATE STARTUP CRASH FOR FIFO WAKE TEST (issue #894) — will be reverted immediately
        throw new InvalidOperationException("FIFO wake test: deliberate startup crash for failedStep verification (#894)");

        var configPath = Path.Combine(AppContext.BaseDirectory, "../config/appsettings.json");

        using var lockManager = HostingExtensions.TryAcquireSingleInstanceLock(configPath);
        if (lockManager == null)
        {
            Environment.Exit(1);
            return;
        }

        HostingExtensions.PrintBanner();

        var builder = WebApplication.CreateBuilder(args);
        builder.ConfigureVirtualAssistant(configPath);

        await using var app = builder.Build();
        app.ConfigurePipeline();

        try
        {
            await app.RunWithTrayAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}
