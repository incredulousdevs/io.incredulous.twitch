using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Incredulous.Twitch
{
    internal class MessageParser
    {
        public Message ParseMessage(string message)
        {
            var result = new Message();

            string rawTagsComponent = null;
            string rawSourceComponent = null;
            string rawCommandComponent = null;
            string rawParametersComponent = null;

            var pos = 0;
            var end = 0;

            // If the message includes tags, get the tags component of the IRC message.
            if (message[pos] == '@')
            {
                end = message.IndexOf(' ');
                rawTagsComponent = message.Substring(1, end);
                pos = end + 1; // Should now point to source colon (:).
            }

            // Get the source component (nick and host) of the IRC message.
            // The pos should point to the source part; otherwise, it's a PING command.
            if (message[pos] == ':')
            {
                pos += 1;
                end = message.IndexOf(' ', pos);
                rawSourceComponent = message.Substring(pos, end - pos);
                pos = end + 1;
            }

            // Get the command component of the IRC message.
            end = message.IndexOf(':', pos);
            if (end == -1) end = message.Length;
            rawCommandComponent = message.Substring(pos, end - pos).Trim();

            // Get the parameters component of the IRC message.
            if (end != message.Length)
            {
                pos = end + 1;
                rawParametersComponent = message.Substring(pos);
            }

            // Parse the command component of the IRC message.
            result.Command = ParseCommand(rawCommandComponent);

            // Only parse the rest of the components if it's a command we care about; we ignore some messages.
            if (result.Command == null) return null;

            // Parse the tags of the IRC message.
            if (rawTagsComponent != null) result.Tags = ParseTags(rawTagsComponent);
                
            result.Source = ParseSource(rawSourceComponent);
            result.Parameters = rawParametersComponent;
            if (rawParametersComponent != null && rawParametersComponent[0] == '!' && result.Command is ChannelCommand channelCommand)
            {
                // The user entered a bot command in the chat window
                result.Command = ParseParameters(rawParametersComponent, channelCommand);
            }

            return result;
        }

        private Command ParseCommand(string rawCommandComponent)
        {
            var commandParts = rawCommandComponent.Split(' ');

            switch (commandParts[0])
            {
                case "JOIN":
                case "PART":
                case "NOTICE":
                case "CLEARCHAT":
                case "HOSTTARGET":
                case "PRIVMSG":
                case "USERSTATE": // Included only if you request the /commands capability. But it has no meaning without also including the /tags capability.
                case "ROOMSTATE": // Included only if you request the /commands capability. But it has no meaning without also including the /tags capability.
                case "001": // Logged in (successfully authenticated)
                    return new ChannelCommand(commandParts[0], commandParts[1]);

                case "PING":
                case "GLOBALUSERSTATE": // Included only if you request the /commands capability. But it has no meaning without also including the /tags capability.
                    return new Command(commandParts[0]);

                case "CAP":
                    // The parameters part of the messages contains the enabled capabilities.
                    return new CapCommand(commandParts[2] == "ACK");

                case "RECONNECT":
                    Debug.LogError("The Twitch IRC server is about to terminate the connection for maintenance.");
                    return new Command(commandParts[0]);

                case "421":
                    Debug.LogError($"Unsupported IRC command: {commandParts[2]}");
                    return null;

                // Ignoring all other numeric messages
                case "002":
                case "003":
                case "004":
                case "353":  // Tells you who else is in the chat room you're joining. TODO: handle this message
                case "366":
                case "372":
                case "375":
                case "376":
                    Debug.Log($"Numeric message: {commandParts[0]}");
                    return null;

                default:
                    Debug.LogError($"Unexpected command: {commandParts[0]}");
                    return null;
            }
        }

        private Tags ParseTags(string rawTagsComponent)
        {
            // TODO: should any other tags be handled here?

            var tags = new Tags();
            var split = rawTagsComponent.Split(';');

            //Loop through tags
            foreach (var tag in split)
            {
                var value = tag.Substring(tag.IndexOf('=') + 1);

                // Ignore empty tags
                if (value.Length <= 0) continue;

                // Find the tags needed
                switch (tag.Substring(0, tag.IndexOf('=')))
                {
                    case "badges":
                    case "badge-info":
                        tags.Badges.AddRange(ParseBadges(value));
                        continue;

                    case "color":
                        tags.ColorHex = value;
                        continue;

                    case "display-name":
                        tags.DisplayName = value;
                        continue;

                    case "emotes":
                        tags.Emotes.AddRange(ParseTwitchEmotes(value));
                        tags.Emotes.Sort((a, b) => a.Indices[0].Start.CompareTo(b.Indices[0].Start));
                        continue;

                    case "room-id": // room-id = channelId
                        tags.ChannelId = value;
                        continue;

                    case "user-id":
                        tags.UserId = value;
                        continue;
                }
            }

            return tags;
        }

        private IEnumerable<Badge> ParseBadges(string rawBadgeString)
        {
            var badgeStrings = rawBadgeString.Split(',');

            for (int i = 0; i < badgeStrings.Length; i++)
            {
                var str = badgeStrings[i];
                var divider = str.IndexOf('/');
                yield return new Badge(str.Substring(0, divider), str.Substring(divider + 1));
            }
        }

        private IEnumerable<Emote> ParseTwitchEmotes(string rawEmoteString)
        {
            var emoteStrings = rawEmoteString.Split('/');

            for (int i = 0; i < emoteStrings.Length; i++)
            {
                string str = emoteStrings[i];
                var colonPos = str.IndexOf(':');

                var indexSuperstring = str.Substring(colonPos + 1);
                var indexStrings = indexSuperstring.Length > 0 ? indexSuperstring.Split(',') : new string[0];
                var indices = new Emote.Index[indexStrings.Length];

                for (int j = 0; j < indices.Length; ++j)
                {
                    var hyphenPos = indexStrings[j].IndexOf('-');
                    indices[j].Start = int.Parse(indexStrings[j].Substring(0, hyphenPos));
                    indices[j].End = int.Parse(indexStrings[j].Substring(hyphenPos + 1));
                }

                yield return new Emote(str.Substring(0, colonPos), indices);
            }
        }

        private Source ParseSource(string rawSourceComponent)
        {
            if (rawSourceComponent == null) return null;

            var sourceParts = rawSourceComponent.Split('!');
            if (sourceParts.Length == 2)
            {
                return new Source(sourceParts[0], sourceParts[1]);
            }
            else
            {
                return new Source(null, sourceParts[0]);
            }
        }

        private BotCommand ParseParameters(string rawParametersComponent, ChannelCommand command)
        {
            var pos = 0;
            var commandParts = rawParametersComponent.Substring(pos + 1).Trim();
            var paramsPos = commandParts.IndexOf(' ');

            if (paramsPos == -1)
            {
                return new BotCommand(command, commandParts);
            }
            else
            {
                var botCommandName = commandParts.Substring(0, paramsPos);
                var botCommandParams = commandParts.Substring(paramsPos).Trim();
                return new BotCommand(command, botCommandName, botCommandParams);
            }
        }
    }
}
