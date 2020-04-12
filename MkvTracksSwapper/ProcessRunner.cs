using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MkvTracksSwapper
{
    public sealed class ProcessRunner : IDisposable
    {
        private readonly Process process;
        private TaskCompletionSource<bool> eventHandler;
        private readonly StringBuilder error;
        private readonly bool captureOutput;
        private readonly bool withTimeout;

        public bool Successful { get; private set; }
        public bool HasRan { get; private set; }
        public string Error { get; private set; }

        private bool isDisposed;

        public ProcessRunner(string processName, bool withTimeout = true, DataReceivedEventHandler onOutputCallback = null, bool useEmbeddedProgram = true)
        {
            this.withTimeout = withTimeout;
            error = new StringBuilder();
            HasRan = false;
            Successful = false;

            if (useEmbeddedProgram)
            {
                var embeddedProgramPath = Path.Combine(AppContext.BaseDirectory, processName);
                if (File.Exists(embeddedProgramPath))
                {
                    processName = embeddedProgramPath;
                }
            }

            process = new Process
            {
                StartInfo = BuildProcessStartInfo(processName),
                EnableRaisingEvents = true
            };
            process.Exited += OnExited;
            process.ErrorDataReceived += OnErrorDataReceived;
            if (onOutputCallback != null)
            {
                captureOutput = true;
                process.OutputDataReceived += onOutputCallback;
            }
        }

        public async Task Run()
        {
            await RunWithArg(null);
        }

        public async Task<bool> RunWithArg(string arg)
        {
            eventHandler = new TaskCompletionSource<bool>();

            if (arg != null)
            {
                process.StartInfo.Arguments = arg;
            }

            process.Start();

            if (captureOutput)
            {
                process.BeginOutputReadLine();
            }
            process.BeginErrorReadLine();

            List<Task> toAwait = new List<Task> { eventHandler.Task };
            if (withTimeout)
            {
                toAwait.Add(Task.Delay(8000));
            }

            await Task.WhenAny(toAwait);

            return Successful;
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                Console.WriteLine("ERROR LINE: " + e.Data);
                error.Append(e.Data);
            }

            if (e.Data == null)
            {
                Console.WriteLine("FINISHED RECEIVING ERROR STREAM");
            }
        }

        private void OnExited(object sender, EventArgs args)
        {
            Successful = process.ExitCode == 0;
            HasRan = true;
            Error = error.ToString();

            Console.WriteLine("PROCESS EXITED");

            eventHandler.SetResult(Successful);
        }

        private ProcessStartInfo BuildProcessStartInfo(string processName)
        {
            return new ProcessStartInfo
            {
                FileName = processName,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
        }

        #region implementation of IDisposable

        public void Dispose()
        {
            Dispose(true);
            // ReSharper disable once GCSuppressFinalizeForTypeWithoutDestructor
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }

            if (disposing)
            {
                process?.Dispose();
            }

            isDisposed = true;
        }

        #endregion implementation of IDisposable
    }
}