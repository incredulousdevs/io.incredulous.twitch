using System;

namespace Incredulous.Twitch
{
    [Serializable]
    public class Badge
    {
        public Badge(string id, string version)
        {
            Id = id;
            Version = version;
        }

        public string Id;
        public string Version;
    }
}