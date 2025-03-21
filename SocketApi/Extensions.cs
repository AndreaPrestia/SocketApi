using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SocketApi;

public static class Extensions
{
    public static IHostBuilder AddSocketApi(this IHostBuilder builder, int port, X509Certificate2 certificate,
        long maxRequestLength = 1024 * 1024,
        long maxResponseLength  = 1024 * 1024,
        int backlog = 100)
    {
        return builder.ConfigureServices((_, services) =>
        {
            services.AddHostedService(serviceProvider => new TcpSslServer(port, certificate,
                backlog, maxRequestLength, maxResponseLength,
                serviceProvider.GetRequiredService<ILogger<TcpSslServer>>()));
        });
    }
}