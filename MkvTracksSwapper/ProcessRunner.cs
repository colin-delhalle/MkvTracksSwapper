using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace MkvTracksSwapper
{
    public sealed class ProcessRunner : IDisposable
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly Process process;
        private readonly string processNameCopy; // to be able to log process name after process exited
        private TaskCompletionSource<bool> eventHandler;
        private readonly StringBuilder error;
        private readonly TimeSpan? timeout;
        private bool isRunning;

        public bool Successful { get; private set; }
        public string Error { get; private set; }

        private bool isDisposed;

        public ProcessRunner(string processName, TimeSpan? executionTimeout = null, DataReceivedEventHandler onOutputCallback = null, bool useEmbeddedProgram = true)
        {
            processNameCopy = processName;
            timeout = executionTimeout;
            isRunning = false;
            error = new StringBuilder();
            Successful = false;

            var path = processName;
            if (useEmbeddedProgram)
            {
                var embeddedProgramPath = Path.Combine(AppContext.BaseDirectory, $"{processName}.exe");
                if (File.Exists(embeddedProgramPath))
                {
                    path = embeddedProgramPath;
                }
                else
                {
                    logger.Trace($"Executable not found at path {embeddedProgramPath}");
                }
            }

            logger.Trace($"Creating process {processName}{(useEmbeddedProgram ? $" using executable {path}" : string.Empty)}");
            process = new Process
            {
                StartInfo = BuildProcessStartInfo(path),
                EnableRaisingEvents = true
            };
            process.Exited += OnExited;
            process.ErrorDataReceived += OnErrorDataReceived;
            process.OutputDataReceived += CatchErrorWrittenOnStandardOutput;
            if (onOutputCallback != null)
            {
                process.OutputDataReceived += onOutputCallback;
            }
        }

        public async Task Run(CancellationToken ct = default)
        {
            logger.Trace($"Running process {process.ProcessName} without arguments");

            await RunWithArg(null, ct);
        }

        public async Task<bool> RunWithArg(string arg, CancellationToken ct = default)
        {
            logger.Trace($"Running process {processNameCopy} with arguments {arg}");

            eventHandler = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (ct != default)
            {
                ct.Register(() =>
                {
                    if (isRunning)
                    {
                        process.Kill();
                        Error = "Task was cancelled and process was killed";
                        logger.Trace(Error);
                        eventHandler.SetResult(false);
                    }
                });
            }

            if (arg != null)
            {
                process.StartInfo.Arguments = arg;
            }

            try
            {
                process.Start();
                isRunning = true;

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                return timeout.HasValue
                    ? await TimeoutAfter(eventHandler.Task, timeout.Value)
                    : await eventHandler.Task;
            }
            catch (Exception e)
            {
                Error += $"Error occurred while running process {processNameCopy}: {e.Message}";
                logger.Fatal(e);
                return false;
            }
        }

        private async Task<bool> TimeoutAfter(Task<bool> task, TimeSpan to)
        {
            using var cts = new CancellationTokenSource();
            var delayTask = Task.Delay(to, cts.Token);

            var resultTask = await Task.WhenAny(task, delayTask);
            if (resultTask == delayTask)
            {
                Error = $"Operation was stopped after exceeding timeout of {to.Seconds} seconds";
                logger.Trace(Error);
                return false;
            }

            // Cancel the timer task so that it does not fire
            cts.Cancel();

            return await task;
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                error.Append(e.Data);
            }
        }

        private void CatchErrorWrittenOnStandardOutput(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null && e.Data.StartsWith("Error: "))
            {
                OnErrorDataReceived(sender, e);
            }
        }

        private void OnExited(object sender, EventArgs args)
        {
            Successful = process.ExitCode == 0;
            if (!Successful)
            {
                Error = error.ToString();
                logger.Trace($"Process {processNameCopy} exited with non 0 exit code ({process.ExitCode}){Environment.NewLine}Error log:{Environment.NewLine}{Error}");
            }
            else
            {
                logger.Trace($"Process {processNameCopy} ran successfully");
            }
            isRunning = false;
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

            logger.Trace($"Process runner for {processNameCopy} is disposed");
            isDisposed = true;
        }

        #endregion implementation of IDisposable
    }
}