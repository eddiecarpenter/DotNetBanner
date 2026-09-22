using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Banner;

public static class BannerHostingExtensions
{
    /// <summary>Prints the generated banner when the host starts.</summary>
    public static IHostApplicationBuilder AddBanner(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHostedService<BannerHostedService>();

        return builder;
    }
}