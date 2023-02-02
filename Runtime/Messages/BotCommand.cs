namespace Incredulous.Twitch
{
    public class BotCommand : ChannelCommand
    {
        public BotCommand(string command, string channel, string botCommand, string botParams) : base (command, channel)
        {
            BotCommandName = botCommand;
            BotCommandParams = botParams;
        }

        public BotCommand(ChannelCommand command, string botCommand, string botParams = null) : base (command.Type, command.Channel)
        {
            BotCommandName = botCommand;
            BotCommandParams = botParams;
        }

        public string BotCommandName;
        public string BotCommandParams;
    }
}