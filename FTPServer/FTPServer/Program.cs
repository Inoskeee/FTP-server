using FTPServer.Providers;
using FTPServer.Providers.FileSystem;
using FubarDev.FtpServer;
using FubarDev.FtpServer.AccountManagement;
using FubarDev.FtpServer.FileSystem;
using FubarDev.FtpServer.FileSystem.DotNet;
using Microsoft.AspNetCore;

namespace QuickStart.AspNetCoreHost
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
               .ConfigureServices(
                    services =>
                    {
                        services
                           .AddFtpServer(
                                builder => builder
                                .Services.AddSingleton<IFileSystemClassFactory, CustomFileSystemProvider>()
                                        .AddSingleton<IUnixFileSystem, CustomFileSystem>())
                           .AddHostedService<HostedFtpService>()
                           .Configure<FtpServerOptions>(opt => opt.ServerAddress = "0.0.0.0")
                           .Configure<FtpServerOptions>(opt => opt.Port = 5000)
                           .Configure<DotNetFileSystemOptions>(opt => opt
                                    .RootPath = Path.Combine(Path.GetTempPath(), "CustomFtpServer"))
                           .AddSingleton<IMembershipProvider, AuthProvider>();
                        

                    })
                .UseStartup<Startup>();

        
        private class HostedFtpService : IHostedService
        {
            private readonly IFtpServerHost _ftpServerHost;
            
            public HostedFtpService(
                IFtpServerHost ftpServerHost)
            {
                _ftpServerHost = ftpServerHost;
            }
            
            /// <inheritdoc />
            public Task StartAsync(CancellationToken cancellationToken)
            {
                return _ftpServerHost.StartAsync(cancellationToken);
            }
            /// <inheritdoc />
            public Task StopAsync(CancellationToken cancellationToken)
            {
                return _ftpServerHost.StopAsync(cancellationToken);
            }

        }
    }
}
