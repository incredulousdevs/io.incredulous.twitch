using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using UnityEngine;

namespace Incredulous.Twitch
{

    public partial class TwitchConnection
    {
        const int READ_BUFFER_SIZE = 256;
        const int READ_INTERVAL = 100;

        /// <summary>
        /// The IRC input process which will run on the receive thread.
        /// </summary>
        private async Task ReceiveProcess(CancellationToken cancellationToken)
        {
            var stream = _tcpClient.GetStream();
            var currentString = new StringBuilder();
            var inputBuffer = new byte[READ_BUFFER_SIZE];
            var chars = new char[READ_BUFFER_SIZE];
            var decoder = Encoding.UTF8.GetDecoder();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    /*
                    // check if the socket is still connected
                    if (!CheckSocketConnection(tcpClient.Client))
                    {
                        // Sometimes, right after a new TcpClient is created, the socket says
                        // it has been shutdown. This catches that case and reconnects.
                        isConnected = false;
                        alertQueue.Enqueue(ConnectionAlert.ConnectionInterrupted);
                        break;
                    }
                    */

                    if (!stream.DataAvailable)
                    {
                        await Task.Delay(READ_INTERVAL);
                        continue;
                    }

                    // Receive and decode the data
                    Debug.Log("Beginning read...");
                    var bytesReceived = await stream.ReadAsync(inputBuffer, 0, READ_BUFFER_SIZE, cancellationToken);
                    var charCount = decoder.GetChars(inputBuffer, 0, bytesReceived, chars, 0);
                    Debug.Log("Read data!");

                    // iterate through the received characters
                    for (var i = 0; i < charCount; i++)
                    {
                        if (chars[i] == '\n' || chars[i] == '\r')
                        {
                            // if the character is a linebreak, handle the line
                            if (currentString.Length > 0) HandleLine(currentString.ToString());
                            currentString.Clear();
                        }
                        else
                        {
                            // append non-linebreak characters to the current string
                            currentString.Append(chars[i]);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException ex)
            {
                Debug.LogError("Error while reading NetworkStream.");
                Debug.LogException(ex);
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
                SendCommand("PONG :tmi.twitch.tv");
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
                isConnected = false;
                ConnectionAlertEvent?.Invoke(ConnectionAlert.BadLogin);
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
                    SendCommand("JOIN #" + Channel.ToLower(), true);
                    break;
                case "353":
                    isConnected = true;
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