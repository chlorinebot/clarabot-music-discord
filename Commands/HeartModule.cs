using Discord;
using Discord.Commands;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Clara_bot.Commands
{
    public class HeartModule : ModuleBase<ICommandContext>
    {
        private enum MetricLevel
        {
            Good,
            Medium,
            NearBad,
            Bad,
        }

        private static readonly HttpClient MetricsHttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(40),
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {
            public uint DwLowDateTime;
            public uint DwHighDateTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MemoryStatusEx
        {
            public uint DwLength;
            public uint DwMemoryLoad;
            public ulong UllTotalPhys;
            public ulong UllAvailPhys;
            public ulong UllTotalPageFile;
            public ulong UllAvailPageFile;
            public ulong UllTotalVirtual;
            public ulong UllAvailVirtual;
            public ulong UllAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

        private static ulong ToUInt64(FileTime fileTime)
        {
            return ((ulong)fileTime.DwHighDateTime << 32) | fileTime.DwLowDateTime;
        }

        private static async Task<double?> GetCpuUsagePercentAsync()
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            if (!GetSystemTimes(out var idleTime1, out var kernelTime1, out var userTime1))
            {
                return null;
            }

            await Task.Delay(600);

            if (!GetSystemTimes(out var idleTime2, out var kernelTime2, out var userTime2))
            {
                return null;
            }

            var idle = ToUInt64(idleTime2) - ToUInt64(idleTime1);
            var kernel = ToUInt64(kernelTime2) - ToUInt64(kernelTime1);
            var user = ToUInt64(userTime2) - ToUInt64(userTime1);
            var total = kernel + user;
            if (total == 0 || total < idle)
            {
                return null;
            }

            return (total - idle) * 100d / total;
        }

        private static (double? UsedGb, double? TotalGb, double? UsedPercent) GetMemoryStatus()
        {
            if (!OperatingSystem.IsWindows())
            {
                return (null, null, null);
            }

            var status = new MemoryStatusEx
            {
                DwLength = (uint)Marshal.SizeOf<MemoryStatusEx>(),
            };

            if (!GlobalMemoryStatusEx(ref status) || status.UllTotalPhys == 0)
            {
                return (null, null, null);
            }

            var totalGb = status.UllTotalPhys / 1024d / 1024d / 1024d;
            var usedBytes = status.UllTotalPhys - status.UllAvailPhys;
            var usedGb = usedBytes / 1024d / 1024d / 1024d;
            var usedPercent = usedBytes * 100d / status.UllTotalPhys;
            return (usedGb, totalGb, usedPercent);
        }

        private static async Task<long?> GetLatencyAsync()
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("1.1.1.1", 3000);
                return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<double?> GetDownloadSpeedMbpsAsync(CancellationToken cancellationToken)
        {
            try
            {
                const long bytesToRead = 5_000_000;
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://speed.cloudflare.com/__down?bytes=5000000");
                var stopwatch = Stopwatch.StartNew();
                using var response = await MetricsHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var buffer = new byte[81920];
                long totalRead = 0;
                while (totalRead < bytesToRead)
                {
                    var remaining = (int)Math.Min(buffer.Length, bytesToRead - totalRead);
                    var read = await stream.ReadAsync(buffer.AsMemory(0, remaining), cancellationToken);
                    if (read <= 0)
                    {
                        break;
                    }

                    totalRead += read;
                }

                stopwatch.Stop();
                if (totalRead <= 0 || stopwatch.Elapsed.TotalSeconds <= 0)
                {
                    return null;
                }

                return totalRead * 8d / 1_000_000d / stopwatch.Elapsed.TotalSeconds;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<double?> GetUploadSpeedMbpsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var payload = new byte[2_000_000];
                RandomNumberGenerator.Fill(payload);

                using var content = new ByteArrayContent(payload);
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://httpbin.org/post")
                {
                    Content = content,
                };

                var stopwatch = Stopwatch.StartNew();
                using var response = await MetricsHttpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                stopwatch.Stop();

                if (stopwatch.Elapsed.TotalSeconds <= 0)
                {
                    return null;
                }

                return payload.Length * 8d / 1_000_000d / stopwatch.Elapsed.TotalSeconds;
            }
            catch
            {
                return null;
            }
        }

        private static MetricLevel EvaluatePercent(double percent)
        {
            if (percent < 50)
            {
                return MetricLevel.Good;
            }

            if (percent < 70)
            {
                return MetricLevel.Medium;
            }

            if (percent < 85)
            {
                return MetricLevel.NearBad;
            }

            return MetricLevel.Bad;
        }

        private static MetricLevel EvaluateLatency(long latencyMs)
        {
            if (latencyMs < 40)
            {
                return MetricLevel.Good;
            }

            if (latencyMs < 80)
            {
                return MetricLevel.Medium;
            }

            if (latencyMs < 150)
            {
                return MetricLevel.NearBad;
            }

            return MetricLevel.Bad;
        }

        private static MetricLevel EvaluateDownloadSpeed(double speedMbps)
        {
            if (speedMbps >= 100)
            {
                return MetricLevel.Good;
            }

            if (speedMbps >= 30)
            {
                return MetricLevel.Medium;
            }

            if (speedMbps >= 10)
            {
                return MetricLevel.NearBad;
            }

            return MetricLevel.Bad;
        }

        private static MetricLevel EvaluateUploadSpeed(double speedMbps)
        {
            if (speedMbps >= 50)
            {
                return MetricLevel.Good;
            }

            if (speedMbps >= 15)
            {
                return MetricLevel.Medium;
            }

            if (speedMbps >= 5)
            {
                return MetricLevel.NearBad;
            }

            return MetricLevel.Bad;
        }

        private static string GetMetricIndicator(MetricLevel level)
        {
            return level switch
            {
                MetricLevel.Good => "🟢 Tốt",
                MetricLevel.Medium => "🟡 Trung bình",
                MetricLevel.NearBad => "🟠 Cận tệ",
                _ => "🔴 Tệ",
            };
        }

        [Command("pingclara")]
        [Alias("ping")]
        [Summary("Kiểm tra CPU, RAM và tốc độ đường truyền internet.")]
        public async Task PingClaraAsync()
        {
            var loadingMessage = await ReplyAsync("🔍 Đang kiểm tra tài nguyên máy chủ và tốc độ mạng...");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));

            var cpuTask = GetCpuUsagePercentAsync();
            var latencyTask = GetLatencyAsync();
            var downloadTask = GetDownloadSpeedMbpsAsync(cts.Token);
            var uploadTask = GetUploadSpeedMbpsAsync(cts.Token);

            var memory = GetMemoryStatus();
            await Task.WhenAll(cpuTask, latencyTask, downloadTask, uploadTask);

            var cpuValue = cpuTask.Result is double cpu
                ? $"{cpu:F1}% ({GetMetricIndicator(EvaluatePercent(cpu))})"
                : "Không đo được";
            var ramValue = memory.UsedGb is double usedGb && memory.TotalGb is double totalGb && memory.UsedPercent is double usedPercent
                ? $"{usedGb:F1}/{totalGb:F1} GB ({usedPercent:F1}% - {GetMetricIndicator(EvaluatePercent(usedPercent))})"
                : "Không đo được";
            var latencyValue = latencyTask.Result is long latency
                ? $"{latency} ms ({GetMetricIndicator(EvaluateLatency(latency))})"
                : "Không đo được";
            var downloadValue = downloadTask.Result is double down
                ? $"{down:F2} Mbps ({GetMetricIndicator(EvaluateDownloadSpeed(down))})"
                : "Không đo được";
            var uploadValue = uploadTask.Result is double up
                ? $"{up:F2} Mbps ({GetMetricIndicator(EvaluateUploadSpeed(up))})"
                : "Không đo được";

            var embed = new EmbedBuilder()
                .WithTitle("Kết quả /pingclara")
                .AddField("CPU", cpuValue, true)
                .AddField("RAM", ramValue, true)
                .AddField("Độ trễ", latencyValue, true)
                .AddField("Tải xuống", downloadValue, true)
                .AddField("Tải lên", uploadValue, true)
                .WithColor(Color.Orange)
                .WithCurrentTimestamp()
                .Build();

            await loadingMessage.ModifyAsync(msg =>
            {
                msg.Content = "";
                msg.Embed = embed;
            });
        }
    }
}
