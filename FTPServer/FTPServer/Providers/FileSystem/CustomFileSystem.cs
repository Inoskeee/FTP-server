using FubarDev.FtpServer.BackgroundTransfer;
using FubarDev.FtpServer.FileSystem;
using FubarDev.FtpServer.FileSystem.DotNet;
using QuickStart.AspNetCoreHost;
using Serilog;
using System.IO;
using System.IO.Pipelines;
using System.Security.Cryptography;
using System.Text;

namespace FTPServer.Providers.FileSystem
{
    public class CustomFileSystem : IUnixFileSystem
    {
        #region parameters
        public static readonly int DefaultStreamBufferSize = 4096;

        private readonly int _streamBufferSize;
        private readonly bool _flushStream;

        private static ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        private readonly Microsoft.Extensions.Logging.ILogger _logger = loggerFactory.CreateLogger<CustomFileSystem>();
        #endregion

        #region constructors
        public CustomFileSystem(string rootPath, bool allowNonEmptyDirectoryDelete)
            : this(rootPath, allowNonEmptyDirectoryDelete, DefaultStreamBufferSize)
        {
        }

        public CustomFileSystem(string rootPath, bool allowNonEmptyDirectoryDelete, int streamBufferSize)
            : this(rootPath, allowNonEmptyDirectoryDelete, streamBufferSize, false)
        {

        }

        public CustomFileSystem(string rootPath, bool allowNonEmptyDirectoryDelete, int streamBufferSize, bool flushStream)
        {
            FileSystemEntryComparer = StringComparer.OrdinalIgnoreCase;
            Root = new DotNetDirectoryEntry(Directory.CreateDirectory(rootPath), true, allowNonEmptyDirectoryDelete);
            SupportsNonEmptyDirectoryDelete = allowNonEmptyDirectoryDelete;
            _streamBufferSize = streamBufferSize;
            _flushStream = flushStream;
        }
        #endregion

        #region fields
        public bool SupportsAppend { get; set; }

        public bool SupportsNonEmptyDirectoryDelete { get; set; }

        public StringComparer FileSystemEntryComparer { get; set; }

        public IUnixDirectoryEntry Root { get; set; }
        #endregion

        public async Task<IBackgroundTransfer?> AppendAsync(IUnixFileEntry fileEntry, long? startPosition, Stream data, CancellationToken cancellationToken)
        {
            var fileInfo = ((DotNetFileEntry)fileEntry).FileInfo;
            using (var output = fileInfo.OpenWrite())
            {
                if (startPosition == null)
                {
                    startPosition = fileInfo.Length;
                }

                output.Seek(startPosition.Value, SeekOrigin.Begin);
                await data.CopyToAsync(output, _streamBufferSize, cancellationToken).ConfigureAwait(false);
            }

            return null;
        }

        /// <summary>
        /// Метод, срабатывающий при добавлении нового файла
        /// </summary>
        /// <param name="targetDirectory"></param>
        /// <param name="fileName"></param>
        /// <param name="data"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IBackgroundTransfer?> CreateAsync(IUnixDirectoryEntry targetDirectory, string fileName, Stream data, CancellationToken cancellationToken)
        {
            var targetEntry = (DotNetDirectoryEntry)targetDirectory;
            var fileInfo = new FileInfo(Path.Combine(targetEntry.Info.FullName, fileName));

            string fileData = string.Empty;

            //using (StreamReader reader = new StreamReader(data, Encoding.UTF8))
            //{
            //    //fileData содержит контент, считанный из файла
            //    fileData = await reader.ReadToEndAsync();
            //    _logger.LogInformation(fileData);
            //    ///<summary>
            //    ///Тут код для передачи данных в RabbitMQ
            //    /// </summary>

            //}
            byte[] b = null;
            using (Stream stream = data)
            using (MemoryStream ms = new MemoryStream())
            {
                int count = 0;
                do
                {
                    byte[] buf = new byte[1024];
                    count = stream.Read(buf, 0, 1024);
                    ms.Write(buf, 0, count);
                } while (stream.CanRead && count > 0);
                b = ms.ToArray();
            }
            string path = $"{Path.Combine(Path.GetTempPath(), "CustomFtpServer")}\\{fileInfo.Name}";

            _logger.LogInformation(path);
            //using (StreamWriter writetext = new StreamWriter(path))
            //{
            //    await writetext.WriteAsync(fileData);
            //}
            File.WriteAllBytes(path, b);
            _logger.LogInformation($"New file named {fileInfo.Name} was created!");
            return null;
        }

        public Task<IUnixDirectoryEntry> CreateDirectoryAsync(IUnixDirectoryEntry targetDirectory, string directoryName, CancellationToken cancellationToken)
        {
            var targetEntry = (DotNetDirectoryEntry)targetDirectory;
            var newDirInfo = targetEntry.DirectoryInfo.CreateSubdirectory(directoryName);
            return Task.FromResult<IUnixDirectoryEntry>(new DotNetDirectoryEntry(newDirInfo, false, SupportsNonEmptyDirectoryDelete));
        }

