using Discord;
using Discord.WebSocket;
using DotNetEnv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Clara_bot.Commands
{
    public class GuildInspector
    {
        private readonly DiscordSocketClient _client;

        public GuildInspector()
        {
            var socketConfig = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds |
                                 GatewayIntents.GuildMembers,
                LogLevel = LogSeverity.Warning
            };

            _client = new DiscordSocketClient(socketConfig);
        }

        public async Task RunInspectionAsync()
        {
            Env.Load();
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");

            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("[X] DISCORD_TOKEN khong tim thay trong file .env");
                return;
            }

            var readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _client.Ready += () =>
            {
                readyTcs.TrySetResult(true);
                return Task.CompletedTask;
            };

            _client.Log += msg =>
            {
                if (msg.Severity >= LogSeverity.Warning)
                {
                    Console.WriteLine($"[Discord] {msg.Severity}: {msg.Message}");
                }
                return Task.CompletedTask;
            };

            try
            {
                await _client.LoginAsync(TokenType.Bot, token);
                await _client.StartAsync();

                var readyTask = await Task.WhenAny(readyTcs.Task, Task.Delay(TimeSpan.FromSeconds(30)));

                if (readyTask != readyTcs.Task)
                {
                    Console.WriteLine("[X] Timeout: Bot chua san sang sau 30 giay");
                    await CleanupAsync();
                    return;
                }

                await readyTcs.Task;
                await PrintReportAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[X] Loi ket noi Discord: {ex.Message}");
            }
            finally
            {
                await CleanupAsync();
            }
        }

        private async Task PrintReportAsync()
        {
            var guilds = _client.Guilds.ToList();

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine($"  BOT: {_client.CurrentUser.Username}#{_client.CurrentUser.Discriminator}");
            Console.WriteLine($"  ID : {_client.CurrentUser.Id}");
            Console.WriteLine($"  Tong so Guild da tham gia: {guilds.Count}");
            Console.WriteLine("========================================");
            Console.WriteLine();

            var totalUsers = 0;
            var totalOnline = 0;
            var totalBots = 0;

            for (int i = 0; i < guilds.Count; i++)
            {
                var guild = guilds[i];
                var members = guild.Users.ToList();
                var owner = guild.Owner;
                var onlineCount = members.Count(m => m.Status != UserStatus.Offline);
                var botCount = members.Count(m => m.IsBot);
                var humanCount = members.Count - botCount;

                totalUsers += members.Count;
                totalOnline += onlineCount;
                totalBots += botCount;

                Console.WriteLine($"[{i + 1}/{guilds.Count}] ====== {guild.Name} ======");
                Console.WriteLine($"  ID Server     : {guild.Id}");
                Console.WriteLine($"  Owner         : {owner?.Username}#{owner?.Discriminator} (ID: {owner?.Id})");
                Console.WriteLine($"  Created At    : {guild.CreatedAt:dd/MM/yyyy HH:mm:ss}");
                Console.WriteLine($"  Tong thanh vien: {members.Count}  (Nguoi: {humanCount} | Bot: {botCount})");
                Console.WriteLine($"  Dang online    : {onlineCount}");
                Console.WriteLine($"  Tong kenh text: {guild.TextChannels.Count}");
                Console.WriteLine($"  Tong kenh voice: {guild.VoiceChannels.Count}");
                Console.WriteLine($"  So vai tro     : {guild.Roles.Count}");
                Console.WriteLine($"  Boost Level   : {guild.PremiumTier} (Boosts: {guild.PremiumSubscriptionCount})");
                Console.WriteLine();

                if (members.Count > 0)
                {
                    Console.WriteLine("  Danh sach thanh vien:");
                    var sortedMembers = members
                        .OrderByDescending(m => m.Status != UserStatus.Offline)
                        .ThenBy(m => m.Username, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var member in sortedMembers)
                    {
                        var statusIcon = GetStatusIcon(member.Status);
                        var botTag = member.IsBot ? " [BOT]" : "";
                        var joinDate = member.JoinedAt.HasValue ? $" (Join: {member.JoinedAt.Value:dd/MM/yy})" : "";
                        var activities = member.Activities?.ToList();
                        var actStr = (activities != null && activities.Count > 0)
                            ? $" - Dang {string.Join(", ", activities.Select(a => $"{a.Type} {a.Name}"))}"
                            : "";

                        Console.WriteLine($"    {statusIcon} {member.Username}#{member.Discriminator} (ID: {member.Id}){botTag}{joinDate}{actStr}");
                    }
                    Console.WriteLine();
                }
            }

            Console.WriteLine("========================================");
            Console.WriteLine("  TONG KET");
            Console.WriteLine($"  Tong Guild        : {guilds.Count}");
            Console.WriteLine($"  Tong thanh vien   : {totalUsers}  (Nguoi: {totalUsers - totalBots} | Bot: {totalBots})");
            Console.WriteLine($"  Tong dang online  : {totalOnline}");
            Console.WriteLine("========================================");
            Console.WriteLine();
        }

        private static string GetStatusIcon(UserStatus status)
        {
            return status switch
            {
                UserStatus.Online => "[ON]",
                UserStatus.Idle => "[IL]",
                UserStatus.AFK => "[AF]",
                UserStatus.DoNotDisturb => "[DN]",
                UserStatus.Invisible => "[IV]",
                UserStatus.Offline => "[--]",
                _ => "[??]"
            };
        }

        private async Task CleanupAsync()
        {
            try
            {
                if (_client.LoginState == LoginState.LoggedIn)
                {
                    await _client.StopAsync();
                    await _client.LogoutAsync();
                }
            }
            catch
            {
            }
        }
    }
}
