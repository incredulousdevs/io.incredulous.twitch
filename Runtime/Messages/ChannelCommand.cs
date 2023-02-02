namespace Incredulous.Twitch
{
    public class ChannelCommand : Command
    {
        public ChannelCommand(string type, string channel) : base(type)
        {
            Channel = channel;
        }

        public string Channel;
    }
}