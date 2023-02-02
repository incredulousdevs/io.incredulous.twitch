using System;
using UnityEngine;

namespace Incredulous.Twitch
{
    /// <summary>
    /// A normal chat message.
    /// </summary>
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

        [field: Tooltip("The content of the chat message.")]
        [field: SerializeField] public string Message { get; private set; }
    }
}