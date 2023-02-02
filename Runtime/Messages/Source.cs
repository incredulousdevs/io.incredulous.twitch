namespace Incredulous.Twitch
{
    public class Source
    {
        public Source(string nick, string host)
        {
            Nick = nick;
            Host = host;
        }

        public string Nick;
        public string Host;
    }
}