using System;
using UnityEngine;

namespace Incredulous.Twitch
{
    public abstract class ChatMessageBase
    {
        public ChatMessageBase(string login, string channel, Tags tags)
        {
            Login = login;
            Channel = channel;
            Tags = tags;
        }

        internal ChatMessageBase(Message message)
        {
            Login = message.Source.Nick;
            Channel = ((ChannelCommand)message.Command).Channel;
            Tags = message.Tags;
        }

        [field: SerializeField] public string Login { get; private set; }
        [field: SerializeField] public string Channel { get; private set; }
        [field: SerializeField] public Tags Tags { get; private set; }

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
        /// Returns whether the message contain a given emote.
        /// </summary>
        public bool ContainsEmote(string emote) => Tags.ContainsEmote(emote);

        /// <summary>
        /// Returns whether the message has a given badge.
        /// </summary>
        public bool HasBadge(string badge) => Tags.HasBadge(badge);
    }
}