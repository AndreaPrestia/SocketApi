using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SocketApi.Server;

public static class Extensions
{
    public static IHostBuilder AddSocketApi(this IHostBuilder builder, string certPath, string certPassword, int port,
        int backlog = 100)
    {
        return builder.ConfigureServices((_, services) =>
        {
            services.AddHostedService(serviceProvider => new TcpSslServer(port, certPath, certPassword,
                backlog, serviceProvider.GetRequiredService<ILogger<TcpSslServer>>()));
        });
    }
}