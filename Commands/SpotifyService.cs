using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Clara_bot.Commands
{
    public static class SpotifyService
    {
        private static readonly HttpClient _http = new();
        private static readonly Regex TrackRegex = new(
            @"open\.spotify\.com/track/([a-zA-Z0-9]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex PlaylistRegex = new(
            @"open\.spotify\.com/(playlist|album)/([a-zA-Z0-9]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool IsSpotifyUrl(string url) => TrackRegex.IsMatch(url) || PlaylistRegex.IsMatch(url);
        public static bool IsSpotifyTrack(string url) => TrackRegex.IsMatch(url);
        public static bool IsSpotifyPlaylist(string url) => PlaylistRegex.IsMatch(url);

        public static string ExtractTrackId(string url)
        {
            var match = TrackRegex.Match(url);
            return match.Success ? match.Groups[1].Value : "";
        }

        public static string ExtractPlaylistId(string url)
        {
            var match = PlaylistRegex.Match(url);
            return match.Success ? match.Groups[2].Value : "";
        }

        private static string ExtractType(string url)
        {
            if (url.Contains("/album/")) return "album";
            return "playlist";
        }

        public static async Task<SpotifyTrackInfo?> GetTrackInfoAsync(string spotifyUrl)
        {
            try
            {
                var oembedUrl = $"https://open.spotify.com/oembed?url={Uri.EscapeDataString(spotifyUrl)}";
                var request = new HttpRequestMessage(HttpMethod.Get, oembedUrl);
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                using var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
                var author = root.TryGetProperty("author_name", out var a) ? a.GetString() : null;
                if (string.IsNullOrWhiteSpace(title)) return null;

                return new SpotifyTrackInfo
                {
                    Title = title,
                    Artist = author ?? "Unknown Artist",
                    SearchQuery = $"{title} {author}".Trim(),
                };
            }
            catch
            {
                return null;
            }
        }

        public static async Task<List<SpotifyTrackInfo>> GetPlaylistTracksAsync(string playlistUrl)
        {
            var type = ExtractType(playlistUrl);
            var id = ExtractPlaylistId(playlistUrl);
            Console.WriteLine($"[Spotify] GetPlaylistTracks: type={type}, id={id}");

            if (string.IsNullOrWhiteSpace(id))
                return new List<SpotifyTrackInfo>();

            // Method 1: spotifydown.com API (free, no auth)
            var tracks = await GetTracksFromSpotifyDownAsync(type, id);
            if (tracks.Count > 0)
            {
                Console.WriteLine($"[Spotify] spotifydown.com thành công: {tracks.Count} tracks");
                return tracks;
            }

            // Method 2: Spotify Web API via anonymous token
            Console.WriteLine("[Spotify] spotifydown thất bại, thử Web API...");
            tracks = await GetTracksFromWebApiAsync(type, id);
            if (tracks.Count > 0)
            {
                Console.WriteLine($"[Spotify] Web API thành công: {tracks.Count} tracks");
                return tracks;
            }

            // Method 3: OEmbed fallback
            Console.WriteLine("[Spotify] Tất cả thất bại, thử OEmbed fallback...");
            var info = await GetTrackInfoAsync(playlistUrl);
            if (info != null)
            {
                Console.WriteLine($"[Spotify] OEmbed album: {info.Title}");
                tracks.Add(info);
            }

            return tracks;
        }

        private static async Task<List<SpotifyTrackInfo>> GetTracksFromSpotifyDownAsync(string type, string id)
        {
            var tracks = new List<SpotifyTrackInfo>();

            try
            {
                var apiUrl = $"https://api.spotifydown.com/{type}/{id}";
                var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                request.Headers.TryAddWithoutValidation("Referer", "https://spotifydown.com/");
                request.Headers.TryAddWithoutValidation("Origin", "https://spotifydown.com");

                using var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode) return tracks;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("tracks", out var tracksArray))
                {
                    foreach (var item in tracksArray.EnumerateArray())
                    {
                        var title = item.TryGetProperty("title", out var ti) ? ti.GetString() : null;
                        var artist = item.TryGetProperty("artists", out var ar) ? ar.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(title))
                            tracks.Add(MakeTrack(title, artist ?? ""));
                    }
                }
                else if (root.TryGetProperty("list", out var listArray))
                {
                    foreach (var item in listArray.EnumerateArray())
                    {
                        var title = item.TryGetProperty("title", out var ti) ? ti.GetString() : null;
                        var artist = item.TryGetProperty("artist", out var ar) ? ar.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(title))
                            tracks.Add(MakeTrack(title, artist ?? ""));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Spotify] Lỗi spotifydown: {ex.Message}");
            }

            return tracks;
        }

        private static async Task<List<SpotifyTrackInfo>> GetTracksFromWebApiAsync(string type, string id)
        {
            var tracks = new List<SpotifyTrackInfo>();

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://open.spotify.com");
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

                using var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode) return tracks;

                var html = await response.Content.ReadAsStringAsync();
                var tokenMatch = Regex.Match(html,
                    @"""accessToken""\s*:\s*""([a-zA-Z0-9_\-]+(?:\.[a-zA-Z0-9_\-]+)+)""",
                    RegexOptions.IgnoreCase);

                if (!tokenMatch.Success) return tracks;
                var token = tokenMatch.Groups[1].Value;

                var apiUrl = $"https://api.spotify.com/v1/{type}s/{id}/tracks?limit=50&market=US";
                using var apiRequest = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                apiRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                using var apiResponse = await _http.SendAsync(apiRequest);
                if (!apiResponse.IsSuccessStatusCode) return tracks;

                var json = await apiResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("items", out var items)) return tracks;

                foreach (var item in items.EnumerateArray())
                {
                    var track = item.TryGetProperty("track", out var t) ? t : item;
                    if (!track.TryGetProperty("name", out var nameProp)) continue;
                    var title = nameProp.GetString() ?? "";
                    var artists = "";
                    if (track.TryGetProperty("artists", out var artistsArray) && artistsArray.EnumerateArray().Any())
                        artists = string.Join(", ", artistsArray.EnumerateArray().Select(a =>
                            a.TryGetProperty("name", out var n) ? n.GetString() : ""));
                    if (!string.IsNullOrWhiteSpace(title))
                        tracks.Add(MakeTrack(title, artists));
                }
            }
            catch { }

            return tracks;
        }

        private static SpotifyTrackInfo MakeTrack(string title, string artist)
        {
            return new SpotifyTrackInfo
            {
                Title = title,
                Artist = artist,
                SearchQuery = string.IsNullOrWhiteSpace(artist) ? title : $"{title} {artist}",
            };
        }
    }

    public class SpotifyTrackInfo
    {
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string SearchQuery { get; set; } = "";
    }
}
