using UnityEngine;

namespace Incredulous.Twitch
{
    [System.Serializable]
    public class Chatter
    {
        public Chatter(string login, string channel, string message, Tags tags)
        {
            Login = login;
            Channel = channel;
            Message = message;
            Tags = tags;
        }

        public string Login { get; private set; }
        public string Channel { get; private set; }
        public string Message { get; private set; }
        public Tags Tags { get; private set; }

        /// <summary>
        /// Get RGBA color using HEX color code
        /// </summary>
        public Color GetRGBAColor()
        {
            if (ColorUtility.TryParseHtmlString(Tags.ColorHex, out Color color))
            {
                return color;
            }
            else
            {
                //Return default white if parsing fails for some reason
                return new Color(1, 1, 1, 1);
            }
        }

        /// <summary>
        /// Returns true if name is "font-safe" meaning that it only contains characters: a-z, A-Z, 0-9, _
        /// </summary>
        public bool IsDisplayNameFontSafe() => true;// ParseHelper.CheckNameRegex(Tags.DisplayName);

        /// <summary>
        /// Returns whether the message contain a given emote.
        /// </summary>
        public bool ContainsEmote(string emote) => Tags.ContainsEmote(emote);

        /// <summary>
        /// Returns whether the message has a given badge.
        /// </summary>
        public bool HasBadge(string badge) => Tags.HasBadge(badge);
    }
}