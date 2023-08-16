using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Keyapp
{
    public class CommandRunner : IDisposable
    {
        private const string endMarker = "!!END!!";

        private readonly Process process;
        private readonly SemaphoreSlim semaphore = new(1);
        private readonly StringBuilder errorBuffer = new();
        public CommandRunner()
        {
            process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = "adb.exe";
            startInfo.Arguments = "shell";
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardError = true;
            startInfo.UseShellExecute = false;

            process.ErrorDataReceived += ErrorDataReceived;
            process.StartInfo = startInfo;
            process.Start();
            process.BeginErrorReadLine();
        }

        private void ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorBuffer.Append(e.Data);
            }
        }

        public void Dispose()
        {
            process.Close();
            process.Dispose();
        }

        public async Task<string> RunCommand(string command)
        {
            semaphore.Wait();
            try
            {
                Trace.WriteLine($"running command {command}");
                string? line;

                ThrowIfError();

                await process.StandardInput.WriteLineAsync(command);
                await process.StandardInput.WriteLineAsync($"echo {endMarker}");
                await process.StandardInput.FlushAsync();
                var output = new StringBuilder();
                while (!string.IsNullOrEmpty(line = await process.StandardOutput.ReadLineAsync()) && line != endMarker)
                {
                    output.AppendLine(line);
                }

                ThrowIfError();

                var result = output.ToString();
                Trace.WriteLine($"result: {result}");
                return result;
            }
            finally
            {
                semaphore.Release();
            }
        }

        private void ThrowIfError()
        {
            if (errorBuffer.Length > 0)
            {
                var bufferContents = errorBuffer.ToString();
                errorBuffer.Clear();
                throw new AppException($"Adb error: {bufferContents}");
            }
        }
    }
}
