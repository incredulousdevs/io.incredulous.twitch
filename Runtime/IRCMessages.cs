using System;
using System.Linq;
using System.Collections.Generic;

namespace Incredulous.Twitch
{
    [Serializable]
    public class Emote
    {
        public Emote(string id, Index[] indices)
        {
            Id = id;
            Indices = indices;
        }

        public string Id;
        public Index[] Indices;

        [Serializable]
        public struct Index
        {
            public int Start;
            public int End;
        }
    }

    [Serializable]
    public class Badge
    {
        public Badge(string id, string version)
        {
            Id = id;
            Version = version;
        }

        public string Id;
        public string Version;
    }

    [Serializable]
    public class Tags
    {
        public string ColorHex = "#FFFFFF";
        public string DisplayName = string.Empty;
        public string ChannelId = string.Empty;
        public string UserId = string.Empty;

        public List<Badge> Badges = new List<Badge>();
        public List<Emote> Emotes = new List<Emote>();

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