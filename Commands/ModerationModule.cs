using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Clara_bot.Commands
{
    [RequireAdminOrOwner]
    public class ModerationModule : ModuleBase<ICommandContext>
    {
        private SocketGuild Guild => (SocketGuild)Context.Guild;
        // Lưu CancellationTokenSource cho mỗi channel để có thể dừng clear
        private static readonly ConcurrentDictionary<ulong, CancellationTokenSource> _clearOperations = new();
        private TimeSpan? ParseBanDuration(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var match = Regex.Match(input, @"^(\d+)(m|h|d|w|mo|y)$", RegexOptions.IgnoreCase);
            if (!match.Success)
                return null;

            if (!int.TryParse(match.Groups[1].Value, out int value) || value <= 0)
                return null;

            string unit = match.Groups[2].Value.ToLower();
            return unit switch
            {
                "m" => TimeSpan.FromMinutes(value),
                "h" => TimeSpan.FromHours(value),
                "d" => TimeSpan.FromDays(value),
                "w" => TimeSpan.FromDays(value * 7),
                "mo" => TimeSpan.FromDays(value * 30),
                "y" => TimeSpan.FromDays(value * 365),
                _ => null
            };
        }

        private (TimeSpan? duration, string? reason) ParseBanArgs(string? args)
        {
            if (string.IsNullOrWhiteSpace(args))
                return (null, null);

            var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return (null, null);

            // Kiểm tra phần đầu tiên có phải là thời gian không
            var duration = ParseBanDuration(parts[0]);
            if (duration.HasValue)
            {
                // Phần còn lại là lý do
                var reason = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : null;
                return (duration, reason);
            }
            else
            {
                // Toàn bộ là lý do
                return (null, args);
            }
        }

        private (SocketGuildUser? user, ulong? userId, string? debugInfo) ParseUserFromInputWithDebug(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return (null, null, "Input rỗng");

            var debugInfo = $"Input nhận: `{input}`\n";
            var guildUsers = Guild.Users;
            debugInfo += $"Số thành viên trong server: {guildUsers.Count}\n";
            SocketGuildUser? user = null;
            ulong? parsedUserId = null;

            // Nếu input là IUser (từ slash command User type), trả về ngay
            if (input.StartsWith("Discord.WebSocket.SocketGlobalUser") || 
                input.StartsWith("Discord.WebSocket.SocketGuildUser") ||
                input.StartsWith("Discord.Rest.RestUser") ||
                input.StartsWith("Discord.IUser") ||
                input.StartsWith("Discord.SocketUser"))
            {
                // Input là đối tượng user, lấy ID từ cuối string (format: "Username#1234 (ID)")
                var match = System.Text.RegularExpressions.Regex.Match(input, @"\((\d+)\)$");
                if (match.Success && ulong.TryParse(match.Groups[1].Value, out ulong userId))
                {
                    debugInfo += $"Đã lấy ID từ IUser object: {userId}\n";
                    user = Guild.GetUser(userId);
                    if (user != null)
                        return (user, user.Id, debugInfo + "Tìm thấy user từ IUser object!");
                }
            }

            // Try parse as mention <@ID> hoặc <@!ID>
            if (MentionUtils.TryParseUser(input, out ulong mentionId))
            {
                debugInfo += $"Đã parse mention thành ID: {mentionId}\n";
                parsedUserId = mentionId;
                user = Guild.GetUser(mentionId);
                if (user != null)
                    return (user, user.Id, debugInfo + "Tìm thấy user từ mention!");
                debugInfo += "Không tìm thấy user với ID này trong server\n";
            }

            // Try parse as plain ID
            if (ulong.TryParse(input.Trim(), out ulong id))
            {
                debugInfo += $"Đã parse thành ID: {id}\n";
                parsedUserId = id;
                user = Guild.GetUser(id);
                if (user != null)
                    return (user, user.Id, debugInfo + "Tìm thấy user từ ID!");
                debugInfo += "Không tìm thấy user với ID này trong server\n";
            }

            // Try find by username#discriminator or username
            
            // Try exact username#discriminator
            user = guildUsers.FirstOrDefault(u => 
                string.Equals($"{u.Username}#{u.Discriminator}", input, StringComparison.OrdinalIgnoreCase));
            if (user != null)
                return (user, user.Id, debugInfo + "Tìm thấy user từ Username#Discriminator!");

            // Try username (partial match)
            var usernameMatches = guildUsers.Where(u => 
                u.Username.Contains(input, StringComparison.OrdinalIgnoreCase)).Take(5).ToList();
            if (usernameMatches.Any())
            {
                if (usernameMatches.Count == 1)
                {
                    user = usernameMatches.First();
                    return (user, user.Id, debugInfo + "Tìm thấy user từ username!");
                }
                debugInfo += $"Tìm thấy {usernameMatches.Count} user khớp username: {string.Join(", ", usernameMatches.Select(u => u.Username))}\n";
            }

            // Try nickname (partial match)
            var nicknameMatches = guildUsers.Where(u => 
                !string.IsNullOrEmpty(u.Nickname) && 
                u.Nickname.Contains(input, StringComparison.OrdinalIgnoreCase)).Take(5).ToList();
            if (nicknameMatches.Any())
            {
                if (nicknameMatches.Count == 1)
                {
                    user = nicknameMatches.First();
                    return (user, user.Id, debugInfo + "Tìm thấy user từ nickname!");
                }
                debugInfo += $"Tìm thấy {nicknameMatches.Count} user khớp nickname: {string.Join(", ", nicknameMatches.Select(u => $"{u.Username}({u.Nickname})"))}\n";
            }

            // Hiển thị một số user trong server để debug
            var sampleUsers = guildUsers.Take(10).Select(u => $"{u.Username}#{u.Discriminator}({u.Id})");
            debugInfo += $"\n10 user đầu tiên trong server: {string.Join(", ", sampleUsers)}";
            
            // Nếu đã parse được ID (dù không tìm thấy user), trả về ID
            if (parsedUserId.HasValue)
            {
                return (null, parsedUserId.Value, debugInfo);
            }
            
            return (null, null, debugInfo);
        }

        private SocketGuildUser? ParseUserFromInput(string input)
        {
            var (user, _, _) = ParseUserFromInputWithDebug(input);
            return user;
        }

        private async Task<bool> KickUserByIdAsync(ulong userId, string? reason = null)
        {
            // Kiểm tra xem bot có quyền kick không
            var currentUser = Guild.CurrentUser;
            if (!currentUser.GuildPermissions.KickMembers)
            {
                await ReplyAsync("❌ Tôi không có quyền đuổi thành viên.");
                return false;
            }

            // Kiểm tra xem người dùng có phải chủ server không
            if (userId == Context.Guild.OwnerId)
            {
                await ReplyAsync("❌ Không thể đuổi chủ server.");
                return false;
            }

            // Không thể kiểm tra hierarchy vì không có user object
            // Có thể thử lấy user từ cache để kiểm tra hierarchy
            var user = Guild.GetUser(userId);
            if (user != null)
            {
                // Kiểm tra role hierarchy với người thực thi
                var executor = Context.User as SocketGuildUser;
                if (executor != null && user.Hierarchy >= executor.Hierarchy)
                {
                    await ReplyAsync("❌ Bạn không thể đuổi người có role cao hơn hoặc bằng bạn.");
                    return false;
                }

                // Kiểm tra bot hierarchy
                if (user.Hierarchy >= currentUser.Hierarchy)
                {
                    await ReplyAsync("❌ Tôi không thể đuổi người có role cao hơn hoặc bằng tôi.");
                    return false;
                }

                // Có user object, kick bình thường
                await user.KickAsync(reason);
                
                // Gửi DM nếu có thể
                try
                {
                    var dmEmbed = new EmbedBuilder()
                        .WithTitle($"Bạn đã bị đuổi khỏi server {Context.Guild.Name}")
                        .AddField("Lý do", string.IsNullOrWhiteSpace(reason) ? "Không có lý do" : reason)
                        .AddField("Người đuổi", $"{Context.User.Username} ({Context.User.Id})")
                        .WithColor(Color.Red)
                        .WithCurrentTimestamp()
                        .Build();
                    await user.SendMessageAsync(embed: dmEmbed);
                }
                catch { }

                return true;
            }
            else
            {
                // User không có trong cache, thử kick bằng ID
                // Discord.NET không hỗ trợ kick bằng ID trực tiếp
                // Cần lấy user từ client (global) hoặc thông báo lỗi
                var globalUser = ((DiscordSocketClient)Context.Client).GetUser(userId);
                if (globalUser != null)
                {
                    // Vẫn không thể kick vì không có trong server
                    await ReplyAsync($"❌ Người dùng {globalUser.Username}#{globalUser.Discriminator} ({userId}) không có trong server hoặc đã rời.");
                    return false;
                }
                else
                {
                    await ReplyAsync($"❌ Không tìm thấy người dùng với ID {userId} trong hệ thống Discord.");
                    return false;
                }
            }
        }

        private async Task<bool> BanUserByIdAsync(ulong userId, string? reason = null, TimeSpan? duration = null)
        {
            // Kiểm tra xem bot có quyền ban không
            var currentUser = Guild.CurrentUser;
            if (!currentUser.GuildPermissions.BanMembers)
            {
                await ReplyAsync("❌ Tôi không có quyền cấm thành viên.");
                return false;
            }

            // Kiểm tra xem người dùng có phải chủ server không
            if (userId == Context.Guild.OwnerId)
            {
                await ReplyAsync("❌ Không thể cấm chủ server.");
                return false;
            }

            // Không thể kiểm tra hierarchy vì không có user object
            // Có thể thử lấy user từ cache để kiểm tra hierarchy
            var user = Guild.GetUser(userId);
            if (user != null)
            {
                // Kiểm tra role hierarchy với người thực thi
                var executor = Context.User as SocketGuildUser;
                if (executor != null && user.Hierarchy >= executor.Hierarchy)
                {
                    await ReplyAsync("❌ Bạn không thể cấm người có role cao hơn hoặc bằng bạn.");
                    return false;
                }

                // Kiểm tra bot hierarchy
                if (user.Hierarchy >= currentUser.Hierarchy)
                {
                    await ReplyAsync("❌ Tôi không thể cấm người có role cao hơn hoặc bằng tôi.");
                    return false;
                }
            }

            // Thực hiện ban bằng ID (có thể ban cả khi user không trong cache)
            try
            {
                int pruneDays = 0; // Số ngày xóa tin nhắn (0-7)
                string banReason = reason ?? "Không có lý do";
                if (duration.HasValue)
                {
                    banReason = $"[TẠM THỜI: {FormatDuration(duration.Value)}] {banReason}";
                }

                await Context.Guild.AddBanAsync(userId, pruneDays, banReason);
                
                // Gửi DM nếu có thể (chỉ khi user có trong cache)
                if (user != null)
                {
                    try
                    {
                        var dmEmbed = new EmbedBuilder()
                            .WithTitle($"Bạn đã bị cấm khỏi server {Context.Guild.Name}")
                            .AddField("Thời hạn", duration.HasValue ? FormatDuration(duration.Value) : "Vĩnh viễn")
                            .AddField("Lý do", string.IsNullOrWhiteSpace(reason) ? "Không có lý do" : reason)
                            .AddField("Người cấm", $"{Context.User.Username} ({Context.User.Id})")
                            .WithColor(Color.Red)
                            .WithCurrentTimestamp()
                            .Build();
                        await user.SendMessageAsync(embed: dmEmbed);
                    }
                    catch { }
                }

                return true;
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Lỗi khi cấm người dùng: {ex.Message}");
                return false;
            }
        }

        [Command("kick")]
        [Summary("Đuổi người dùng khỏi server bằng mention, ID hoặc tên.")]
        [RequireBotPermission(GuildPermission.KickMembers)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task KickAsync(
            [Summary("Người dùng cần đuổi (mention, ID hoặc tên)")] string userInput,
            [Summary("Lý do đuổi")][Remainder] string? reason = null)
        {
            // Tìm người dùng bằng input
            var (user, userId, debugInfo) = ParseUserFromInputWithDebug(userInput);
            if (user == null)
            {
                // Không có user object, nhưng có thể có userId đã parse được
                if (userId.HasValue)
                {
                    bool success = await KickUserByIdAsync(userId.Value, reason);
                    if (success)
                    {
                        // Gửi embed thành công (vì KickUserByIdAsync không gửi reply thành công)
                        var embed = new EmbedBuilder()
                            .WithTitle("✅ Đã đuổi thành viên")
                            .AddField("Người bị đuổi", $"User ID: {userId.Value}")
                            .AddField("Lý do", string.IsNullOrWhiteSpace(reason) ? "Không có lý do" : reason)
                            .AddField("Người đuổi", Context.User.Mention)
                            .WithColor(Color.Orange)
                            .WithCurrentTimestamp()
                            .Build();
                        await ReplyAsync(embed: embed);
                    }
                    // Nếu không thành công, KickUserByIdAsync đã gửi thông báo lỗi
                    return;
                }
                
                // Không có cả user lẫn userId
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Không tìm thấy người dùng")
                    .WithDescription($"{debugInfo}\n\nVui lòng sử dụng: mention (@user), ID hoặc tên chính xác.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();
                await ReplyAsync(embed: errorEmbed);
                return;
            }

            // Kiểm tra xem bot có quyền kick không
            var currentUser = Guild.CurrentUser;
            if (!currentUser.GuildPermissions.KickMembers)
            {
                await ReplyAsync("❌ Tôi không có quyền đuổi thành viên.");
                return;
            }

            // Kiểm tra xem người dùng có thể bị kick không
            if (user.Id == Context.Guild.OwnerId)
            {
                await ReplyAsync("❌ Không thể đuổi chủ server.");
                return;
            }

            // Kiểm tra role hierarchy
            var executor = Context.User as SocketGuildUser;
            if (executor != null && user.Hierarchy >= executor.Hierarchy)
            {
                await ReplyAsync("❌ Bạn không thể đuổi người có role cao hơn hoặc bằng bạn.");
                return;
            }

            // Kiểm tra bot hierarchy
            if (user.Hierarchy >= currentUser.Hierarchy)
            {
                await ReplyAsync("❌ Tôi không thể đuổi người có role cao hơn hoặc bằng tôi.");
                return;
            }

            try
            {
                // Gửi thông báo cho người bị kick (nếu có thể)
                try
                {
                    var dmEmbed = new EmbedBuilder()
                        .WithTitle($"Bạn đã bị đuổi khỏi server {Context.Guild.Name}")
                        .AddField("Lý do", string.IsNullOrWhiteSpace(reason) ? "Không có lý do" : reason)
                        .AddField("Người đuổi", $"{Context.User.Username} ({Context.User.Id})")
                        .WithColor(Color.Red)
                        .WithCurrentTimestamp()
                        .Build();

                    await user.SendMessageAsync(embed: dmEmbed);
                }
                catch
                {
                    // Không thể gửi tin nhắn DM, bỏ qua
                }

                // Thực hiện kick
                await user.KickAsync(reason);

                // Tạo embed thông báo thành công
                var embed = new EmbedBuilder()
                    .WithTitle("✅ Đã đuổi thành viên")
                    .AddField("Người bị đuổi", $"{user.Username} ({user.Id})")
                    .AddField("Lý do", string.IsNullOrWhiteSpace(reason) ? "Không có lý do" : reason)
                    .AddField("Người đuổi", Context.User.Mention)
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp()
                    .Build();

                await ReplyAsync(embed: embed);
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Lỗi khi đuổi người dùng: {ex.Message}");
            }
        }

        [Command("ban")]
        [Summary("Cấm người dùng khỏi server bằng mention, ID hoặc tên (có thể tạm thời).")]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task BanAsync(
            [Summary("Người dùng cần cấm (mention, ID hoặc tên)")] string userInput,
            [Summary("Thời gian và lý do (ví dụ: 1h spamming)")][Remainder] string? args = null)
        {
            // Tìm người dùng bằng input
            var (user, userId, debugInfo) = ParseUserFromInputWithDebug(userInput);
            if (user == null)
            {
                // Không có user object, nhưng có thể có userId đã parse được
                if (userId.HasValue)
                {
                    // Parse args để lấy duration và reason
                    var (banDuration, banReason) = ParseBanArgs(args);
                    bool success = await BanUserByIdAsync(userId.Value, banReason, banDuration);
                    if (success)
                    {
                        // Gửi embed thành công (vì BanUserByIdAsync không gửi reply thành công)
                        var embed = new EmbedBuilder()
                            .WithTitle("✅ Đã cấm thành viên")
                            .AddField("Người bị cấm", $"User ID: {userId.Value}")
                            .AddField("Thời hạn", banDuration.HasValue ? FormatDuration(banDuration.Value) : "Vĩnh viễn")
                            .AddField("Lý do", string.IsNullOrWhiteSpace(banReason) ? "Không có lý do" : banReason)
                            .AddField("Người cấm", Context.User.Mention)
                            .WithColor(Color.Red)
                            .WithCurrentTimestamp()
                            .Build();
                        await ReplyAsync(embed: embed);
                    }
                    // Nếu không thành công, BanUserByIdAsync đã gửi thông báo lỗi
                    return;
                }
                
                // Không có cả user lẫn userId
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Không tìm thấy người dùng")
                    .WithDescription($"{debugInfo}\n\nVui lòng sử dụng: mention (@user), ID hoặc tên chính xác.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();
                await ReplyAsync(embed: errorEmbed);
                return;
            }

            // Kiểm tra xem bot có quyền ban không
            var currentUser = Guild.CurrentUser;
            if (!currentUser.GuildPermissions.BanMembers)
            {
                await ReplyAsync("❌ Tôi không có quyền cấm thành viên.");
                return;
            }

            // Kiểm tra xem người dùng có thể bị ban không
            if (user.Id == Context.Guild.OwnerId)
            {
                await ReplyAsync("❌ Không thể cấm chủ server.");
                return;
            }

            // Kiểm tra role hierarchy
            var executor = Context.User as SocketGuildUser;
            if (executor != null && user.Hierarchy >= executor.Hierarchy)
            {
                await ReplyAsync("❌ Bạn không thể cấm người có role cao hơn hoặc bằng bạn.");
                return;
            }

            // Kiểm tra bot hierarchy
            if (user.Hierarchy >= currentUser.Hierarchy)
            {
                await ReplyAsync("❌ Tôi không thể cấm người có role cao hơn hoặc bằng tôi.");
                return;
            }

            // Parse args để lấy thời gian và lý do
            var (duration, reason) = ParseBanArgs(args);

            try
            {
                // Gửi thông báo cho người bị ban (nếu có thể)
                try
                {
                    var dmEmbed = new EmbedBuilder()
                        .WithTitle($"Bạn đã bị cấm khỏi server {Context.Guild.Name}")
                        .AddField("Thời hạn", duration.HasValue ? FormatDuration(duration.Value) : "Vĩnh viễn")
                        .AddField("Lý do", string.IsNullOrWhiteSpace(reason) ? "Không có lý do" : reason)
                        .AddField("Người cấm", $"{Context.User.Username} ({Context.User.Id})")
                        .WithColor(Color.Red)
                        .WithCurrentTimestamp()
                        .Build();

                    await user.SendMessageAsync(embed: dmEmbed);
                }
                catch
                {
                    // Không thể gửi tin nhắn DM, bỏ qua
                }

                // Thực hiện ban
                int pruneDays = 0; // Số ngày xóa tin nhắn (0-7)
                string banReason = reason ?? "Không có lý do";
                if (duration.HasValue)
                {
                    // Thêm thông tin thời hạn vào lý do
                    banReason = $"[TẠM THỜI: {FormatDuration(duration.Value)}] {(string.IsNullOrWhiteSpace(reason) ? "Không có lý do" : reason)}";
                }

                await Context.Guild.AddBanAsync(user, pruneDays, banReason);

                // Tạo embed thông báo thành công
                var embed = new EmbedBuilder()
                    .WithTitle("✅ Đã cấm thành viên")
                    .AddField("Người bị cấm", $"{user.Username} ({user.Id})")
                    .AddField("Thời hạn", duration.HasValue ? FormatDuration(duration.Value) : "Vĩnh viễn")
                    .AddField("Lý do", string.IsNullOrWhiteSpace(reason) ? "Không có lý do" : reason)
                    .AddField("Người cấm", Context.User.Mention)
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await ReplyAsync(embed: embed);
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Lỗi khi cấm người dùng: {ex.Message}");
            }
        }

        [Command("unban")]
        [Summary("Gỡ cấm người dùng khỏi server bằng mention, ID hoặc tên.")]
        [RequireBotPermission(GuildPermission.BanMembers)]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task UnbanAsync(
            [Summary("Người dùng cần gỡ cấm (mention, ID hoặc tên)")] string userInput,
            [Summary("Lý do gỡ cấm")][Remainder] string? reason = null)
        {
            // Parse user ID từ input
            ulong? userId = null;
            string? userName = null;

            // Try parse as mention
            if (MentionUtils.TryParseUser(userInput, out ulong mentionId))
            {
                userId = mentionId;
            }
            // Try parse as plain ID
            else if (ulong.TryParse(userInput, out ulong id))
            {
                userId = id;
            }
            else
            {
                // Tìm trong danh sách ban (IAsyncEnumerable cần flatten)
                var banList = await Context.Guild.GetBansAsync().FlattenAsync();
                var ban = banList.FirstOrDefault(b =>
                    b.User.Username.Contains(userInput, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(b.User.GlobalName) && b.User.GlobalName.Contains(userInput, StringComparison.OrdinalIgnoreCase)));

                if (ban != null)
                {
                    userId = ban.User.Id;
                    userName = ban.User.Username;
                }
            }

            if (!userId.HasValue)
            {
                await ReplyAsync("❌ Không tìm thấy người dùng trong danh sách bị cấm. Vui lòng sử dụng mention (@user), ID hoặc tên chính xác.");
                return;
            }

            // Kiểm tra xem bot có quyền ban không (unban cũng cần quyền BanMembers)
            var currentUser = Guild.CurrentUser;
            if (!currentUser.GuildPermissions.BanMembers)
            {
                await ReplyAsync("❌ Tôi không có quyền gỡ cấm thành viên.");
                return;
            }

            try
            {
                // Thực hiện unban
                await Context.Guild.RemoveBanAsync(userId.Value);

                // Tạo embed thông báo thành công
                var embed = new EmbedBuilder()
                    .WithTitle("✅ Đã gỡ cấm thành viên")
                    .AddField("Người được gỡ cấm", userName ?? $"User ID: {userId.Value}")
                    .AddField("Lý do", string.IsNullOrWhiteSpace(reason) ? "Không có lý do" : reason)
                    .AddField("Người gỡ cấm", Context.User.Mention)
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp()
                    .Build();

                await ReplyAsync(embed: embed);
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Lỗi khi gỡ cấm người dùng: {ex.Message}");
            }
        }

        [Command("role")]
        [Summary("Thêm hoặc xóa vai trò cho thành viên (all, bots, humans).")]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        [RequireUserPermission(GuildPermission.ManageRoles)]
        public async Task RoleAsync(
            [Summary("Người dùng: @user, ID, tên, all, bots, hoặc humans")] string target,
            [Summary("Vai trò: (+/-)RoleName hoặc (+/-)RoleName1, RoleName2")][Remainder] string roleInput)
        {
            if (string.IsNullOrWhiteSpace(roleInput))
            {
                await ReplyAsync("❌ Vui lòng cung cấp tên vai trò. Ví dụ: `Admin` hoặc `+Admin, Member` hoặc `-Admin`");
                return;
            }

            // Parse các vai trò và thao tác (+/-)
            var roleOperations = ParseRoleOperations(roleInput);
            if (!roleOperations.Any())
            {
                await ReplyAsync("❌ Không thể parse vai trò từ input.");
                return;
            }

            // Xác định danh sách target users
            List<SocketGuildUser> targetUsers = new();
            string targetDescription;

            var targetLower = target.ToLowerInvariant();
            if (targetLower == "all")
            {
                targetUsers = Guild.Users.ToList();
                targetDescription = "tất cả thành viên";
            }
            else if (targetLower == "bots")
            {
                targetUsers = Guild.Users.Where(u => u.IsBot).ToList();
                targetDescription = "tất cả bot";
            }
            else if (targetLower == "humans")
            {
                targetUsers = Guild.Users.Where(u => !u.IsBot).ToList();
                targetDescription = "tất cả người dùng";
            }
            else
            {
                // Tìm user đơn lẻ
                var (user, _, debugInfo) = ParseUserFromInputWithDebug(target);
                if (user == null)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithTitle("❌ Không tìm thấy người dùng")
                        .WithDescription($"{debugInfo}\n\nVui lòng sử dụng: mention (@user), ID, tên, hoặc `all`/`bots`/`humans`.")
                        .WithColor(Color.Red)
                        .Build();
                    await ReplyAsync(embed: errorEmbed);
                    return;
                }
                targetUsers = new List<SocketGuildUser> { user };
                targetDescription = user.Mention;
            }

            if (!targetUsers.Any())
            {
                await ReplyAsync("❌ Không tìm thấy thành viên nào phù hợp.");
                return;
            }

            // Thực hiện thêm/xóa vai trò
            var addedRoles = new List<string>();
            var removedRoles = new List<string>();
            var failedRoles = new List<string>();
            var botUser = Guild.CurrentUser;

            foreach (var (roleNameFromOps, shouldAdd) in roleOperations)
            {
                // Tìm vai trò: theo mention <@&ID>, theo ID, hoặc theo tên
                IRole? role = null;
                var cleanRoleInput = roleNameFromOps.Trim();

                // Thử parse role mention <@&ID>
                if (cleanRoleInput.StartsWith("<@&") && cleanRoleInput.EndsWith(">"))
                {
                    var idStr = cleanRoleInput[3..^1]; // Bỏ <@& và >
                    if (ulong.TryParse(idStr, out var roleId))
                    {
                        role = Guild.GetRole(roleId);
                    }
                }
                // Thử parse role ID trực tiếp
                else if (ulong.TryParse(cleanRoleInput, out var roleId))
                {
                    role = Guild.GetRole(roleId);
                }
                // Tìm theo tên
                else
                {
                    role = Guild.Roles.FirstOrDefault(r =>
                        r.Name.Equals(cleanRoleInput, StringComparison.OrdinalIgnoreCase));
                }

                if (role == null)
                {
                    failedRoles.Add($"❌ `{roleNameFromOps}` (không tìm thấy)");
                    continue;
                }

                // Kiểm tra bot có quyền quản lý vai trò này không
                if (role.Position >= botUser.Hierarchy)
                {
                    failedRoles.Add($"❌ `{role.Name}` (vai trò cao hơn bot)");
                    continue;
                }

                int successCount = 0;
                foreach (var user in targetUsers)
                {
                    try
                    {
                        if (shouldAdd)
                        {
                            if (!user.Roles.Any(r => r.Id == role.Id))
                            {
                                await user.AddRoleAsync(role);
                                successCount++;
                            }
                            else
                            {
                                successCount++; // Đã có role này
                            }
                        }
                        else
                        {
                            if (user.Roles.Any(r => r.Id == role.Id))
                            {
                                await user.RemoveRoleAsync(role);
                                successCount++;
                            }
                            else
                            {
                                successCount++; // Không có role này
                            }
                        }
                    }
                    catch
                    {
                        // Bỏ qua lỗi cho từng user
                    }
                }

                if (successCount > 0)
                {
                    if (shouldAdd)
                        addedRoles.Add($"✅ `{role.Name}` ({successCount}/{targetUsers.Count})");
                    else
                        removedRoles.Add($"✅ `{role.Name}` ({successCount}/{targetUsers.Count})");
                }
                else
                {
                    failedRoles.Add($"❌ `{role.Name}` (không thể thực hiện)");
                }
            }

            // Tạo embed kết quả
            var resultEmbed = new EmbedBuilder()
                .WithTitle("📝 Kết quả thay đổi vai trò")
                .AddField("Đối tượng", targetDescription, true)
                .AddField("Tổng số", $"{targetUsers.Count} thành viên", true)
                .WithColor(Color.Blue)
                .WithCurrentTimestamp();

            if (addedRoles.Any())
                resultEmbed.AddField("Đã thêm", string.Join("\n", addedRoles));
            if (removedRoles.Any())
                resultEmbed.AddField("Đã xóa", string.Join("\n", removedRoles));
            if (failedRoles.Any())
                resultEmbed.AddField("Thất bại", string.Join("\n", failedRoles));

            await ReplyAsync(embed: resultEmbed.Build());
        }

        private List<(string roleName, bool shouldAdd)> ParseRoleOperations(string input)
        {
            var result = new List<(string, bool)>();
            var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                bool shouldAdd = true;
                var roleName = trimmed;

                // Kiểm tra prefix + hoặc -
                if (trimmed.StartsWith('+'))
                {
                    shouldAdd = true;
                    roleName = trimmed.Substring(1).Trim();
                }
                else if (trimmed.StartsWith('-'))
                {
                    shouldAdd = false;
                    roleName = trimmed.Substring(1).Trim();
                }

                if (!string.IsNullOrWhiteSpace(roleName))
                {
                    result.Add((roleName, shouldAdd));
                }
            }

            return result;
        }

        [Command("warn")]
        [Summary("Gửi cảnh báo cho thành viên hoặc toàn bộ vai trò qua DM.")]
        [RequireBotPermission(GuildPermission.KickMembers)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task WarnAsync(
            [Summary("Người dùng cần cảnh báo (mention, ID, tên hoặc &roleID cho vai trò)")][Remainder] string? input = null)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                await ReplyAsync("❌ Vui lòng cung cấp người dùng (mention/ID/tên) hoặc vai trò (&roleID).");
                return;
            }

            // Tách lý do từ input
            string? reason = null;
            var parts = input.Split(new[] { ' ' }, 2);
            var userOrRoleInput = parts[0].Trim();
            if (parts.Length > 1)
                reason = parts[1].Trim();

            // Kiểm tra nếu là role (bắt đầu bằng &)
            if (userOrRoleInput.StartsWith("&"))
            {
                await WarnRoleAsync(userOrRoleInput, reason);
                return;
            }

            // Tìm người dùng
            var (user, _, debugInfo) = ParseUserFromInputWithDebug(userOrRoleInput);
            if (user == null)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Không tìm thấy người dùng")
                    .WithDescription($"{debugInfo}\n\nVui lòng sử dụng: mention (@user), ID, tên chính xác, hoặc &roleID cho vai trò.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();
                await ReplyAsync(embed: errorEmbed);
                return;
            }

            await WarnUserAsync(user, reason);
        }

        private async Task WarnUserAsync(SocketGuildUser user, string? reason)
        {
            // Không thể tự cảnh báo chính mình
            if (user.Id == Context.User.Id)
            {
                await ReplyAsync("❌ Bạn không thể tự cảnh báo chính mình.");
                return;
            }

            // Không thể cảnh báo chủ server
            if (user.Id == Context.Guild.OwnerId)
            {
                await ReplyAsync("❌ Không thể cảnh báo chủ server.");
                return;
            }

            try
            {
                bool dmSent = false;

                // Gửi DM cảnh báo cho người dùng
                try
                {
                    var warnEmbed = new EmbedBuilder()
                        .WithTitle("⚠️ Cảnh báo từ Ban Quản Trị")
                        .WithDescription($"Bạn đã nhận được một cảnh báo từ server **{Context.Guild.Name}**.")
                        .AddField("Lý do", string.IsNullOrWhiteSpace(reason) ? "Không có lý do cụ thể" : reason)
                        .AddField("Người cảnh báo", $"{Context.User.Username} ({Context.User.Mention})")
                        .AddField("Thời gian", $"<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:F>")
                        .AddField("Lưu ý", "Vi phạm nhiều lần có thể dẫn đến các hình thức xử lý nghiêm trọng hơn (mute, kick, ban).")
                        .WithColor(Color.Orange)
                        .WithCurrentTimestamp()
                        .Build();

                    await user.SendMessageAsync(embed: warnEmbed);
                    dmSent = true;
                }
                catch
                {
                    dmSent = false;
                }

                // Tạo embed xác nhận
                var confirmEmbed = new EmbedBuilder()
                    .WithTitle("⚠️ Đã gửi cảnh báo")
                    .AddField("Người bị cảnh báo", user.Mention, true)
                    .AddField("ID", user.Id, true)
                    .AddField("Lý do", string.IsNullOrWhiteSpace(reason) ? "Không có lý do" : reason, true)
                    .AddField("Trạng thái DM", dmSent ? "✅ Đã gửi" : "❌ Không thể gửi (DM bị tắt)", true)
                    .AddField("Người cảnh báo", Context.User.Mention, true)
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp()
                    .Build();

                await ReplyAsync(embed: confirmEmbed);

                if (!dmSent)
                {
                    await ReplyAsync($"⚠️ Không thể gửi DM cho {user.Mention}. Họ có thể đã tắt DM hoặc chặn bot.");
                }
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Lỗi khi gửi cảnh báo: {ex.Message}");
            }
        }

        private async Task WarnRoleAsync(string roleInput, string? reason)
        {
            // Parse role ID (bỏ & đầu tiên)
            var roleIdStr = roleInput.Substring(1);
            if (!ulong.TryParse(roleIdStr, out ulong roleId))
            {
                await ReplyAsync("❌ ID vai trò không hợp lệ.");
                return;
            }

            var role = Context.Guild.GetRole(roleId);
            if (role == null)
            {
                await ReplyAsync("❌ Không tìm thấy vai trò với ID này.");
                return;
            }

            // Lấy tất cả thành viên có role này
            var socketGuild = Context.Guild as SocketGuild;
            if (socketGuild == null)
            {
                await ReplyAsync("❌ Không thể truy cập danh sách thành viên.");
                return;
            }

            // Tải toàn bộ danh sách thành viên (không chỉ cached users)
            await socketGuild.DownloadUsersAsync();
            var membersWithRole = socketGuild.Users.Where(u => u.Roles.Any(r => r.Id == roleId)).ToList();

            if (!membersWithRole.Any())
            {
                await ReplyAsync($"❌ Không có thành viên nào có vai trò {role.Mention}.");
                return;
            }

            await ReplyAsync($"⏳ Đang gửi cảnh báo cho {membersWithRole.Count} thành viên có vai trò {role.Mention}...");

            int successCount = 0;
            int failCount = 0;
            var failedUsers = new List<string>();

            foreach (var user in membersWithRole)
            {
                // Bỏ qua chính mình và chủ server
                if (user.Id == Context.User.Id || user.Id == Context.Guild.OwnerId)
                    continue;

                try
                {
                    var warnEmbed = new EmbedBuilder()
                        .WithTitle("⚠️ Cảnh báo từ Ban Quản Trị")
                        .WithDescription($"Bạn đã nhận được một cảnh báo từ server **{Context.Guild.Name}** vì thuộc vai trò {role.Name}.")
                        .AddField("Lý do", string.IsNullOrWhiteSpace(reason) ? "Không có lý do cụ thể" : reason)
                        .AddField("Người cảnh báo", $"{Context.User.Username} ({Context.User.Mention})")
                        .AddField("Thời gian", $"<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:F>")
                        .AddField("Lưu ý", "Vi phạm nhiều lần có thể dẫn đến các hình thức xử lý nghiêm trọng hơn (mute, kick, ban).")
                        .WithColor(Color.Orange)
                        .WithCurrentTimestamp()
                        .Build();

                    await user.SendMessageAsync(embed: warnEmbed);
                    successCount++;
                }
                catch
                {
                    failCount++;
                    failedUsers.Add(user.Mention);
                }

                // Delay nhỏ để tránh rate limit
                await Task.Delay(100);
            }

            // Tạo embed xác nhận
            var confirmEmbed = new EmbedBuilder()
                .WithTitle("⚠️ Đã gửi cảnh báo cho vai trò")
                .AddField("Vai trò", role.Mention, true)
                .AddField("Số thành viên", membersWithRole.Count, true)
                .AddField("Thành công", successCount, true)
                .AddField("Thất bại", failCount, true)
                .AddField("Lý do", string.IsNullOrWhiteSpace(reason) ? "Không có lý do" : reason, false)
                .AddField("Người cảnh báo", Context.User.Mention, true)
                .WithColor(Color.Orange)
                .WithCurrentTimestamp()
                .Build();

            await ReplyAsync(embed: confirmEmbed);

            if (failCount > 0 && failedUsers.Any())
            {
                var failList = string.Join(", ", failedUsers.Take(10));
                if (failedUsers.Count > 10)
                    failList += $" và {failedUsers.Count - 10} người khác...";
                await ReplyAsync($"⚠️ Không thể gửi DM cho: {failList}");
            }
        }

        [Command("clear")]
        [Summary("Xóa tin nhắn trong kênh (tối đa 1000, trong vòng 14 ngày).")]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task ClearAsync(
            [Summary("Số lượng tin nhắn (mặc định 100, tối đa 1000)")] int count = 100)
        {
            await ClearMessagesAsync(count);
        }

        [Command("clear")]
        [Summary("Xóa tin nhắn của bot hoặc người dùng cụ thể.")]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task ClearAsync(
            [Summary("'bots' hoặc mention/ID/tên người dùng")] string target,
            [Summary("Số lượng tin nhắn (mặc định 100, tối đa 1000)")] int count = 100)
        {
            var targetLower = target.ToLowerInvariant();

            if (targetLower == "bots" || targetLower == "bot")
            {
                await ClearBotMessagesAsync(count);
            }
            else
            {
                // Tìm user
                var (user, _, debugInfo) = ParseUserFromInputWithDebug(target);
                if (user == null)
                {
                    await ReplyAsync("❌ Không tìm thấy người dùng. Vui lòng sử dụng `bots` hoặc mention/ID/tên user.");
                    return;
                }
                await ClearUserMessagesAsync(user, count);
            }
        }

        private async Task ClearMessagesAsync(int count)
        {
            if (count < 1 || count > 1000)
            {
                await ReplyAsync("❌ Số lượng tin nhắn phải từ 1 đến 1000.");
                return;
            }

            // Gửi tin nhắn loading
            var loadingMsg = await ReplyAsync("⏳ Đang xóa tin nhắn...");

            // Tạo CancellationTokenSource để có thể dừng
            var cts = new CancellationTokenSource();
            _clearOperations[Context.Channel.Id] = cts;

            try
            {
                // Kiểm tra xem channel có phải là kênh văn bản không
                // Hỗ trợ cả ITextChannel (prefix commands) và DeferredEditChannel (slash commands)
                ITextChannel? textChannel = null;
                
                // Thử cast sang ITextChannel (cho prefix commands)
                textChannel = Context.Channel as ITextChannel;
                
                // Nếu không được, thử lấy từ guild bằng channel ID (cho slash commands)
                if (textChannel == null)
                {
                    textChannel = Guild.GetChannel(Context.Channel.Id) as ITextChannel;
                }
                
                if (textChannel == null)
                {
                    await loadingMsg.ModifyAsync(m => m.Content = "❌ Lệnh này chỉ hoạt động trong kênh văn bản.");
                    return;
                }

                // Lấy tin nhắn để xóa (loại trừ tin nhắn lệnh và loading)
                var messages = await textChannel.GetMessagesAsync(count + 2).FlattenAsync();
                var messagesToDelete = messages.Where(m => m.Id != loadingMsg.Id && (Context.Message == null || m.Id != Context.Message.Id)).Take(count).ToList();

                if (!messagesToDelete.Any())
                {
                    await loadingMsg.ModifyAsync(m => m.Content = "❌ Không tìm thấy tin nhắn nào để xóa.");
                    return;
                }

                // Kiểm tra tin nhắn cũ hơn 14 ngày
                var twoWeeksAgo = DateTimeOffset.UtcNow.AddDays(-14);
                var oldMessages = messagesToDelete.Where(m => m.CreatedAt < twoWeeksAgo).ToList();
                var deletableMessages = messagesToDelete.Where(m => m.CreatedAt >= twoWeeksAgo).ToList();

                int deletedCount = 0;
                int oldDeletedCount = 0;

                // Xóa tin nhắn trong 14 ngày (bulk delete)
                if (deletableMessages.Any())
                {
                    if (deletableMessages.Count == 1)
                    {
                        await deletableMessages.First().DeleteAsync();
                        deletedCount = 1;
                    }
                    else
                    {
                        await textChannel.DeleteMessagesAsync(deletableMessages);
                        deletedCount = deletableMessages.Count;
                    }
                }

                // Xóa tin nhắn cũ hơn 14 ngày (từng cái một)
                if (oldMessages.Any())
                {
                    await loadingMsg.ModifyAsync(m => m.Content = $"⏳ Đang xóa {oldMessages.Count} tin nhắn cũ hơn 14 ngày (từng cái một, có thể chậm)...\n💡 Dùng `/stopclear` để dừng");

                    foreach (var msg in oldMessages)
                    {
                        // Kiểm tra nếu có yêu cầu dừng
                        if (cts.Token.IsCancellationRequested)
                        {
                            await loadingMsg.ModifyAsync(m => m.Content = $"⏹️ Đã dừng. Đã xóa {oldDeletedCount}/{oldMessages.Count} tin nhắn cũ.");
                            break;
                        }

                        try
                        {
                            await msg.DeleteAsync();
                            oldDeletedCount++;
                            await Task.Delay(1050, cts.Token); // Delay có thể bị hủy
                        }
                        catch (OperationCanceledException) { break; }
                        catch { /* Bỏ qua lỗi cho từng tin nhắn */ }
                    }
                }

                // Xóa tin nhắn lệnh (nếu có - slash commands không có Message)
                if (Context.Message != null)
                {
                    try { await Context.Message.DeleteAsync(); } catch { }
                }

                // Thông báo kết quả
                var embed = new EmbedBuilder()
                    .WithTitle("🗑️ Đã xóa tin nhắn")
                    .WithDescription($"Đã xóa **{deletedCount + oldDeletedCount}** tin nhắn.")
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp();

                if (deletedCount > 0)
                    embed.AddField("✅ Tin nhắn gần đây", $"{deletedCount} tin nhắn (trong 14 ngày)", true);
                if (oldDeletedCount > 0)
                    embed.AddField("✅ Tin nhắn cũ", $"{oldDeletedCount} tin nhắn (>14 ngày)", true);
                if (oldMessages.Any() && oldDeletedCount < oldMessages.Count)
                    embed.AddField("⚠️ Không thể xóa", $"{oldMessages.Count - oldDeletedCount} tin nhắn", true);

                await loadingMsg.ModifyAsync(m =>
                {
                    m.Content = null;
                    m.Embed = embed.Build();
                });
            }
            catch (Exception ex)
            {
                await loadingMsg.ModifyAsync(m => m.Content = $"❌ Lỗi khi xóa tin nhắn: {ex.Message}");
            }
            finally
            {
                // Xóa khỏi dictionary khi xong
                _clearOperations.TryRemove(Context.Channel.Id, out _);
            }
        }

        private async Task ClearBotMessagesAsync(int count)
        {
            if (count < 1 || count > 1000)
            {
                await ReplyAsync("❌ Số lượng tin nhắn phải từ 1 đến 1000.");
                return;
            }

            // Gửi tin nhắn loading
            var loadingMsg = await ReplyAsync("⏳ Đang xóa tin nhắn bot...");

            // Tạo CancellationTokenSource để có thể dừng
            var cts = new CancellationTokenSource();
            _clearOperations[Context.Channel.Id] = cts;

            try
            {
                // Kiểm tra xem channel có phải là kênh văn bản không
                ITextChannel? textChannel = Context.Channel as ITextChannel;
                
                if (textChannel == null)
                {
                    textChannel = Guild.GetChannel(Context.Channel.Id) as ITextChannel;
                }
                
                if (textChannel == null)
                {
                    await loadingMsg.ModifyAsync(m => m.Content = "❌ Lệnh này chỉ hoạt động trong kênh văn bản.");
                    return;
                }

                // Lấy nhiều tin nhắn hơn để lọc bot (loại trừ loading message)
                var messages = await textChannel.GetMessagesAsync(Math.Min(count * 3 + 1, 1000)).FlattenAsync();
                var botMessages = messages
                    .Where(m => m.Author.IsBot && m.Id != Context.Message.Id && m.Id != loadingMsg.Id)
                    .Take(count)
                    .ToList();

                if (!botMessages.Any())
                {
                    await loadingMsg.ModifyAsync(m => m.Content = "❌ Không tìm thấy tin nhắn bot nào để xóa.");
                    return;
                }

                // Kiểm tra tin nhắn cũ
                var twoWeeksAgo = DateTimeOffset.UtcNow.AddDays(-14);
                var oldMessages = botMessages.Where(m => m.CreatedAt < twoWeeksAgo).ToList();
                var deletableMessages = botMessages.Where(m => m.CreatedAt >= twoWeeksAgo).ToList();

                int deletedCount = 0;
                int oldDeletedCount = 0;

                // Xóa tin nhắn trong 14 ngày
                if (deletableMessages.Any())
                {
                    if (deletableMessages.Count == 1)
                    {
                        await deletableMessages.First().DeleteAsync();
                        deletedCount = 1;
                    }
                    else
                    {
                        await textChannel.DeleteMessagesAsync(deletableMessages);
                        deletedCount = deletableMessages.Count;
                    }
                }

                // Xóa tin nhắn cũ hơn 14 ngày
                if (oldMessages.Any())
                {
                    await loadingMsg.ModifyAsync(m => m.Content = $"⏳ Đang xóa {oldMessages.Count} tin nhắn bot cũ (>14 ngày)...\n💡 Dùng `/stopclear` để dừng");

                    foreach (var msg in oldMessages)
                    {
                        // Kiểm tra nếu có yêu cầu dừng
                        if (cts.Token.IsCancellationRequested)
                        {
                            await loadingMsg.ModifyAsync(m => m.Content = $"⏹️ Đã dừng. Đã xóa {oldDeletedCount}/{oldMessages.Count} tin nhắn bot cũ.");
                            break;
                        }

                        try
                        {
                            await msg.DeleteAsync();
                            oldDeletedCount++;
                            await Task.Delay(1050, cts.Token);
                        }
                        catch (OperationCanceledException) { break; }
                        catch { }
                    }
                }

                // Xóa tin nhắn lệnh (nếu có - slash commands không có Message)
                if (Context.Message != null)
                {
                    try { await Context.Message.DeleteAsync(); } catch { }
                }

                var embed = new EmbedBuilder()
                    .WithTitle("🗑️ Đã xóa tin nhắn bot")
                    .WithDescription($"Đã xóa **{deletedCount + oldDeletedCount}** tin nhắn bot.")
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp();

                if (deletedCount > 0)
                    embed.AddField("✅ Gần đây", $"{deletedCount} tin nhắn", true);
                if (oldDeletedCount > 0)
                    embed.AddField("✅ Cũ >14 ngày", $"{oldDeletedCount} tin nhắn", true);
                if (oldMessages.Any() && oldDeletedCount < oldMessages.Count)
                    embed.AddField("⚠️ Không xóa được", $"{oldMessages.Count - oldDeletedCount} tin nhắn", true);

                await loadingMsg.ModifyAsync(m =>
                {
                    m.Content = null;
                    m.Embed = embed.Build();
                });
            }
            catch (Exception ex)
            {
                await loadingMsg.ModifyAsync(m => m.Content = $"❌ Lỗi khi xóa tin nhắn: {ex.Message}");
            }
            finally
            {
                _clearOperations.TryRemove(Context.Channel.Id, out _);
            }
        }

        private async Task ClearUserMessagesAsync(SocketGuildUser user, int count)
        {
            if (count < 1 || count > 1000)
            {
                await ReplyAsync("❌ Số lượng tin nhắn phải từ 1 đến 1000.");
                return;
            }

            // Gửi tin nhắn loading
            var loadingMsg = await ReplyAsync($"⏳ Đang xóa tin nhắn của {user.Username}...");

            // Tạo CancellationTokenSource để có thể dừng
            var cts = new CancellationTokenSource();
            _clearOperations[Context.Channel.Id] = cts;

            try
            {
                // Kiểm tra xem channel có phải là kênh văn bản không
                ITextChannel? textChannel = Context.Channel as ITextChannel;
                
                if (textChannel == null)
                {
                    textChannel = Guild.GetChannel(Context.Channel.Id) as ITextChannel;
                }
                
                if (textChannel == null)
                {
                    await loadingMsg.ModifyAsync(m => m.Content = "❌ Lệnh này chỉ hoạt động trong kênh văn bản.");
                    return;
                }

                // Lấy nhiều tin nhắn hơn để lọc user (loại trừ loading message)
                var messages = await textChannel.GetMessagesAsync(Math.Min(count * 3 + 1, 1000)).FlattenAsync();
                var userMessages = messages
                    .Where(m => m.Author.Id == user.Id && m.Id != loadingMsg.Id && (Context.Message == null || m.Id != Context.Message.Id))
                    .Take(count)
                    .ToList();

                if (!userMessages.Any())
                {
                    await loadingMsg.ModifyAsync(m => m.Content = $"❌ Không tìm thấy tin nhắn nào của {user.Mention} để xóa.");
                    return;
                }

                // Kiểm tra tin nhắn cũ
                var twoWeeksAgo = DateTimeOffset.UtcNow.AddDays(-14);
                var oldMessages = userMessages.Where(m => m.CreatedAt < twoWeeksAgo).ToList();
                var deletableMessages = userMessages.Where(m => m.CreatedAt >= twoWeeksAgo).ToList();

                int deletedCount = 0;
                int oldDeletedCount = 0;

                // Xóa tin nhắn trong 14 ngày
                if (deletableMessages.Any())
                {
                    if (deletableMessages.Count == 1)
                    {
                        await deletableMessages.First().DeleteAsync();
                        deletedCount = 1;
                    }
                    else
                    {
                        await textChannel.DeleteMessagesAsync(deletableMessages);
                        deletedCount = deletableMessages.Count;
                    }
                }

                // Xóa tin nhắn cũ hơn 14 ngày
                if (oldMessages.Any())
                {
                    await loadingMsg.ModifyAsync(m => m.Content = $"⏳ Đang xóa {oldMessages.Count} tin nhắn cũ của {user.Username} (>14 ngày)...\n💡 Dùng `/stopclear` để dừng");

                    foreach (var msg in oldMessages)
                    {
                        // Kiểm tra nếu có yêu cầu dừng
                        if (cts.Token.IsCancellationRequested)
                        {
                            await loadingMsg.ModifyAsync(m => m.Content = $"⏹️ Đã dừng. Đã xóa {oldDeletedCount}/{oldMessages.Count} tin nhắn cũ của {user.Username}.");
                            break;
                        }

                        try
                        {
                            await msg.DeleteAsync();
                            oldDeletedCount++;
                            await Task.Delay(1050, cts.Token);
                        }
                        catch (OperationCanceledException) { break; }
                        catch { }
                    }
                }

                // Xóa tin nhắn lệnh (nếu có - slash commands không có Message)
                if (Context.Message != null)
                {
                    try { await Context.Message.DeleteAsync(); } catch { }
                }

                var embed = new EmbedBuilder()
                    .WithTitle("🗑️ Đã xóa tin nhắn")
                    .WithDescription($"Đã xóa **{deletedCount + oldDeletedCount}** tin nhắn của {user.Mention}.")
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp();

                if (deletedCount > 0)
                    embed.AddField("✅ Gần đây", $"{deletedCount} tin nhắn", true);
                if (oldDeletedCount > 0)
                    embed.AddField("✅ Cũ >14 ngày", $"{oldDeletedCount} tin nhắn", true);
                if (oldMessages.Any() && oldDeletedCount < oldMessages.Count)
                    embed.AddField("⚠️ Không xóa được", $"{oldMessages.Count - oldDeletedCount} tin nhắn", true);

                await loadingMsg.ModifyAsync(m =>
                {
                    m.Content = null;
                    m.Embed = embed.Build();
                });
            }
            catch (Exception ex)
            {
                await loadingMsg.ModifyAsync(m => m.Content = $"❌ Lỗi khi xóa tin nhắn: {ex.Message}");
            }
            finally
            {
                _clearOperations.TryRemove(Context.Channel.Id, out _);
            }
        }

        [Command("stopclear")]
        [Summary("Dừng quá trình xóa tin nhắn đang chạy trong kênh này.")]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task StopClearAsync()
        {
            if (_clearOperations.TryGetValue(Context.Channel.Id, out var cts))
            {
                cts.Cancel();
                await ReplyAsync("⏹️ Đã gửi yêu cầu dừng xóa tin nhắn. Vui lòng đợi...");
            }
            else
            {
                await ReplyAsync("❌ Không có quá trình xóa tin nhắn nào đang chạy trong kênh này.");
            }
        }

        [Command("lock")]
        [Summary("Khóa kênh, vô hiệu hóa @everyone gửi tin nhắn.")]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task LockAsync(
            [Summary("Kênh cần khóa (mặc định: kênh hiện tại)")] ITextChannel? channel = null,
            [Summary("Lý do khóa")][Remainder] string? reason = null)
        {
            ITextChannel? targetChannel;
            if (channel != null)
            {
                targetChannel = channel;
            }
            else
            {
                targetChannel = Context.Channel as ITextChannel;
                
                if (targetChannel == null)
                {
                    targetChannel = Guild.GetChannel(Context.Channel.Id) as ITextChannel;
                }
                
                if (targetChannel == null)
                {
                    await ReplyAsync("❌ Không tìm thấy kênh để khóa.");
                    return;
                }
            }

            try
            {
                // Lấy @everyone role của server
                var everyoneRole = Guild.EveryoneRole;

                // Lấy permission hiện tại
                var perms = targetChannel.GetPermissionOverwrite(everyoneRole);

                // Tạo OverwritePermissions mới với SendMessages = Deny
                var newPerms = perms.GetValueOrDefault();
                newPerms = newPerms.Modify(sendMessages: PermValue.Deny);

                // Áp dụng permission mới
                await targetChannel.AddPermissionOverwriteAsync(everyoneRole, newPerms);

                var embed = new EmbedBuilder()
                    .WithTitle("🔒 Kênh đã bị khóa")
                    .WithDescription($"Kênh {targetChannel.Mention} đã bị khóa.\n@everyone không thể gửi tin nhắn.")
                    .AddField("Người khóa", Context.User.Mention, true)
                    .AddField("Lý do", string.IsNullOrWhiteSpace(reason) ? "Không có lý do" : reason, true)
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await ReplyAsync(embed: embed);
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Lỗi khi khóa kênh: {ex.Message}");
            }
        }

        [Command("unlock")]
        [Summary("Mở khóa kênh, cho phép @everyone gửi tin nhắn.")]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task UnlockAsync(
            [Summary("Kênh cần mở khóa (mặc định: kênh hiện tại)")] ITextChannel? channel = null)
        {
            ITextChannel? targetChannel;
            if (channel != null)
            {
                targetChannel = channel;
            }
            else
            {
                targetChannel = Context.Channel as ITextChannel;
                
                if (targetChannel == null)
                {
                    targetChannel = Guild.GetChannel(Context.Channel.Id) as ITextChannel;
                }
                
                if (targetChannel == null)
                {
                    await ReplyAsync("❌ Không tìm thấy kênh để mở khóa.");
                    return;
                }
            }

            try
            {
                // Lấy @everyone role của server
                var everyoneRole = Guild.EveryoneRole;

                // Lấy permission hiện tại
                var perms = targetChannel.GetPermissionOverwrite(everyoneRole);

                if (!perms.HasValue || perms.Value.SendMessages == PermValue.Inherit)
                {
                    await ReplyAsync($"✅ Kênh {targetChannel.Mention} đã được mở khóa (hoặc chưa bị khóa).");
                    return;
                }

                // Xóa permission override hoặc set về Inherit
                var newPerms = perms.Value.Modify(sendMessages: PermValue.Inherit);
                await targetChannel.AddPermissionOverwriteAsync(everyoneRole, newPerms);

                var embed = new EmbedBuilder()
                    .WithTitle("🔓 Kênh đã được mở khóa")
                    .WithDescription($"Kênh {targetChannel.Mention} đã được mở khóa.\n@everyone có thể gửi tin nhắn trở lại.")
                    .AddField("Người mở khóa", Context.User.Mention, true)
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp()
                    .Build();

                await ReplyAsync(embed: embed);
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Lỗi khi mở khóa kênh: {ex.Message}");
            }
        }

        [Command("vkick")]
        [Alias("voicekick")]
        [Summary("Đuổi người dùng khỏi kênh thoại.")]
        [RequireBotPermission(GuildPermission.MoveMembers)]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task VoiceKickAsync(
            [Summary("Người dùng cần đuổi khỏi voice")] string userInput)
        {
            // Tìm người dùng
            var (user, _, debugInfo) = ParseUserFromInputWithDebug(userInput);
            if (user == null)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Không tìm thấy người dùng")
                    .WithDescription($"{debugInfo}\n\nVui lòng sử dụng: mention (@user), ID hoặc tên chính xác.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();
                await ReplyAsync(embed: errorEmbed);
                return;
            }

            // Kiểm tra xem user có trong voice channel không
            if (user.VoiceChannel == null)
            {
                await ReplyAsync($"❌ {user.Mention} không ở trong kênh thoại nào.");
                return;
            }

            // Kiểm tra quyền của bot
            var botUser = Guild.CurrentUser;
            if (!botUser.GuildPermissions.MoveMembers)
            {
                await ReplyAsync("❌ Tôi không có quyền di chuyển thành viên.");
                return;
            }

            try
            {
                var voiceChannel = user.VoiceChannel;

                // Đuổi user khỏi voice channel bằng cách chuyển sang null
                await user.ModifyAsync(props => props.Channel = null);

                var embed = new EmbedBuilder()
                    .WithTitle("🔊 Đã đuổi khỏi kênh thoại")
                    .WithDescription($"{user.Mention} đã bị đuổi khỏi {voiceChannel.Mention}.")
                    .AddField("Người bị đuổi", $"{user.Username} ({user.Mention})", true)
                    .AddField("Kênh thoại", voiceChannel.Name, true)
                    .AddField("Người thực hiện", Context.User.Mention, true)
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp()
                    .Build();

                await ReplyAsync(embed: embed);
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Lỗi khi đuổi khỏi kênh thoại: {ex.Message}");
            }
        }

        [Command("slowmode")]
        [Summary("Bật hoặc tắt chế độ chậm (slowmode) trên kênh.")]
        [RequireBotPermission(GuildPermission.ManageChannels)]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task SlowModeAsync(
            [Summary("Thời gian (s/m/h) hoặc 'off' để tắt")][Remainder] string? timeInput = null)
        {
            ITextChannel? channel = Context.Channel as ITextChannel;
            
            if (channel == null)
            {
                channel = Guild.GetChannel(Context.Channel.Id) as ITextChannel;
            }
            
            if (channel == null)
            {
                await ReplyAsync("❌ Lệnh này chỉ hoạt động trong kênh văn bản.");
                return;
            }

            try
            {
                int slowModeSeconds = 0;

                // Nếu không có input hoặc "off" → tắt slowmode
                if (string.IsNullOrWhiteSpace(timeInput) || timeInput.ToLowerInvariant() == "off")
                {
                    slowModeSeconds = 0;
                }
                else
                {
                    // Parse thời gian
                    var parsed = ParseSlowModeTime(timeInput);
                    if (!parsed.HasValue)
                    {
                        await ReplyAsync("❌ Định dạng thời gian không hợp lệ.\nVí dụ: `5s`, `30m`, `1h`, `off`");
                        return;
                    }
                    slowModeSeconds = parsed.Value;
                }

                // Giới hạn tối đa 6 giờ (21600 giây) theo Discord
                if (slowModeSeconds > 21600)
                {
                    await ReplyAsync("❌ Thời gian tối đa là 6 giờ (21600 giây).");
                    return;
                }

                // Áp dụng slowmode
                await channel.ModifyAsync(props => props.SlowModeInterval = slowModeSeconds);

                if (slowModeSeconds == 0)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("🐢 Chế độ chậm đã tắt")
                        .WithDescription($"Slowmode đã được tắt trong {channel.Mention}.")
                        .AddField("Người thực hiện", Context.User.Mention, true)
                        .WithColor(Color.Green)
                        .WithCurrentTimestamp()
                        .Build();
                    await ReplyAsync(embed: embed);
                }
                else
                {
                    var timeDisplay = FormatDuration(TimeSpan.FromSeconds(slowModeSeconds));
                    var embed = new EmbedBuilder()
                        .WithTitle("🐢 Chế độ chậm đã bật")
                        .WithDescription($"Thành viên phải đợi **{timeDisplay}** giữa mỗi tin nhắn trong {channel.Mention}.")
                        .AddField("Thời gian", $"{slowModeSeconds} giây", true)
                        .AddField("Người bật", Context.User.Mention, true)
                        .WithColor(Color.Orange)
                        .WithCurrentTimestamp()
                        .Build();
                    await ReplyAsync(embed: embed);
                }
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Lỗi khi cài đặt slowmode: {ex.Message}");
            }
        }

        private int? ParseSlowModeTime(string input)
        {
            input = input.Trim().ToLowerInvariant();

            // Chỉ số (giây)
            if (int.TryParse(input, out int seconds))
            {
                return seconds;
            }

            // Parse với đơn vị
            var match = Regex.Match(input, @"^(\d+)([smh])$");
            if (!match.Success) return null;

            if (!int.TryParse(match.Groups[1].Value, out int value)) return null;
            string unit = match.Groups[2].Value;

            return unit switch
            {
                "s" => value,
                "m" => value * 60,
                "h" => value * 3600,
                _ => null
            };
        }

        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 365)
                return $"{duration.TotalDays / 365:0.##} năm";
            if (duration.TotalDays >= 30)
                return $"{duration.TotalDays / 30:0.##} tháng";
            if (duration.TotalDays >= 7)
                return $"{duration.TotalDays / 7:0.##} tuần";
            if (duration.TotalDays >= 1)
                return $"{duration.TotalDays:0.##} ngày";
            if (duration.TotalHours >= 1)
                return $"{duration.TotalHours:0.##} giờ";
            if (duration.TotalMinutes >= 1)
                return $"{duration.TotalMinutes:0.##} phút";
            
            return $"{duration.TotalSeconds:0.##} giây";
        }
    }
}