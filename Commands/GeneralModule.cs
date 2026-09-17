using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Clara_bot.Commands
{
    public class GeneralModule : ModuleBase<ICommandContext>
    {
        // Lưu trạng thái trang hiện tại cho mỗi guild
        private static readonly ConcurrentDictionary<ulong, int> _currentPages = new();
        private static readonly ConcurrentDictionary<ulong, ulong> _messageIds = new();

        // Danh sách tất cả các lệnh (tên, mô tả, yêu cầu quyền)
        private static readonly List<(string name, string description, bool adminOnly)> _allCommands = new()
        {
            // Lệnh chung
            ("/help (/heyclara)", "Hiển thị danh sách các lệnh hỗ trợ.", false),
            ("/info (/infoclara)", "Hiển thị thông tin về bot.", false),
            ("/ping (/pingclara)", "Kiểm tra CPU, RAM, độ trễ và tốc độ internet.", false),

            // Lệnh nhạc
            ("/play (/playclara)", "Phát nhạc từ YouTube vào kênh voice.", false),
            ("/pause (/pauseclara)", "Tạm dừng phát nhạc.", false),
            ("/resume (/resumeclara)", "Tiếp tục phát nhạc.", false),
            ("/stop (/stopclara)", "Dừng phát nhạc và rời khỏi kênh voice.", false),
            ("/prev (/prevclara)", "Lùi về bài trước (khi đang phát playlist).", false),
            ("/next (/nextclara)", "Chuyển sang bài tiếp theo (khi đang phát playlist).", false),
            ("/jump [n] (/jumpclara)", "Nhảy tới bài số n trong playlist đang phát.", false),
            ("/playlist (/showplaylistclara)", "Hiển thị danh sách bài đang phát theo trang.", false),
            ("/loop (/loopclara)", "Bật hoặc tắt chế độ lặp lại playlist.", false),
            ("/shuffle (/shufclara)", "Trộn ngẫu nhiên các bài trong playlist.", false),
            ("/speed (/speedclara)", "Điều chỉnh tốc độ phát nhạc (0.25x - 2.0x).", false),
            ("/queue (/queueclara)", "Bật/tắt chế độ hàng chờ. Khi bật, bài mới vào playlist.", false),
            ("/infoplay (/infoplayclara)", "Hiển thị thông tin bài hát đang phát.", false),

            // Lệnh roleplay
            ("/roleplay [on/off] (/roleplayclara)", "Bật/tắt chế độ roleplay với Clara.", false),

            // Lệnh quản lý (yêu cầu Admin/Owner)
            ("/kick @user [lý do]", "Đuổi thành viên khỏi server.", true),
            ("/ban @user [thời gian] [lý do]", "Cấm thành viên (hỗ trợ tạm thời: 1h, 2d, 1w).", true),
            ("/unban @user", "Gỡ cấm thành viên.", true),
            ("/warn @user [lý do]", "Gửi cảnh báo qua DM.", true),
            ("/role @user +/-Role", "Thêm/xóa vai trò cho thành viên.", true),
            ("/role all/bots/humans +/-Role", "Thêm/xóa role cho nhóm.", true),
            ("/clear [số]", "Xóa tin nhắn (mặc định 100, tối đa 1000).", true),
            ("/clear bots [số]", "Xóa tin nhắn của bot.", true),
            ("/clear @user [số]", "Xóa tin nhắn của người dùng cụ thể.", true),
            ("/vkick @user", "Đuổi người dùng khỏi kênh thoại.", true),
            ("/lock [kênh] [lý do]", "Khóa kênh, chặn @everyone gửi tin.", true),
            ("/unlock [kênh]", "Mở khóa kênh.", true),
            ("/slowmode [thời gian/off]", "Bật/tắt chế độ chậm trên kênh.", true),
        };

        private const int CommandsPerPage = 5;

        [Command("heyclara")]
        [Summary("Hiển thị danh sách các lệnh hỗ trợ.")]
        public async Task HeyClaraAsync()
        {
            var guildId = Context.Guild?.Id ?? Context.User.Id;
            _currentPages[guildId] = 0;

            var (embed, components) = BuildHelpPage(0, Context.User.Id);
            var message = await ReplyAsync(embed: embed, components: components);
            _messageIds[guildId] = message.Id;
        }

        [Command("help")]
        [Summary("Hiển thị danh sách các lệnh hỗ trợ.")]
        public async Task HelpAsync()
        {
            await HeyClaraAsync();
        }

        private static (Embed embed, MessageComponent components) BuildHelpPage(int page, ulong userId)
        {
            var totalPages = (int)Math.Ceiling(_allCommands.Count / (double)CommandsPerPage);
            if (page < 0) page = 0;
            if (page >= totalPages) page = totalPages - 1;

            var commandsOnPage = _allCommands
                .Skip(page * CommandsPerPage)
                .Take(CommandsPerPage)
                .ToList();

            var embed = new EmbedBuilder()
                .WithTitle("📋 Danh sách lệnh - Clara Bot")
                .WithDescription($"Trang **{page + 1}/{totalPages}** | Tổng số: {_allCommands.Count} lệnh\n\n`🔒` = Chỉ Admin/Owner")
                .WithColor(Color.Blue)
                .WithCurrentTimestamp();

            foreach (var (name, description, adminOnly) in commandsOnPage)
            {
                var prefix = adminOnly ? "🔒 " : "";
                embed.AddField(prefix + name, description, false);
            }

            var components = new ComponentBuilder()
                .WithButton("⏮️ Đầu", $"help:first:{userId}:{page}", ButtonStyle.Secondary, disabled: page == 0)
                .WithButton("◀️ Trước", $"help:prev:{userId}:{page}", ButtonStyle.Primary, disabled: page == 0)
                .WithButton("▶️ Tiếp", $"help:next:{userId}:{page}", ButtonStyle.Primary, disabled: page >= totalPages - 1)
                .WithButton("⏭️ Cuối", $"help:last:{userId}:{page}", ButtonStyle.Secondary, disabled: page >= totalPages - 1)
                .Build();

            return (embed.Build(), components);
        }

        // Xử lý button interactions cho help
        public static async Task<bool> TryHandleHelpComponentAsync(SocketMessageComponent component)
        {
            var customId = component.Data.CustomId;
            if (string.IsNullOrWhiteSpace(customId) || !customId.StartsWith("help:", StringComparison.Ordinal))
            {
                return false;
            }

            var parts = customId.Split(':');
            if (parts.Length != 4 ||
                !ulong.TryParse(parts[2], out var userId) ||
                !int.TryParse(parts[3], out var currentPage))
            {
                await component.DeferAsync().ConfigureAwait(false);
                return true;
            }

            // Kiểm tra quyền
            if (component.User.Id != userId)
            {
                await component.RespondAsync("❌ Bạn không thể điều khiển trang này.", ephemeral: true).ConfigureAwait(false);
                return true;
            }

            var totalPages = (int)Math.Ceiling(_allCommands.Count / (double)CommandsPerPage);
            var newPage = currentPage;

            switch (parts[1])
            {
                case "first":
                    newPage = 0;
                    break;
                case "prev":
                    newPage = Math.Max(0, currentPage - 1);
                    break;
                case "next":
                    newPage = Math.Min(totalPages - 1, currentPage + 1);
                    break;
                case "last":
                    newPage = totalPages - 1;
                    break;
                default:
                    await component.DeferAsync().ConfigureAwait(false);
                    return true;
            }

            var (embed, newComponents) = BuildHelpPage(newPage, userId);

            await component.UpdateAsync(msg =>
            {
                msg.Embed = embed;
                msg.Components = newComponents;
            }).ConfigureAwait(false);

            return true;
        }

        [Command("infoclara")]
        [Alias("info")]
        [Summary("Hiển thị thông tin về bot.")]
        public async Task InfoClaraAsync()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "Unknown";
            var embed = new EmbedBuilder()
                .WithTitle("Thông tin về Clara")
                .WithDescription("Clara là một bot Discord được tạo ra để hỗ trợ phát nhạc và quản lý server.")
                .AddField("Phiên bản", version)
                .AddField("Tác giả", "Kim Tuấn")
                .AddField("Tính năng", "🎵 Phát nhạc YouTube\n🔧 Quản lý server\n💬 Roleplay AI")
                .AddField("Follow GitHub", "https://github.com/chlorinebot")
                .AddField("Donate", "https://i.pinimg.com/736x/1c/5c/5b/1c5c5beddb559e0f2b85b2f354ef75e1.jpg")
                .WithColor(Color.Green)
                .WithCurrentTimestamp()
                .Build();

            await ReplyAsync(embed: embed);
        }
    }
}
