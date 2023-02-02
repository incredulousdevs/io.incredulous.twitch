using System;
using UnityEngine;

namespace Incredulous.Twitch
{
    [Serializable]
    public class ChatMessage : ChatMessageBase
    {
        public ChatMessage(string login, string channel, string message, Tags tags) : base(login, channel, tags)
        {
            Message = message;
        }

        internal ChatMessage(Message message) : base(message)
        {
            Message = message.Parameters;
        }

        [field: SerializeField] public string Message { get; private set; }
    }
}