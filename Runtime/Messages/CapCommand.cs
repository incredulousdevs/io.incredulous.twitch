namespace Incredulous.Twitch
{
    public class CapCommand : Command
    {
        public CapCommand(bool isCapRequestEnabled) : base("CAP")
        {
            IsCapRequestEnabled = isCapRequestEnabled;
        }
        
        public bool IsCapRequestEnabled;
    }
}