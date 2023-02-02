using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Incredulous.Twitch
{
    internal static class ParseHelper
    {
        public static int IndexOfNth(this string source, char val, int nth = 0)
        {
            int index = source.IndexOf(val);

            for (int i = 0; i < nth; i++)
            {
                if (index == -1) return -1;
                index = source.IndexOf(val, index + 1);
            }

            return index;
        }

        private static Regex _symbolRegex = new Regex(@"^[a-zA-Z0-9_ ]+$");

        /// <summary>
        /// Checks whether a display name uses simple characters only (a-z, A-Z, 0-9, _, space)
        /// </summary>
        /// <param name="displayName">The display name to check</param>
        /// <returns>True if the name uses only simple characters, false othewise.</returns>
        public static bool CheckNameRegex(string displayName) => _symbolRegex.IsMatch(displayName);

        public static IRCTags ParseTags(string tagString)
        {
            IRCTags tags = new IRCTags();
            string[] split = tagString.Split(';');

            //Loop through tags
            for (int i = 0; i < split.Length; ++i)
            {
                string value = split[i].Substring(split[i].IndexOf('=') + 1);

                // Ignore empty tags
                if (value.Length <= 0) continue;

                // Find the tags needed
                switch (split[i].Substring(0, split[i].IndexOf('=')))
                {
                    case "badges":
                        tags.Badges = ParseBadges(value.Split(','));
                        continue;

                    case "color":
                        tags.ColorHex = value;
                        continue;

                    case "display-name":
                        tags.DisplayName = value;
                        continue;

                    case "emotes":
                        tags.Emotes = ParseTwitchEmotes(value.Split('/')).OrderBy(t => t.Indexes[0].Start).ToArray();
                        continue;

                    case "room-id": // room-id = channelId
                        tags.ChannelId = value;
                        continue;

                    case "user-id":
                        tags.UserId = value;
                        continue;
                }
            }

            return tags;
        }

        public static string ParseLoginName(string ircString)
        {
            return ircString.Substring(1, ircString.IndexOf('!') - 1);
        }

        public static string ParseChannel(string ircString)
        {
            return ircString.Substring(ircString.IndexOf('#') + 1).Split(' ')[0];
        }

        public static string ParseMessage(string ircString)
        {
            return ircString.Substring(ircString.IndexOfNth(' ', 2) + 2);
        }

        public static ChatterEmote[] ParseTwitchEmotes(string[] emoteStrings)
        {
            var emotes = new ChatterEmote[emoteStrings.Length];

            for (int i = 0; i < emoteStrings.Length; i++)
            {
                string str = emoteStrings[i];
                var colonPos = str.IndexOf(':');

                var indexSuperstring = str.Substring(colonPos + 1);
                var indexStrings = indexSuperstring.Length > 0 ? indexSuperstring.Split(',') : new string[0];
                var indexes = new ChatterEmote.Index[indexStrings.Length];

                for (int j = 0; j < indexes.Length; ++j)
                {
                    var hyphenPos = indexStrings[j].IndexOf('-');
                    indexes[j].Start = int.Parse(indexStrings[j].Substring(0, hyphenPos));
                    indexes[j].End = int.Parse(indexStrings[j].Substring(hyphenPos + 1));
                }

                emotes[i] = new ChatterEmote()
                {
                    Id = str.Substring(0, colonPos),
                    Indexes = indexes
                };
            }

            return emotes;
        }

        public static ChatterBadge[] ParseBadges(string[] badgeStrings)
        {
            var badges = new ChatterBadge[badgeStrings.Length];

            for (int i = 0; i < badgeStrings.Length; i++)
            {
                var str = badgeStrings[i];
                var divider = str.IndexOf('/');
                badges[i].Id = str.Substring(0, divider);
                badges[i].Version = str.Substring(divider + 1);
            }

            return badges;
        }
    }
}