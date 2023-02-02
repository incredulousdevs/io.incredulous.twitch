using System;
using UnityEngine;

namespace Incredulous.Twitch
{
    /// <summary>
    /// A bot command message.
    /// </summary>
    [Serializable]
    public class BotCommandMessage : ChatMessageBase
    {
        public BotCommandMessage(string login, string channel, string commandName, string commandParams, Tags tags) : base(login, channel, tags)
        {
            CommandName = commandName;
            CommandParams = commandParams;
        }

        internal BotCommandMessage(Message message) : base(message)
        {
            var botCommand = (BotCommand)message.Command;
            CommandName = botCommand.BotCommandName;
            CommandParams = botCommand.BotCommandParams;
        }

        [field: Tooltip("The name of the command.")]
        [field: SerializeField] public string CommandName { get; private set; }

        [field: Tooltip("The optional string of parameters following the command.")]
        [field: SerializeField] public string CommandParams { get; private set; }
    }
}