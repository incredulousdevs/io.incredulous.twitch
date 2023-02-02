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

        [Tooltip("Whether the client should use verified-status rate limits. NOTE: Do not set this to true if you have not been granted verified status by Twitch. Your account risks being locked or banned.")]
        public bool IsVerified;
    }
}