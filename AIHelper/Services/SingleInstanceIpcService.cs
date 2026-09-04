// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AIHelper.Services
{
    /// <summary>
    /// Service for inter-process communication between AIHelper instances.
    /// Used when a secondary instance is launched (e.g. from Explorer context menu)
    /// to forward arguments to the running primary instance.
    /// </summary>
    public static class SingleInstanceIpcService
    {
        private const string PipeName = "AIHelper_SingleInstance_Pipe";
        private static CancellationTokenSource _cts;

        /// <summary>
        /// Starts listening for incoming arguments from secondary instances.
        /// </summary>
        public static void StartServer(Action<string[]> onArgsReceived)
        {
            StopServer();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        using (var server = new NamedPipeServerStream(
                            PipeName,
                            PipeDirection.In,
                            NamedPipeServerStream.MaxAllowedServerInstances,
                            PipeTransmissionMode.Byte,
                            PipeOptions.Asynchronous))
                        {
                            await server.WaitForConnectionAsync(token);

                            using (var reader = new StreamReader(server, Encoding.UTF8))
                            {
                                string line = await reader.ReadLineAsync();
                                if (!string.IsNullOrEmpty(line))
                                {
                                    try
                                    {
                                        var args = JsonConvert.DeserializeObject<string[]>(line);
                                        if (args != null && args.Length > 0)
                                        {
                                            onArgsReceived?.Invoke(args);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.LogError("Failed to deserialize IPC message", ex);
                                    }
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"IPC server error: {ex.Message}");
                        try
                        {
                            await Task.Delay(500, token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
            }, token);
        }

        /// <summary>
        /// Sends arguments to the running primary instance via named pipe.
        /// </summary>
        public static bool SendArgsToRunningInstance(string[] args, int timeoutMs = 1500)
        {
            if (args == null || args.Length == 0) return false;

            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    client.Connect(timeoutMs);
                    using (var writer = new StreamWriter(client, Encoding.UTF8))
                    {
                        string json = JsonConvert.SerializeObject(args);
                        writer.WriteLine(json);
                        writer.Flush();
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to send args to primary instance via pipe: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stops the IPC pipe server.
        /// </summary>
        public static void StopServer()
        {
            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
            }
            catch
            {
            }
            finally
            {
                _cts = null;
            }
        }

        /// <summary>
        /// Extracts the file path from command line arguments (e.g. --file path, -file path, --file=path).
        /// </summary>
        public static string ExtractFileArg(string[] args)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--file", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[i], "-file", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        return args[i + 1];
                    }
                }
                if (args[i].StartsWith("--file=", StringComparison.OrdinalIgnoreCase) ||
                    args[i].StartsWith("-file=", StringComparison.OrdinalIgnoreCase))
                {
                    int eqIdx = args[i].IndexOf('=');
                    if (eqIdx >= 0 && eqIdx < args[i].Length - 1)
                    {
                        return args[i].Substring(eqIdx + 1).Trim('"');
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Extracts the action ID from command line arguments (e.g. --action id, -action id, --action=id).
        /// </summary>
        public static string ExtractActionIdArg(string[] args)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--action", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[i], "-action", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        return args[i + 1];
                    }
                }
                if (args[i].StartsWith("--action=", StringComparison.OrdinalIgnoreCase) ||
                    args[i].StartsWith("-action=", StringComparison.OrdinalIgnoreCase))
                {
                    int eqIdx = args[i].IndexOf('=');
                    if (eqIdx >= 0 && eqIdx < args[i].Length - 1)
                    {
                        return args[i].Substring(eqIdx + 1).Trim('"');
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Checks whether the arguments include the --more flag.
        /// </summary>
        public static bool HasMoreArg(string[] args)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--more", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[i], "-more", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
