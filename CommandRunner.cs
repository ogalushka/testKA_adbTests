using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Keyapp
{
    public class CommandRunner : IDisposable
    {
        private readonly Process process;
        private bool initialized = false;
        public CommandRunner()
        {
            process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardInput = true;
            startInfo.UseShellExecute = false;

            process.StartInfo = startInfo;
            process.Start();
        }

        public void Dispose()
        {
            process.Close();
            process.Dispose();
        }

        public async Task<string> RunCommand(string command)
        {
            Trace.WriteLine($"running command {command}");
            // TODO semaphore
            if (!initialized)
            {
                // read out cmd intro
                Trace.WriteLine("Setting up cmd, reading windows info");
                while (!string.IsNullOrEmpty(await process.StandardOutput.ReadLineAsync()))
                {
                }
                initialized = true;
            }

            await process.StandardInput.WriteLineAsync(command);
            await process.StandardInput.FlushAsync();
            var output = new StringBuilder();
            string? line;
            // skip one line which is the command itself
            await process.StandardOutput.ReadLineAsync();
            while (!string.IsNullOrEmpty(line = await process.StandardOutput.ReadLineAsync()))
            {
                output.AppendLine(line);
            }

            var result = output.ToString();
            Trace.WriteLine($"result: {result}");

            return result;
        }
    }
}
