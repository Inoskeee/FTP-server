using FubarDev.FtpServer;
using FubarDev.FtpServer.CommandHandlers;
using FubarDev.FtpServer.FileSystem;
using FubarDev.FtpServer.FileSystem.DotNet;
using Microsoft.Extensions.Options;

namespace FTPServer.Providers.FileSystem
{
    public class CustomFileSystemProvider : IFileSystemClassFactory
    {
        private readonly IAccountDirectoryQuery _accountDirectoryQuery;
        private readonly ILogger<CustomFileSystemProvider>? _logger;
        private readonly string _rootPath;
        private readonly int _streamBufferSize;
        private readonly bool _allowNonEmptyDirectoryDelete;
        private readonly bool _flushAfterWrite;

        public CustomFileSystemProvider(
            IOptions<DotNetFileSystemOptions> options,
            IAccountDirectoryQuery accountDirectoryQuery,
            ILogger<CustomFileSystemProvider>? logger = null)
        {
            _accountDirectoryQuery = accountDirectoryQuery;
            _logger = logger;
            _rootPath = string.IsNullOrEmpty(options.Value.RootPath)
                ? Path.GetTempPath()
                : options.Value.RootPath!;
            _streamBufferSize = options.Value.StreamBufferSize ?? CustomFileSystem.DefaultStreamBufferSize;
            _allowNonEmptyDirectoryDelete = options.Value.AllowNonEmptyDirectoryDelete;
            _flushAfterWrite = options.Value.FlushAfterWrite;
        }
        public Task<IUnixFileSystem> Create(IAccountInformation accountInformation)
        {
            var path = _rootPath;
            var directories = _accountDirectoryQuery.GetDirectories(accountInformation);
            if (!string.IsNullOrEmpty(directories.RootPath))
            {
                path = Path.Combine(path, directories.RootPath);
            }
            _logger?.LogInformation("The root directory for {userName} is {rootPath}", accountInformation.FtpUser.Identity!.Name, path);

            return Task.FromResult<IUnixFileSystem>(new CustomFileSystem(path, _allowNonEmptyDirectoryDelete, _streamBufferSize, _flushAfterWrite));
        }
    }
}
