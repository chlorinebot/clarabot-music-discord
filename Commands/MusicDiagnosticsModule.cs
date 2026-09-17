using Discord.Commands;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Clara_bot.Commands
{
    /// <summary>
    /// Diagnostic commands for analyzing and troubleshooting music playback errors.
    /// </summary>
    public class MusicDiagnosticsModule : ModuleBase<ICommandContext>
    {
        private readonly LavalinkErrorHandler _errorHandler;

        public MusicDiagnosticsModule(LavalinkErrorHandler errorHandler)
        {
            _errorHandler = errorHandler;
        }

        [Command("musicerrors")]
        [Alias("musicdiag", "musicdiagnostics")]
        [Summary("Shows error history and recommendations for the current server.")]
        [RequireContext(ContextType.Guild)]
        public async Task ShowErrorHistoryAsync()
        {
            var guildId = Context.Guild.Id;
            var errorHistory = _errorHandler.GetErrorHistory(guildId);

            if (errorHistory.Count == 0)
            {
                await ReplyAsync("✅ No errors recorded for this server. Music playback is running smoothly!");
                return;
            }

            // Analyze patterns
            var pattern = _errorHandler.AnalyzeErrorPatterns(guildId);

            // Build error history display
            var embed = new EmbedBuilder()
                .WithTitle("🎵 Music Error Diagnostics")
                .WithColor(Color.Gold)
                .WithTimestamp(DateTimeOffset.UtcNow);

            // Summary section
            embed.AddField("Summary", 
                $"**Total Errors:** {pattern.TotalErrors}\n" +
                $"**Most Common:** {pattern.MostCommonCategory}", 
                false);

            // Error breakdown
            if (pattern.Categories.Any())
            {
                var categoryBreakdown = string.Join("\n", pattern.Categories
                    .OrderByDescending(x => x.Value)
                    .Select(x => $"• {x.Key}: {x.Value} error(s)"));

                embed.AddField("Error Breakdown", categoryBreakdown, false);
            }

            // Recommendation
            embed.AddField("💡 Recommendation", pattern.Recommendation, false);

            // Recent errors (last 5)
            var recentErrors = errorHistory.TakeLast(5).Reverse();
            if (recentErrors.Any())
            {
                var recentErrorsList = string.Join("\n", recentErrors.Select(e =>
                    $"**{e.TrackTitle}** - {e.AnalyzedError.Category}"));

                embed.AddField("Recent Errors (Last 5)", recentErrorsList, false);
            }

            // Footer with helpful info
            embed.WithFooter("Run /musicclearerrors to reset error history");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("musicclearerrors")]
        [Alias("clearmusicerrors")]
        [Summary("Clears the error history for the current server.")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task ClearErrorHistoryAsync()
        {
            var guildId = Context.Guild.Id;
            _errorHandler.ClearErrorHistory(guildId);
            await ReplyAsync("✅ Error history cleared for this server.");
        }

        [Command("lavalinkhelp")]
        [Alias("musichelp")]
        [Summary("Shows help information about common Lavalink and music errors.")]
        public async Task ShowLavalinkHelpAsync()
        {
            var embed = new EmbedBuilder()
                .WithTitle("🎵 Lavalink & Music Bot Troubleshooting")
                .WithColor(Color.Blue)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithDescription("Common issues and solutions:");

            embed.AddField("⚠️ Age-Restricted Videos",
                "**Problem:** Videos marked as age-restricted won't play.\n" +
                "**Solution:** Configure YouTube plugin credentials:\n" +
                "1. Get `poToken` and `visitorData` from a working YouTube session\n" +
                "2. Add them to your Lavalink `application.yml`\n" +
                "3. Restart Lavalink",
                false);

            embed.AddField("⚠️ Login Required / Private Videos",
                "**Problem:** Videos require authentication.\n" +
                "**Solution:** Only public YouTube videos can be played. Use public playlists and unlisted videos instead.",
                false);

            embed.AddField("⚠️ Video Unavailable",
                "**Problem:** Video has been removed or is blocked.\n" +
                "**Solution:** The video is no longer available. This is normal for removed or copyright-struck content.",
                false);

            embed.AddField("⚠️ Network Errors",
                "**Problem:** Lavalink can't reach YouTube.\n" +
                "**Solution:** \n" +
                "1. Check your internet connection\n" +
                "2. Verify firewall allows outbound HTTPS\n" +
                "3. Check Lavalink logs for more details\n" +
                "4. Restart Lavalink if needed",
                false);

            embed.AddField("⚠️ Invalid Track / No Audio",
                "**Problem:** Track is invalid or has no audio.\n" +
                "**Solution:** The track may be corrupted. Try another video.",
                false);

            embed.AddField("📊 Check Your Server's Error Stats",
                "Run `/musicerrors` to see detailed error analysis and recommendations for your server.",
                false);

            embed.WithFooter("Need more help? Check Lavalink documentation or bot logs");

            await ReplyAsync(embed: embed.Build());
        }
    }
}
