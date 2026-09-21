using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CodexWeeklyTray
{
    internal sealed class CodexAppServerClient : IDisposable
    {
        private readonly SemaphoreSlim requestLock = new SemaphoreSlim(1, 1);
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private readonly Action<string> log;
        private Process process;
        private StreamWriter writer;
        private StreamReader reader;
        private int nextRequestId;
        private bool disposed;

        public CodexAppServerClient(Action<string> logAction)
        {
            log = logAction ?? delegate { };
        }

        public async Task<UsageSnapshot> FetchWeeklyUsageAsync()
        {
            await requestLock.WaitAsync().ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                await EnsureStartedAsync().ConfigureAwait(false);

                int requestId = Interlocked.Increment(ref nextRequestId);
                await SendAsync(new Dictionary<string, object>
                {
                    { "method", "account/rateLimits/read" },
                    { "id", requestId }
                }).ConfigureAwait(false);

                IDictionary<string, object> result = await ReadResponseAsync(requestId, 15000).ConfigureAwait(false);
                return UsageParser.Parse(result);
            }
            catch
            {
                StopProcess();
                throw;
            }
            finally
            {
                requestLock.Release();
            }
        }

        private async Task EnsureStartedAsync()
        {
            if (process != null && !process.HasExited && writer != null && reader != null)
            {
                return;
            }

            StopProcess();
            string codexPath = CodexLocator.FindExecutable();
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = codexPath,
                Arguments = "app-server",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            process = new Process();
            process.StartInfo = startInfo;
            process.EnableRaisingEvents = true;
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs args)
            {
                if (!String.IsNullOrWhiteSpace(args.Data))
                {
                    log("app-server: " + args.Data);
                }
            };

            if (!StartWithBomFreeStandardInput(process))
            {
                throw new InvalidOperationException("codex app-server could not be started.");
            }

            process.BeginErrorReadLine();
            writer = process.StandardInput;
            reader = process.StandardOutput;
            writer.AutoFlush = true;
            if (writer.Encoding.GetPreamble().Length > 0)
            {
                // Keep a framework-injected preamble away from the first JSON-RPC line.
                await writer.WriteLineAsync().ConfigureAwait(false);
            }

            int initializeId = Interlocked.Increment(ref nextRequestId);
            await SendAsync(new Dictionary<string, object>
            {
                { "method", "initialize" },
                { "id", initializeId },
                { "params", new Dictionary<string, object>
                    {
                        { "clientInfo", new Dictionary<string, object>
                            {
                                { "name", "codex_weekly_tray" },
                                { "title", "Codex Weekly Tray" },
                                { "version", "0.1.0" }
                            }
                        }
                    }
                }
            }).ConfigureAwait(false);

            await ReadResponseAsync(initializeId, 15000).ConfigureAwait(false);
            await SendAsync(new Dictionary<string, object>
            {
                { "method", "initialized" },
                { "params", new Dictionary<string, object>() }
            }).ConfigureAwait(false);
        }

        private static bool StartWithBomFreeStandardInput(Process targetProcess)
        {
            Encoding originalEncoding = null;
            bool encodingChanged = false;
            try
            {
                originalEncoding = Console.InputEncoding;
                Console.InputEncoding = Encoding.ASCII;
                encodingChanged = true;
            }
            catch
            {
                // A GUI process may not have a console. The fallback below still starts
                // the child; the caller can reconnect after any transport failure.
            }

            try
            {
                return targetProcess.Start();
            }
            finally
            {
                if (encodingChanged && originalEncoding != null)
                {
                    try { Console.InputEncoding = originalEncoding; } catch { }
                }
            }
        }

        private async Task SendAsync(object message)
        {
            if (writer == null)
            {
                throw new InvalidOperationException("App Server has not been started.");
            }

            string json = serializer.Serialize(message);
            await writer.WriteLineAsync(json).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
        }

        private async Task<IDictionary<string, object>> ReadResponseAsync(int requestId, int timeoutMilliseconds)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
            {
                int remaining = Math.Max(1, timeoutMilliseconds - (int)stopwatch.ElapsedMilliseconds);
                Task<string> readTask = reader.ReadLineAsync();
                Task completed = await Task.WhenAny(readTask, Task.Delay(remaining)).ConfigureAwait(false);
                if (completed != readTask)
                {
                    throw new TimeoutException("Timed out while reading the Codex rate limit.");
                }

                string line = await readTask.ConfigureAwait(false);
                if (line == null)
                {
                    throw new EndOfStreamException("Codex App Server exited unexpectedly.");
                }

                IDictionary<string, object> message;
                try
                {
                    message = serializer.DeserializeObject(line) as IDictionary<string, object>;
                }
                catch (Exception exception)
                {
                    log("Ignored invalid app-server output: " + exception.Message);
                    continue;
                }

                if (message == null || !MatchesRequestId(message, requestId))
                {
                    continue;
                }

                object error;
                if (message.TryGetValue("error", out error) && error != null)
                {
                    throw new InvalidOperationException("Codex App Server returned an error: " + serializer.Serialize(error));
                }

                object result;
                if (!message.TryGetValue("result", out result))
                {
                    throw new InvalidOperationException("The Codex App Server response has no result field.");
                }

                IDictionary<string, object> resultDictionary = result as IDictionary<string, object>;
                if (resultDictionary == null)
                {
                    return new Dictionary<string, object>();
                }
                return resultDictionary;
            }

            throw new TimeoutException("Timed out while reading the Codex rate limit.");
        }

        private static bool MatchesRequestId(IDictionary<string, object> message, int requestId)
        {
            object idValue;
            if (!message.TryGetValue("id", out idValue) || idValue == null)
            {
                return false;
            }

            try
            {
                return Convert.ToInt32(idValue) == requestId;
            }
            catch
            {
                return false;
            }
        }

        private void StopProcess()
        {
            StreamWriter oldWriter = writer;
            StreamReader oldReader = reader;
            Process oldProcess = process;
            writer = null;
            reader = null;
            process = null;

            if (oldWriter != null)
            {
                try { oldWriter.Dispose(); } catch { }
            }
            if (oldReader != null)
            {
                try { oldReader.Dispose(); } catch { }
            }
            if (oldProcess != null)
            {
                try
                {
                    if (!oldProcess.HasExited)
                    {
                        oldProcess.Kill();
                        oldProcess.WaitForExit(1000);
                    }
                }
                catch { }
                finally
                {
                    oldProcess.Dispose();
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException("CodexAppServerClient");
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            StopProcess();
            requestLock.Dispose();
        }
    }
}
