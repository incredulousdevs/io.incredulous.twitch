using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net.WebSockets;
using UnityEngine;

namespace Incredulous.Twitch
{
    public partial class TwitchConnection
    {
        private const int RECEIVE_BUFFER_SIZE = 512;

        /// <summary>
        /// The IRC input process which will run on the receive thread.
        /// </summary>
        private async Task ReceiveProcess(CancellationToken cancellationToken)
        {
            //var stream = _webSocketClient.GetStream();
            var buffer = new byte[512];
            var builder = new StringBuilder();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    //await Task.Yield();
                    //if (!stream.DataAvailable) continue;

                    Debug.Log("Beginning read...");
                    var more = true;
                    while (more)
                    {
                        var result = await _webSocketClient.ReceiveAsync(buffer, cancellationToken);
                        if (result.CloseStatus != null) throw new WebSocketException("Connection closed by server.");

                        var received = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Debug.Log($"Received: {received}");

                        for (var i = 0; i < received.Length; i++)
                        {
                            if (received[i] == '\n' || received[i] == '\r')
                            {
                                var line = builder.ToString();
                                builder.Clear();

                                if (!string.IsNullOrEmpty(line)) HandleLine(line);

                                continue;
                            }

                            builder.Append(received[i]);
                        }
                        
                        more = !result.EndOfMessage;
                    }

                    await Task.Delay(100);
                }
            }
            catch (IOException ex)
            {
                Debug.LogError("Error while reading NetworkStream.");
                Debug.LogException(ex);
                RetryConnection();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                RetryConnection();
            }
        }

        /// <summary>
        /// Handle a completed line from the network stream.
        /// </summary>
        private void HandleLine(string raw)
        {
            if (LogIrcMessages) Debug.Log("<color=#005ae0><b>[IRC INPUT]</b></color> " + raw);

            // Respond to PING messages
            if (raw.StartsWith("PING"))
            {
                Send("PONG :tmi.twitch.tv");
                return;
            }

            // Notify when PONG messages are received.
            if (raw.StartsWith(":tmi.twitch.tv PONG"))
            {
                ConnectionAlertEvent?.Invoke(ConnectionAlert.Pong);
                return;
            }

            string ircString = raw;
            string tagString = string.Empty;

            if (raw[0] == '@')
            {
                int ind = raw.IndexOf(' ');

                tagString = raw.Substring(0, ind);
                ircString = raw.Substring(ind).TrimStart();
            }

            if (ircString[0] == ':')
            {
                string type = ircString.Substring(ircString.IndexOf(' ')).TrimStart();
                type = type.Substring(0, type.IndexOf(' '));

                switch (type)
                {
                    case "PRIVMSG": // = Chat message
                        HandlePRIVMSG(ircString, tagString);
                        break;
                    case "USERSTATE": // = Userstate
                        HandleUSERSTATE(ircString, tagString);
                        break;
                    case "NOTICE": // = Notice
                        HandleNOTICE(ircString, tagString);
                        break;
                    case "353": // = Successful channel join
                    case "001": // = Successful IRC connection
                        HandleRPL(type);
                        break;
                }
            }
        }

        /// <summary>
        /// Handle a NOTICE message from the server.
        /// </summary>
        private void HandleNOTICE(string ircString, string tagString)
        {
            if (ircString.Contains(":Login authentication failed"))
            {
                ConnectionAlertEvent?.Invoke(ConnectionAlert.BadLogin);
                TerminateConnection();
            }
        }

        /// <summary>
        /// Handle an RPL message from the server.
        /// </summary>
        private void HandleRPL(string type)
        {
            switch (type)
            {
                case "001":
                    ConnectionAlertEvent?.Invoke(ConnectionAlert.ConnectedToServer);
                    Send("JOIN #" + Credentials.Channel.ToLower());
                    break;
                case "353":
                    ConnectionStatus = Status.Connected;
                    ConnectionAlertEvent?.Invoke(ConnectionAlert.JoinedChannel);
                    break;
            }
        }

        /// <summary>
        /// Handle a PRIVMSG command form the server.
        /// </summary>
        private void HandlePRIVMSG(string ircString, string tagString)
        {
            // Parse PRIVMSG
            var login = ParseHelper.ParseLoginName(ircString);
            var channel = ParseHelper.ParseChannel(ircString);
            var message = ParseHelper.ParseMessage(ircString);
            var tags = ParseHelper.ParseTags(tagString);

            // Queue chatter object
            ChatMessageEvent?.Invoke(new Chatter(login, channel, message, tags));
        }

        /// <summary>
        /// Handle a USERSTATE command form the server.
        /// </summary>
        private void HandleUSERSTATE(string ircString, string tagString)
        {
            // Update the client user tags
            var tags = ParseHelper.ParseTags(tagString);
            ClientUserTags = tags;
            UpdateRateLimits(tags);
        }

        /// <summary>
        /// Checks whether the socket is still connected to the network.
        /// </summary>
        private bool CheckSocketConnection(Socket socket)
        {
            var poll = socket.Poll(1000, SelectMode.SelectRead);
            var avail = (socket.Available == 0);
            if ((poll && avail) || !socket.Connected)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}