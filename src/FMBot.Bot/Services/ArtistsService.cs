using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using FMBot.Bot.Interfaces;
using FMBot.Bot.Models;
using FMBot.Bot.Resources;
using FMBot.LastFM.Domain.Models;
using FMBot.Persistence.Domain.Models;
using FMBot.Persistence.EntityFrameWork;
using Microsoft.EntityFrameworkCore;

namespace FMBot.Bot.Services
{
    public class ArtistsService : IArtistsService
    {
        private readonly FMBotDbContext _db;

        public ArtistsService(FMBotDbContext db)
        {
            this._db = db;
        }

        public static IList<ArtistWithUser> AddUserToIndexList(IList<ArtistWithUser> artists, User userSettings, IGuildUser user, ArtistResponse artist)
        {
            artists.Add(new ArtistWithUser
            {
                UserId = userSettings.UserId,
                ArtistName = artist.Artist.Name,
                Playcount = Convert.ToInt32(artist.Artist.Stats.Userplaycount.Value),
                LastFMUsername = userSettings.UserNameLastFM,
                DiscordUserId = userSettings.DiscordUserId,
                DiscordName = user.Nickname ?? user.Username
            });

            return artists.OrderByDescending(o => o.Playcount).ToList();
        }

        public static string ArtistWithUserToStringList(IList<ArtistWithUser> artists, ArtistResponse artistResponse, int userId)
        {
            var reply = "";

            var artistsCount = artists.Count;
            if (artistsCount > 14)
            {
                artistsCount = 14;
            }

            for (var index = 0; index < artistsCount; index++)
            {
                var artist = artists[index];

                var nameWithLink = NameWithLink(artist);
                var playString = GetPlaysString(artist.Playcount);

                if (index == 0)
                {
                    reply += $"👑  {nameWithLink}";
                }
                else
                {
                    reply += $" {index + 1}.  {nameWithLink} ";
                }
                if (artist.UserId != userId)
                {
                    reply += $"- **{artist.Playcount}** {playString}\n";
                }
                else
                {
                    reply += $"- **{artistResponse.Artist.Stats.Userplaycount}** {playString}\n";
                }
            }

            if (artists.Count == 1)
            {
                reply += $"\nNobody else has this artist in their top {Constants.ArtistsToIndex} artists.";
            }

            return reply;
        }

        private static string NameWithLink(ArtistWithUser artist)
        {
            var discordName = artist.DiscordName.Replace("(", "").Replace(")", "").Replace("[", "").Replace("]", "");
            var nameWithLink = $"[{discordName}]({Constants.LastFMUserUrl}{artist.LastFMUsername})";
            return nameWithLink;
        }

        private static string GetPlaysString(int artistPlaycount)
        {
            return artistPlaycount == 1 ? "play" : "plays";
        }

        public async Task<IList<ArtistWithUser>> GetIndexedUsersForArtist(IReadOnlyCollection<IGuildUser> guildUsers, string artistName)
        {
            var userIds = guildUsers.Select(s => s.Id);

            var artists = await this._db.Artists
                .Include(i => i.User)
                .Where(w => w.Name.ToLower() == artistName.ToLower()
                            && userIds.Contains(w.User.DiscordUserId))
                .OrderByDescending(o => o.Playcount)
                .Take(14)
                .ToListAsync();

            return artists
                .Select(s =>
                {
                    var discordUser = guildUsers.First(f => f.Id == s.User.DiscordUserId);
                    return new ArtistWithUser
                    {
                        ArtistName = s.Name,
                        DiscordName = discordUser.Nickname ?? discordUser.Username,
                        Playcount = s.Playcount,
                        DiscordUserId = s.User.DiscordUserId,
                        LastFMUsername = s.User.UserNameLastFM,
                        UserId = s.UserId,
                    };
                }).ToList();
        }


        public async Task<int> GetArtistListenerCountForServer(IEnumerable<IGuildUser> guildUsers, string artistName)
        {
            var userIds = guildUsers.Select(s => s.Id);

            return await this._db.Artists
                .Include(i => i.User)
                .Where(w => w.Name.ToLower() == artistName.ToLower()
                            && userIds.Contains(w.User.DiscordUserId))
                .CountAsync();
        }

        public async Task<int> GetArtistPlayCountForServer(IEnumerable<IGuildUser> guildUsers, string artistName)
        {
            var userIds = guildUsers.Select(s => s.Id);

            var query = this._db.Artists
                .Include(i => i.User)
                .Where(w => w.Name.ToLower() == artistName.ToLower()
                            && userIds.Contains(w.User.DiscordUserId));

            // This is bad practice, but it helps with speed. An exception gets thrown if the artist does not exist in the database.
            // Checking if the records exist first would be an extra database call
            try
            {
                return await query.SumAsync(s => s.Playcount);
            }
            catch
            {
                return 0;
            }
        }

        public async Task<double> GetArtistAverageListenerPlaycountForServer(IEnumerable<IGuildUser> guildUsers, string artistName)
        {
            var userIds = guildUsers.Select(s => s.Id);

            var query = this._db.Artists
                .Include(i => i.User)
                .Where(w => w.Name.ToLower() == artistName.ToLower()
                            && userIds.Contains(w.User.DiscordUserId));

            try
            {
                return await query.AverageAsync(s => s.Playcount);
            }
            catch
            {
                return 0;
            }
        }
    }
}
