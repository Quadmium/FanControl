using clipr;
using StagWare.FanControl;
using StagWare.FanControl.Plugins;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using System.Threading;

namespace FanControl
{
    public class FanController
    {
        public static void ECWrite(byte register, byte value, bool verbose)
        {
            AccessEcSynchronized(ec =>
            {
                if (verbose)
                {
                    Console.WriteLine("Writing at {0}: {1} (0x{1:X2})", register, value);
                }

                ec.WriteByte(register, value);

                if (verbose)
                {
                    byte b = ec.ReadByte(register);
                    Console.WriteLine("Current value at {0}: {1} (0x{1:X2})", register, b);
                }
            });
        }

        public static void ECRead(byte register)
        {
            AccessEcSynchronized(ec =>
            {
                byte b = ec.ReadByte(register);
                Console.WriteLine("{0} (0x{0:X2})", b);
            });
        }

        public static void ECDump()
        {
            AccessEcSynchronized(ec =>
            {
                StringBuilder sb = new StringBuilder(16 * 54);

                // Read all register bytes
                for (int i = 0; i <= 0xF0; i += 0x10)
                {
                    sb.AppendFormat("{0:X2}: ", i);

                    for (int j = 0; j <= 0xF; j++)
                    {
                        byte b = ec.ReadByte((byte)(i + j));
                        sb.AppendFormat("{0:X2} ", b);
                    }

                    sb.AppendLine();
                }

                Console.WriteLine(sb);
            });
        }

        public static void ECMonitor(int timespan, int interval, string reportPath, bool clearly)
        {
            var dict = new Dictionary<byte, List<byte>>();

            Console.CancelKeyPress += (sender, e) =>
            {
                if (reportPath != null)
                {
                    SaveMonitoringReport(reportPath, dict, clearly);
                }
            };

            using (IEmbeddedController embeddedController = LoadEC())
            {
                if (embeddedController == null)
                {
                    return;
                }

                byte[] initialBytes = new byte[byte.MaxValue];

                for (byte reg = 0; reg < initialBytes.Length; reg++)
                {
                    AccessEcSynchronized(ec =>
                    {
                        initialBytes[reg] = ec.ReadByte(reg);
                    },
                    embeddedController);
                }

                int loopCount = 0;

                while ((timespan < 1) || (loopCount < Math.Ceiling(((double)timespan / interval) - 1)))
                {
                    Thread.Sleep(interval * 1000);
                    AccessEcSynchronized(ec =>
                    {
                        for (byte reg = 0; reg < initialBytes.Length; reg++)
                        {
                            byte value = ec.ReadByte(reg);

                            if (dict.ContainsKey(reg))
                            {
                                dict[reg].Add(value);
                            }
                            else if (value != initialBytes[reg])
                            {
                                var log = new List<byte>();

                                for (int j = 0; j <= loopCount; j++)
                                {
                                    log.Add(initialBytes[reg]);
                                }

                                log.Add(value);
                                dict.Add(reg, log);
                            }
                        }
                    },
                    embeddedController);

                    Console.Clear();
                    PrintMonitoringStatus(dict, clearly);
                    loopCount++;
                }
            }

            if (reportPath != null)
            {
                SaveMonitoringReport(reportPath, dict, clearly);
            }
        }

        public static void SaveMonitoringReport(
            string path,
            IEnumerable<KeyValuePair<byte, List<byte>>> registerLogs,
            bool clearly)
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                Console.Error.WriteLine($"Could not save report: {path} already exists");
                return;
            }

            try
            {
                var report = new StringBuilder();

                foreach (var pair in registerLogs.OrderBy(x => x.Key))
                {
                    report.AppendFormat("0x{0:X2}: ", pair.Key);

                    AppendRegisterLog(report, pair.Value, 0, clearly);
                    report.AppendLine();
                }

                File.WriteAllText(path, report.ToString());
            }
            catch (Exception ex)
            {
                string msg = "Could not save report";

                if (!string.IsNullOrWhiteSpace(ex.Message))
                {
                    msg += $": {ex.Message}";
                }

                Console.Error.WriteLine(msg);
            }
        }

        public static void PrintMonitoringStatus(
            IEnumerable<KeyValuePair<byte, List<byte>>> registerLogs,
            bool clearly)
        {
            var report = new StringBuilder();

            foreach (var pair in registerLogs.OrderBy(x => x.Key))
            {
                int start = 0;
                report.AppendFormat("0x{0:X2}: ", pair.Key);

                if (6 + pair.Value.Count * 3 > Console.BufferWidth)
                {
                    report.Append("...,");
                    start = pair.Value.Count - ((Console.BufferWidth - 10) / 3);
                }

                AppendRegisterLog(report, pair.Value, start, clearly);
                report.AppendLine();
            }

            Console.Write(report.ToString());
        }

        public static void AppendRegisterLog(StringBuilder report, List<byte> log, int start, bool clearly)
        {
            byte? prev = null;

            for (int i = start; i < log.Count - 1; i++)
            {
                if (clearly && (log[i] == prev))
                {
                    report.Append("   ");
                }
                else
                {
                    report.AppendFormat("{0:X2},", log[i]);
                    prev = log[i];
                }
            }

            report.AppendFormat("{0:X2}", log.Last());
        }

        public static IEmbeddedController LoadEC()
        {
            var ecLoader = new FanControlPluginLoader<IEmbeddedController>("Plugins");

            if (ecLoader.FanControlPlugin == null)
            {
                Console.Error.WriteLine("Could not load EC plugin");
                return null;
            }

            ecLoader.FanControlPlugin.Initialize();

            if (ecLoader.FanControlPlugin.IsInitialized)
            {
                return ecLoader.FanControlPlugin;
            }
            else
            {
                Console.Error.WriteLine("EC initialization failed");
                ecLoader.FanControlPlugin.Dispose();
            }

            return null;
        }

        public static void AccessEcSynchronized(Action<IEmbeddedController> callback)
        {
            using (IEmbeddedController ec = LoadEC())
            {
                if (ec != null)
                {
                    AccessEcSynchronized(callback, ec);
                }
            }
        }

        public static void AccessEcSynchronized(Action<IEmbeddedController> callback, IEmbeddedController ec)
        {
            if (ec.AcquireLock(200))
            {
                try
                {
                    callback(ec);
                }
                finally
                {
                    ec.ReleaseLock();
                }
            }
            else
            {
                Console.Error.WriteLine("Could not acquire EC lock");
            }
        }
    }
}
