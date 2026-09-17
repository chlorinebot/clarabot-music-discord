using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Clara_bot.Commands
{
    /// <summary>
    /// Analyzes Lavalink console logs to identify and categorize playback errors.
    /// </summary>
    public static class LavalinkLogAnalyzer
    {
        /// <summary>
        /// Represents a parsed error from Lavalink logs.
        /// </summary>
        public sealed class LavalinkError
        {
            public required ErrorCategory Category { get; init; }
            public required string Message { get; init; }
            public required string UserFriendlyMessage { get; init; }
            public string? SuggestedAction { get; init; }
            public DateTime AnalyzedAt { get; init; } = DateTime.UtcNow;
            public required string RawLogContent { get; init; }
        }

        public enum ErrorCategory
        {
            None,
            AgeRestricted,
            LoginRequired,
            ContentUnavailable,
            NetworkError,
            InvalidTrack,
            LavalinkServerError,
            HttpError,
            YoutubePluginError,
            PlaylistError,
            Unknown
        }

        // Regex patterns for common Lavalink errors
        private static readonly Dictionary<ErrorCategory, Regex[]> ErrorPatterns = new()
        {
            {
                ErrorCategory.AgeRestricted, new[]
                {
                    new Regex(@"age.?restrict", RegexOptions.IgnoreCase),
                    new Regex(@"age.?gate", RegexOptions.IgnoreCase),
                    new Regex(@"sign in to confirm your age", RegexOptions.IgnoreCase),
                    new Regex(@"confirm.?age", RegexOptions.IgnoreCase),
                }
            },
            {
                ErrorCategory.LoginRequired, new[]
                {
                    new Regex(@"requires.?login", RegexOptions.IgnoreCase),
                    new Regex(@"sign.?in", RegexOptions.IgnoreCase),
                    new Regex(@"authentication.?required", RegexOptions.IgnoreCase),
                    new Regex(@"account.?required", RegexOptions.IgnoreCase),
                }
            },
            {
                ErrorCategory.ContentUnavailable, new[]
                {
                    new Regex(@"unavailable", RegexOptions.IgnoreCase),
                    new Regex(@"not.?available", RegexOptions.IgnoreCase),
                    new Regex(@"video.?removed", RegexOptions.IgnoreCase),
                    new Regex(@"removed.?copyright", RegexOptions.IgnoreCase),
                    new Regex(@"copyright", RegexOptions.IgnoreCase),
                    new Regex(@"deleted", RegexOptions.IgnoreCase),
                }
            },
            {
                ErrorCategory.NetworkError, new[]
                {
                    new Regex(@"network", RegexOptions.IgnoreCase),
                    new Regex(@"timeout", RegexOptions.IgnoreCase),
                    new Regex(@"connection.?refused", RegexOptions.IgnoreCase),
                    new Regex(@"unreachable", RegexOptions.IgnoreCase),
                    new Regex(@"socket.?closed", RegexOptions.IgnoreCase),
                    new Regex(@"no.?route", RegexOptions.IgnoreCase),
                }
            },
            {
                ErrorCategory.InvalidTrack, new[]
                {
                    new Regex(@"invalid.?track", RegexOptions.IgnoreCase),
                    new Regex(@"track.?not.?found", RegexOptions.IgnoreCase),
                    new Regex(@"no.?audio", RegexOptions.IgnoreCase),
                    new Regex(@"audio.?not.?found", RegexOptions.IgnoreCase),
                }
            },
            {
                ErrorCategory.HttpError, new[]
                {
                    new Regex(@"http.?(?:40[0-9]|50[0-9])", RegexOptions.IgnoreCase),
                    new Regex(@"status.?code.?(?:40[0-9]|50[0-9])", RegexOptions.IgnoreCase),
                    new Regex(@"429", RegexOptions.IgnoreCase), // Rate limited
                }
            },
            {
                ErrorCategory.YoutubePluginError, new[]
                {
                    new Regex(@"youtube.*plugin", RegexOptions.IgnoreCase),
                    new Regex(@"poToken", RegexOptions.IgnoreCase),
                    new Regex(@"visitorData", RegexOptions.IgnoreCase),
                    new Regex(@"yt-dlp", RegexOptions.IgnoreCase),
                }
            },
            {
                ErrorCategory.PlaylistError, new[]
                {
                    new Regex(@"playlist.*failed", RegexOptions.IgnoreCase),
                    new Regex(@"all.?clients.?failed", RegexOptions.IgnoreCase),
                    new Regex(@"loadtype.*error", RegexOptions.IgnoreCase),
                }
            }
        };

        /// <summary>
        /// Analyzes Lavalink console log content to identify errors.
        /// </summary>
        /// <param name="logContent">The Lavalink console log content to analyze</param>
        /// <returns>Identified error information, or None if no error detected</returns>
        public static LavalinkError AnalyzeLog(string logContent)
        {
            if (string.IsNullOrWhiteSpace(logContent))
            {
                return new LavalinkError
                {
                    Category = ErrorCategory.None,
                    Message = "No log content provided",
                    UserFriendlyMessage = "Unable to analyze error",
                    RawLogContent = logContent ?? string.Empty
                };
            }

            // Check each error category
            foreach (var category in ErrorPatterns.Keys)
            {
                var patterns = ErrorPatterns[category];
                if (patterns.Any(pattern => pattern.IsMatch(logContent)))
                {
                    return CreateErrorResponse(category, logContent);
                }
            }

            // If no specific pattern matched, try to extract generic error message
            return CreateGenericErrorResponse(logContent);
        }

        /// <summary>
        /// Analyzes multiple log lines and returns the most critical error found.
        /// </summary>
        public static LavalinkError AnalyzeLogLines(params string[] logLines)
        {
            if (!logLines.Any() || logLines.All(string.IsNullOrWhiteSpace))
            {
                return new LavalinkError
                {
                    Category = ErrorCategory.None,
                    Message = "No log lines provided",
                    UserFriendlyMessage = "Unable to analyze error",
                    RawLogContent = string.Empty
                };
            }

            var combinedLog = string.Join(" ", logLines.Where(l => !string.IsNullOrWhiteSpace(l)));
            return AnalyzeLog(combinedLog);
        }

        private static LavalinkError CreateErrorResponse(ErrorCategory category, string logContent)
        {
            return category switch
            {
                ErrorCategory.AgeRestricted => new LavalinkError
                {
                    Category = category,
                    Message = "Content is age-restricted",
                    UserFriendlyMessage = "⚠️ This video is age-restricted on YouTube. YouTube requires authentication to play this content.",
                    SuggestedAction = "The bot owner should configure YouTube plugin credentials (poToken + visitorData) in Lavalink, then restart Lavalink.",
                    RawLogContent = logContent
                },
                ErrorCategory.LoginRequired => new LavalinkError
                {
                    Category = category,
                    Message = "Authentication required",
                    UserFriendlyMessage = "⚠️ This video requires YouTube account authentication. It may be private or restricted.",
                    SuggestedAction = "Ensure the YouTube video is publicly accessible. Check Lavalink YouTube plugin configuration.",
                    RawLogContent = logContent
                },
                ErrorCategory.ContentUnavailable => new LavalinkError
                {
                    Category = category,
                    Message = "Content is unavailable or removed",
                    UserFriendlyMessage = "⚠️ This video has been removed, deleted, or is unavailable. It may have copyright issues or been taken down.",
                    SuggestedAction = "Try another video. This content is no longer available to play.",
                    RawLogContent = logContent
                },
                ErrorCategory.NetworkError => new LavalinkError
                {
                    Category = category,
                    Message = "Network connectivity issue",
                    UserFriendlyMessage = "⚠️ Network error occurred while trying to reach YouTube. The connection timed out or was refused.",
                    SuggestedAction = "Check if Lavalink can reach youtube.com. Verify firewall/proxy settings. Restart Lavalink if needed.",
                    RawLogContent = logContent
                },
                ErrorCategory.InvalidTrack => new LavalinkError
                {
                    Category = category,
                    Message = "Track is invalid or contains no audio",
                    UserFriendlyMessage = "⚠️ This track is invalid or does not contain playable audio.",
                    SuggestedAction = "Try another track. This track may not have audio or may be corrupted.",
                    RawLogContent = logContent
                },
                ErrorCategory.HttpError => new LavalinkError
                {
                    Category = category,
                    Message = ExtractHttpErrorCode(logContent),
                    UserFriendlyMessage = $"⚠️ YouTube returned an HTTP error: {ExtractHttpErrorCode(logContent)}",
                    SuggestedAction = DetermineHttpErrorAction(logContent),
                    RawLogContent = logContent
                },
                ErrorCategory.YoutubePluginError => new LavalinkError
                {
                    Category = category,
                    Message = "YouTube plugin configuration issue",
                    UserFriendlyMessage = "⚠️ The YouTube plugin is not properly configured in Lavalink.",
                    SuggestedAction = "Configure YouTube plugin credentials and restart Lavalink. See Lavalink documentation for setup.",
                    RawLogContent = logContent
                },
                ErrorCategory.PlaylistError => new LavalinkError
                {
                    Category = category,
                    Message = "Playlist loading failed",
                    UserFriendlyMessage = "⚠️ Failed to load playlist or individual tracks.",
                    SuggestedAction = "Verify the playlist URL is valid and publicly accessible.",
                    RawLogContent = logContent
                },
                _ => new LavalinkError
                {
                    Category = ErrorCategory.Unknown,
                    Message = "Unknown error occurred",
                    UserFriendlyMessage = "⚠️ An unknown error occurred while processing your request.",
                    RawLogContent = logContent
                }
            };
        }

        private static LavalinkError CreateGenericErrorResponse(string logContent)
        {
            var errorMessage = ExtractErrorMessage(logContent);

            return new LavalinkError
            {
                Category = ErrorCategory.Unknown,
                Message = errorMessage,
                UserFriendlyMessage = $"⚠️ {errorMessage}",
                RawLogContent = logContent
            };
        }

        private static string ExtractErrorMessage(string logContent)
        {
            // Try to extract common error message patterns
            var patterns = new[]
            {
                @"Exception:?\s*([^'\n]+)",
                @"Error:?\s*([^'\n]+)",
                @"ERROR:?\s*([^'\n]+)",
                @"failed:?\s*([^'\n]+)",
                @"FetchingTrackContext failed",
                @"([\w\.]+Exception[^'\n]*)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(logContent, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var message = match.Groups[1].Value.Trim();
                    return string.IsNullOrWhiteSpace(message) ? "An error occurred during playback" : message;
                }
            }

            return "An error occurred during playback";
        }

        private static string ExtractHttpErrorCode(string logContent)
        {
            var match = Regex.Match(logContent, @"(\d{3})", RegexOptions.IgnoreCase);
            return match.Success ? $"HTTP {match.Groups[1].Value}" : "HTTP Error";
        }

        private static string DetermineHttpErrorAction(string logContent)
        {
            if (logContent.Contains("429", StringComparison.OrdinalIgnoreCase))
            {
                return "YouTube rate-limited the request. Wait a moment and try again.";
            }

            if (logContent.Contains("40", StringComparison.OrdinalIgnoreCase) && 
                !logContent.Contains("429", StringComparison.OrdinalIgnoreCase))
            {
                return "The request was invalid or access was denied. Check the track URL and try again.";
            }

            if (logContent.Contains("50", StringComparison.OrdinalIgnoreCase))
            {
                return "YouTube's server encountered an error. Try again later.";
            }

            return "An HTTP error occurred. Please try again.";
        }

        /// <summary>
        /// Gets a user-friendly error message for a given error category.
        /// </summary>
        public static string GetFriendlyErrorMessage(ErrorCategory category)
        {
            return category switch
            {
                ErrorCategory.AgeRestricted => "⚠️ This video is age-restricted. The bot needs proper YouTube plugin setup.",
                ErrorCategory.LoginRequired => "⚠️ This video requires login. It may be private.",
                ErrorCategory.ContentUnavailable => "⚠️ This video is unavailable or has been removed.",
                ErrorCategory.NetworkError => "⚠️ Network error. Cannot reach YouTube.",
                ErrorCategory.InvalidTrack => "⚠️ This track is invalid or contains no audio.",
                ErrorCategory.HttpError => "⚠️ YouTube returned an HTTP error.",
                ErrorCategory.YoutubePluginError => "⚠️ YouTube plugin configuration issue.",
                ErrorCategory.PlaylistError => "⚠️ Failed to load the playlist.",
                _ => "⚠️ An error occurred while processing your request."
            };
        }
    }
}
