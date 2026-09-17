using Discord;
using Discord.Commands;
using System;
using System.Threading.Tasks;

namespace Clara_bot.Commands
{
    public class RequireAdminOrOwnerAttribute : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (context.Guild == null)
            {
                return PreconditionResult.FromError("Lệnh này chỉ có thể sử dụng trong server.");
            }

            if (context.User is not IGuildUser user)
            {
                return PreconditionResult.FromError("Không thể xác định người dùng.");
            }

            if (context.Guild.OwnerId == user.Id)
            {
                return PreconditionResult.FromSuccess();
            }

            if (user.GuildPermissions.Administrator)
            {
                return PreconditionResult.FromSuccess();
            }

            if (user.GuildPermissions.ManageGuild)
            {
                return PreconditionResult.FromSuccess();
            }

            return PreconditionResult.FromError("Chỉ chủ server, admin hoặc người có quyền quản lý server mới có thể sử dụng lệnh này.");
        }
    }
}