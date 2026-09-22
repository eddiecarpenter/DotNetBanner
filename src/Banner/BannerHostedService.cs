using Microsoft.Extensions.Hosting;

namespace Banner;

/// <summary>Prints the banner when the host starts.</summary>
internal sealed class BannerHostedService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        BannerRuntime.PrintBanner();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}