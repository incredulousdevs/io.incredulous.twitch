using System;

namespace Incredulous.Twitch
{
    [Serializable]
    public class Emote
    {
        public Emote(string id, Index[] indices)
        {
            Id = id;
            Indices = indices;
        }

        public string Id;
        public Index[] Indices;

        [Serializable]
        public struct Index
        {
            public int Start;
            public int End;
        }
    }
}