        public Task<IReadOnlyList<IUnixFileSystemEntry>> GetEntriesAsync(IUnixDirectoryEntry directoryEntry, CancellationToken cancellationToken)
        {
            var result = new List<IUnixFileSystemEntry>();
            var searchDirInfo = ((DotNetDirectoryEntry)directoryEntry).DirectoryInfo;
            foreach (var info in searchDirInfo.EnumerateFileSystemInfos())
            {
                if (info is DirectoryInfo dirInfo)
                {
                    result.Add(new DotNetDirectoryEntry(dirInfo, false, SupportsNonEmptyDirectoryDelete));
                }
                else
                {
                    if (info is FileInfo fileInfo)
                    {
                        result.Add(new DotNetFileEntry(fileInfo));
                    }
                }
            }
            return Task.FromResult<IReadOnlyList<IUnixFileSystemEntry>>(result);
        }

        public Task<IUnixFileSystemEntry?> GetEntryByNameAsync(IUnixDirectoryEntry directoryEntry, string name, CancellationToken cancellationToken)
        {
            var searchDirInfo = ((DotNetDirectoryEntry)directoryEntry).Info;
            var fullPath = Path.Combine(searchDirInfo.FullName, name);
            IUnixFileSystemEntry? result;
            if (File.Exists(fullPath))
            {
                result = new DotNetFileEntry(new FileInfo(fullPath));
            }
            else if (Directory.Exists(fullPath))
            {
                result = new DotNetDirectoryEntry(new DirectoryInfo(fullPath), false, SupportsNonEmptyDirectoryDelete);
            }
            else
            {
                result = null;
            }

            return Task.FromResult(result);
        }

        public Task<IUnixFileSystemEntry> MoveAsync(IUnixDirectoryEntry parent, IUnixFileSystemEntry source, IUnixDirectoryEntry target, string fileName, CancellationToken cancellationToken)
        {
            var targetEntry = (DotNetDirectoryEntry)target;
            var targetName = Path.Combine(targetEntry.Info.FullName, fileName);

            if (source is DotNetFileEntry sourceFileEntry)
            {
                sourceFileEntry.FileInfo.MoveTo(targetName);
                return Task.FromResult<IUnixFileSystemEntry>(new DotNetFileEntry(new FileInfo(targetName)));
            }

            var sourceDirEntry = (DotNetDirectoryEntry)source;
            sourceDirEntry.DirectoryInfo.MoveTo(targetName);
            return Task.FromResult<IUnixFileSystemEntry>(new DotNetDirectoryEntry(new DirectoryInfo(targetName), false, SupportsNonEmptyDirectoryDelete));
        }

        public Task<Stream> OpenReadAsync(IUnixFileEntry fileEntry, long startPosition, CancellationToken cancellationToken)
        {
            var fileInfo = ((DotNetFileEntry)fileEntry).FileInfo;
            var input = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (startPosition != 0)
            {
                input.Seek(startPosition, SeekOrigin.Begin);
            }

            return Task.FromResult<Stream>(input);
        }

        public async Task<IBackgroundTransfer?> ReplaceAsync(IUnixFileEntry fileEntry, Stream data, CancellationToken cancellationToken)
        {
            var fileInfo = ((DotNetFileEntry)fileEntry).FileInfo;
            using (var output = fileInfo.OpenWrite())
            {
                await data.CopyToAsync(output, _streamBufferSize, cancellationToken).ConfigureAwait(false);
                output.SetLength(output.Position);
            }

            return null;
        }

        public Task<IUnixFileSystemEntry> SetMacTimeAsync(IUnixFileSystemEntry entry, DateTimeOffset? modify, DateTimeOffset? access, DateTimeOffset? create, CancellationToken cancellationToken)
        {
            var item = ((DotNetFileSystemEntry)entry).Info;

            if (access != null)
            {
                item.LastAccessTimeUtc = access.Value.UtcDateTime;
            }

            if (modify != null)
            {
                item.LastWriteTimeUtc = modify.Value.UtcDateTime;
            }

            if (create != null)
            {
                item.CreationTimeUtc = create.Value.UtcDateTime;
            }

            if (entry is DotNetDirectoryEntry dirEntry)
            {
                return Task.FromResult<IUnixFileSystemEntry>(new DotNetDirectoryEntry((DirectoryInfo)item, dirEntry.IsRoot, SupportsNonEmptyDirectoryDelete));
            }

            return Task.FromResult<IUnixFileSystemEntry>(new DotNetFileEntry((FileInfo)item));
        }

        public Task UnlinkAsync(IUnixFileSystemEntry entry, CancellationToken cancellationToken)
        {
            if (entry is DotNetDirectoryEntry dirEntry)
            {
                dirEntry.DirectoryInfo.Delete(SupportsNonEmptyDirectoryDelete);
            }
            else
            {
                var fileEntry = (DotNetFileEntry)entry;
                fileEntry.Info.Delete();
            }

            return Task.FromResult(0);
        }
    }
}
