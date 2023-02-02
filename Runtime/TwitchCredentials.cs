using System;
using UnityEngine;

namespace Incredulous.Twitch
{
    [Serializable]
    public struct TwitchCredentials
    {
        [Tooltip("The Twitch username which will be used to authenticate with Twitch.")]
        public string Username;

        [Tooltip("The Twitch channel which the chat client will join.")]
        public string Channel;

        [Tooltip("The OAuth token which will be used to authenticate with Twitch. Generate a token at https://twitchapps.com/tmi/.")]
        public string Token;
    }
}