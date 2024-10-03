using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace YourNamespace.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SFTPController : ControllerBase
    {
        private readonly string _localFolderPath = @"C:\\Users\\elhas\\Desktop\\src";
        private readonly string _remoteFolderPath = "/shared";
        private readonly string _host = "127.0.0.1";
        private readonly int _port = 14149; // Use the port your server is listening on
        private readonly string _username = "sftpuser";
        private readonly string _password = "Root__2025";
        private readonly ILogger<SFTPController> _logger;

        public SFTPController(ILogger<SFTPController> logger)
        {
            _logger = logger;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendFiles()
        {
            var files = Directory.GetFiles(_localFolderPath);
            int maxRetries = 3;
            int retryDelay = 2000; // 2 seconds

            foreach (var file in files)
            {
                bool success = false;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        using (var sftp = new SftpClient(_host, _port, _username, _password))
                        {
                            _logger.LogInformation("Attempting to connect to SFTP server at {Host}:{Port}", _host, _port);
                            sftp.ConnectionInfo.Timeout = TimeSpan.FromMinutes(2); // Set a reasonable timeout
                            sftp.Connect();
                            _logger.LogInformation("Connected to SFTP server.");

                            using (var fileStream = new FileStream(file, FileMode.Open))
                            {
                                var remoteFileName = Path.Combine(_remoteFolderPath, Path.GetFileName(file)).Replace("\\", "/");
                                sftp.UploadFile(fileStream, remoteFileName);
                                _logger.LogInformation("Uploaded file: {File} to {RemoteFileName}", file, remoteFileName);
                            }

                            sftp.Disconnect();
                            _logger.LogInformation("Disconnected from SFTP server.");
                            success = true;
                            break;
                        }
                    }
                    catch (SocketException ex)
                    {
                        _logger.LogError(ex, "SocketException: An existing connection was forcibly closed by the remote host. Attempt {Attempt} of {MaxRetries}", attempt, maxRetries);
                        if (attempt < maxRetries)
                        {
                            await Task.Delay(retryDelay);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An error occurred while uploading the file: {Message}. Attempt {Attempt} of {MaxRetries}", ex.Message, attempt, maxRetries);
                        if (attempt < maxRetries)
                        {
                            await Task.Delay(retryDelay);
                        }
                    }
                }

                if (!success)
                {
                    return StatusCode(500, $"Failed to upload file: {file} after {maxRetries} attempts.");
                }
            }

            return Ok("Files sent to remote server.");
        }
    }
}
