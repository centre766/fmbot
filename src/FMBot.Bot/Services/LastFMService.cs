using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FMBot.Bot.Configurations;
using FMBot.Bot.Resources;
using FMBot.LastFM.Domain.Models;
using FMBot.LastFM.Domain.Types;
using FMBot.LastFM.Services;
using FMBot.Persistence.Domain.Models;
using IF.Lastfm.Core.Api;
using IF.Lastfm.Core.Api.Enums;
using IF.Lastfm.Core.Api.Helpers;
using IF.Lastfm.Core.Objects;

namespace FMBot.Bot.Services
{
    internal class LastFMService
    {
        private readonly LastfmClient _lastFMClient = new LastfmClient(ConfigData.Data.FMKey, ConfigData.Data.FMSecret);

        private readonly ILastfmApi _lastfmApi;

        public LastFMService(ILastfmApi lastfmApi)
        {
            this._lastfmApi = lastfmApi;
        }

        // Last scrobble
        public async Task<LastTrack> GetLastScrobbleAsync(string lastFMUserName)
        {
            var tracks = await this._lastFMClient.User.GetRecentScrobbles(lastFMUserName, null, 1, 1);
            Statistics.LastfmApiCalls.Inc();

            return tracks.Content[0];
        }

        // Recent scrobbles
        public async Task<PageResponse<LastTrack>> GetRecentScrobblesAsync(string lastFMUserName, int count = 2)
        {
            var recentScrobbles = await this._lastFMClient.User.GetRecentScrobbles(lastFMUserName, null, 1, count);
            Statistics.LastfmApiCalls.Inc();

            return recentScrobbles;
        }


        public static string TrackToLinkedString(LastTrack track)
        {
            if (track.Url.ToString().IndexOfAny(new[] { '(', ')' }) >= 0)
            {
                return TrackToString(track);
            }

            return $"[{track.Name}]({track.Url})\n" +
                   $"By **{track.ArtistName}**" +
                   (string.IsNullOrWhiteSpace(track.AlbumName)
                       ? "\n"
                       : $" | *{track.AlbumName}*\n");
        }

        public static string TrackToString(LastTrack track)
        {
            return $"{track.Name}\n" +
                   $"By **{track.ArtistName}**" +
                   (string.IsNullOrWhiteSpace(track.AlbumName)
                       ? "\n"
                       : $" | *{track.AlbumName}*\n");
        }

        public static string TrackToOneLinedString(LastTrack track)
        {
            return $"{track.Name} By **{track.ArtistName}**" +
                   (string.IsNullOrWhiteSpace(track.AlbumName)
                       ? ""
                       : $" | *{track.AlbumName}*");
        }

        public string TagsToLinkedString(Tags tags)
        {
            var tagString = "";
            for (var i = 0; i < tags.Tag.Length; i++)
            {
                if (i != 0)
                {
                    tagString += " - ";
                }
                var tag = tags.Tag[i];
                tagString += $"[{tag.Name}]({tag.Url})";
            }

            return tagString;
        }

        public string TopTagsToString(Toptags tags)
        {
            var tagString = "";
            for (var i = 0; i < tags.Tag.Length; i++)
            {
                if (i != 0)
                {
                    tagString += " - ";
                }
                var tag = tags.Tag[i];
                tagString += $"{tag.Name}";
            }

            return tagString;
        }

        // User
        public async Task<LastResponse<LastUser>> GetUserInfoAsync(string lastFMUserName)
        {
            var user = await this._lastFMClient.User.GetInfoAsync(lastFMUserName);
            Statistics.LastfmApiCalls.Inc();

            return user;
        }

        // Track info
        public async Task<Track> GetTrackInfoAsync(string trackName, string artistName, string username = null)
        {
            var queryParams = new Dictionary<string, string>
            {
                {"artist", artistName },
                {"track", trackName },
                {"username", username },
                {"autocorrect", "1"}
            };

            var trackCall = await this._lastfmApi.CallApiAsync<TrackResponse>(queryParams, Call.TrackInfo);
            Statistics.LastfmApiCalls.Inc();

            return !trackCall.Success ? null : trackCall.Content.Track;
        }

        public async Task<Response<ArtistResponse>> GetArtistInfoAsync(string artistName, string username = null)
        {
            var queryParams = new Dictionary<string, string>
            {
                {"artist", artistName },
                {"username", username },
                {"autocorrect", "1"}
            };

            var artistCall = await this._lastfmApi.CallApiAsync<ArtistResponse>(queryParams, Call.ArtistInfo);
            Statistics.LastfmApiCalls.Inc();
            
            return artistCall;
        }

        public async Task<Response<AlbumResponse>> GetAlbumInfoAsync(string artistName, string albumName, string username = null)
        {
            var queryParams = new Dictionary<string, string>
            {
                {"artist", artistName },
                {"album", albumName },
                {"username", username }
            };

            var albumCall = await this._lastfmApi.CallApiAsync<AlbumResponse>(queryParams, Call.AlbumInfo);
            Statistics.LastfmApiCalls.Inc();

            return albumCall;
        }

        // Album images
        public async Task<LastImageSet> GetAlbumImagesAsync(string artistName, string albumName)
        {
            var album = await this._lastFMClient.Album.GetInfoAsync(artistName, albumName);
            Statistics.LastfmApiCalls.Inc();

            return album?.Content?.Images;
        }

        public async Task<Bitmap> GetAlbumImageAsBitmapAsync(Uri largestImageSize)
        {
            try
            {
                var request = WebRequest.Create(largestImageSize);
                using var response = await request.GetResponseAsync();
                await using var responseStream = response.GetResponseStream();

                return new Bitmap(responseStream);
            }
            catch
            {
                return null;
            }
        }

        // Top albums
        public async Task<PageResponse<LastAlbum>> GetTopAlbumsAsync(string lastFMUserName, LastStatsTimeSpan timespan, int count = 2)
        {
            var topAlbums = await this._lastFMClient.User.GetTopAlbums(lastFMUserName, timespan, 1, count);
            Statistics.LastfmApiCalls.Inc();

            return topAlbums;
        }


        // Top artists
        public async Task<PageResponse<LastArtist>> GetTopArtistsAsync(string lastFMUserName,
            LastStatsTimeSpan timespan, int count = 2)
        {
            var topArtists = await this._lastFMClient.User.GetTopArtists(lastFMUserName, timespan, 1, count);
            Statistics.LastfmApiCalls.Inc();

            return topArtists;
        }

        // Check if lastfm user exists
        public async Task<bool> LastFMUserExistsAsync(string lastFMUserName)
        {
            var lastFMUser = await this._lastFMClient.User.GetInfoAsync(lastFMUserName);
            Statistics.LastfmApiCalls.Inc();

            return lastFMUser.Success;
        }

        public LastStatsTimeSpan GetLastStatsTimeSpan(ChartTimePeriod timePeriod)
        {
            switch (timePeriod)
            {
                case ChartTimePeriod.Weekly:
                    return LastStatsTimeSpan.Week;
                case ChartTimePeriod.Monthly:
                    return LastStatsTimeSpan.Month;
                case ChartTimePeriod.Yearly:
                    return LastStatsTimeSpan.Year;
                case ChartTimePeriod.AllTime:
                    return LastStatsTimeSpan.Overall;
                default:
                    return LastStatsTimeSpan.Week;
            }
        }
    }
}
