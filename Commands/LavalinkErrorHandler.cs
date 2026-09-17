using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clara_bot.Commands
{
    /// <summary>
    /// Handles Lavalink console errors and provides enhanced error responses.
    /// Maintains a cache of recent errors for better context.
    /// </summary>
    public class LavalinkErrorHandler
    {
        // Store recent errors per guild for context
        private readonly ConcurrentDictionary<ulong, Queue<ErrorContext>> _recentErrors = new();
        private const int MaxCachedErrorsPerGuild = 10;

        public sealed class ErrorContext
        {
            public required string TrackIdentifier { get; init; }
            public required string TrackTitle { get; init; }
            public required LavalinkLogAnalyzer.LavalinkError AnalyzedError { get; init; }
            public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
        }

        /// <summary>
        /// Handles an exception from track playback and analyzes the error.
        /// </summary>
        public async Task<LavalinkLogAnalyzer.LavalinkError> HandlePlaybackErrorAsync(
            ulong guildId, 
            string trackIdentifier, 
            string trackTitle, 
            Exception exception)
        {
            var logContent = exception.ToString();
            var analyzedError = LavalinkLogAnalyzer.AnalyzeLog(logContent);

            // Cache the error for future reference
            CacheError(guildId, trackIdentifier, trackTitle, analyzedError);

            return await Task.FromResult(analyzedError);
        }

        /// <summary>
        /// Handles an exception from track playback using error message text.
        /// </summary>
        public async Task<LavalinkLogAnalyzer.LavalinkError> HandlePlaybackErrorAsync(
            ulong guildId, 
            string trackIdentifier, 
            string trackTitle, 
            string errorMessage)
        {
            var analyzedError = LavalinkLogAnalyzer.AnalyzeLog(errorMessage);

            // Cache the error for future reference
            CacheError(guildId, trackIdentifier, trackTitle, analyzedError);

            return await Task.FromResult(analyzedError);
        }

        /// <summary>
        /// Analyzes recent errors for a guild to identify patterns.
        /// </summary>
        public ErrorPattern AnalyzeErrorPatterns(ulong guildId)
        {
            if (!_recentErrors.TryGetValue(guildId, out var errors) || errors.Count == 0)
            {
                return new ErrorPattern
                {
                    TotalErrors = 0,
                    Categories = new Dictionary<LavalinkLogAnalyzer.ErrorCategory, int>(),
                    MostCommonCategory = LavalinkLogAnalyzer.ErrorCategory.None,
                    Recommendation = "No errors recorded yet."
                };
            }

            var categoryCounts = new Dictionary<LavalinkLogAnalyzer.ErrorCategory, int>();
            lock (errors)
            {
                foreach (var error in errors)
                {
                    if (!categoryCounts.ContainsKey(error.AnalyzedError.Category))
                    {
                        categoryCounts[error.AnalyzedError.Category] = 0;
                    }
                    categoryCounts[error.AnalyzedError.Category]++;
                }
            }

            var mostCommon = categoryCounts.OrderByDescending(x => x.Value).FirstOrDefault();

            return new ErrorPattern
            {
                TotalErrors = errors.Count,
                Categories = categoryCounts,
                MostCommonCategory = mostCommon.Key,
                Recommendation = GenerateRecommendation(categoryCounts)
            };
        }

        /// <summary>
        /// Gets error history for a guild.
        /// </summary>
        public IReadOnlyList<ErrorContext> GetErrorHistory(ulong guildId)
        {
            if (_recentErrors.TryGetValue(guildId, out var errors))
            {
                lock (errors)
                {
                    return errors.ToList().AsReadOnly();
                }
            }

            return Array.Empty<ErrorContext>();
        }

        /// <summary>
        /// Clears error history for a guild.
        /// </summary>
        public void ClearErrorHistory(ulong guildId)
        {
            _recentErrors.TryRemove(guildId, out _);
        }

        private void CacheError(ulong guildId, string trackIdentifier, string trackTitle, 
            LavalinkLogAnalyzer.LavalinkError analyzedError)
        {
            var errorQueue = _recentErrors.GetOrAdd(guildId, _ => new Queue<ErrorContext>());

            lock (errorQueue)
            {
                errorQueue.Enqueue(new ErrorContext
                {
                    TrackIdentifier = trackIdentifier,
                    TrackTitle = trackTitle,
                    AnalyzedError = analyzedError
                });

                // Keep only the most recent errors
                while (errorQueue.Count > MaxCachedErrorsPerGuild)
                {
                    errorQueue.Dequeue();
                }
            }
        }

        private string GenerateRecommendation(Dictionary<LavalinkLogAnalyzer.ErrorCategory, int> categoryCounts)
        {
            if (categoryCounts.Count == 0)
            {
                return "No specific recommendation available.";
            }

            var (mostCommonCategory, count) = categoryCounts.OrderByDescending(x => x.Value).First();
            var percentage = (count * 100) / categoryCounts.Values.Sum();

            return mostCommonCategory switch
            {
                LavalinkLogAnalyzer.ErrorCategory.AgeRestricted => 
                    $"🔧 {percentage}% of errors are age-restricted videos. Configure YouTube plugin credentials in Lavalink.",

                LavalinkLogAnalyzer.ErrorCategory.LoginRequired => 
                    $"🔧 {percentage}% of errors require authentication. Ensure videos are publicly accessible or update Lavalink YouTube plugin.",

                LavalinkLogAnalyzer.ErrorCategory.ContentUnavailable => 
                    $"🔧 {percentage}% of videos are unavailable. This is normal for removed or copyright-struck content.",

                LavalinkLogAnalyzer.ErrorCategory.NetworkError => 
                    $"🔧 {percentage}% of errors are network-related. Check Lavalink's connection to YouTube.",

                LavalinkLogAnalyzer.ErrorCategory.HttpError => 
                    $"🔧 {percentage}% of errors are HTTP errors. YouTube may be blocking requests; check rate limiting.",

                LavalinkLogAnalyzer.ErrorCategory.YoutubePluginError => 
                    $"🔧 {percentage}% of errors are YouTube plugin issues. Reconfigure or reinstall the YouTube plugin.",

                _ => 
                    $"🔧 Most errors ({percentage}%) are: {mostCommonCategory}. Review Lavalink logs for more details."
            };
        }

        public sealed class ErrorPattern
        {
            public required int TotalErrors { get; init; }
            public required Dictionary<LavalinkLogAnalyzer.ErrorCategory, int> Categories { get; init; }
            public required LavalinkLogAnalyzer.ErrorCategory MostCommonCategory { get; init; }
            public required string Recommendation { get; init; }
        }
    }
}
