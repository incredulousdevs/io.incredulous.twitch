using System;
using System.Linq;

namespace Incredulous.Twitch
{
    [Serializable]
    public struct ChatterEmote
    {
        public string Id;
        public Index[] Indexes;

        [Serializable]
        public struct Index
        {
            public int Start;
            public int End;
        }
    }

    [Serializable]
    public struct ChatterBadge
    {
        public string Id;
        public string Version;
    }

    [Serializable]
    public class IRCTags
    {
        public string ColorHex = "#FFFFFF";
        public string DisplayName = string.Empty;
        public string ChannelId = string.Empty;
        public string UserId = string.Empty;

        public ChatterBadge[] Badges = new ChatterBadge[0];
        public ChatterEmote[] Emotes = new ChatterEmote[0];

        /// <summary>
        /// Returns whether the tags contain a given emote.
        /// </summary>
        public bool ContainsEmote(string emote) => Emotes.Any(e => e.Id == emote);

        /// <summary>
        /// Returns whether the tags contain a given badge.
        /// </summary>
        public bool HasBadge(string badge) => Badges.Any(b => b.Id == badge);
    }
}