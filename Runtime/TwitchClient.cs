using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Incredulous.Twitch
{
    public partial class TwitchClient
    {
        private const string TWITCH_WEBSOCKET_URL = "wss://irc-ws.chat.twitch.tv:443";
        private const int RECEIVE_BUFFER_SIZE = 512;
        private const int RETRY_CONNECTION_TIME = 500;

        private readonly Queue<string> _outputQueue = new Queue<string>();
        private readonly Queue<DateTime> _outputTimestamps = new Queue<DateTime>();

        public TwitchClient(TwitchCredentials credentials)
        {
            Credentials = new TwitchCredentials
            {
                Username = credentials.Username.ToLower(),
                Channel = credentials.Channel.ToLower(),
                Token = credentials.Token.StartsWith("oauth:") ? credentials.Token.Substring(6) : credentials.Token
            };

            if (Credentials.IsVerified)
            {
                RateLimit = RateLimit.SiteLimitVerified;
            }
            else
            {
                RateLimit = Credentials.Username == Credentials.Channel ? RateLimit.ChatModerator : RateLimit.ChatRegular;
            }
        }

        private bool _maintainConnection;
        private ClientWebSocket _webSocketClient;
        private Task _connectionProcess;
        private Task _receiveProcess;
        private Task _sendProcess;
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// The credentials used to connect to Twitch.
        /// </summary>
        public TwitchCredentials Credentials { get; }

        /// <summary>
        /// The current rate limit determined by the server role assigned to the credentials.
        /// </summary>
        public RateLimit RateLimit { get; private set; }

        /// <summary>
        /// The status of the connection.
        /// </summary>
        public Status ConnectionStatus { get; private set; }

        /// <summary>
        /// Whether all IRC message should be logged.
        /// </summary>
        public bool LogIrcMessages { get; set; }

        /// <summary>
        /// The client user's Twitch tags.
        /// </summary>
        public Tags ClientUserTags { get; private set; }

        /// <summary>
        /// An event which is triggered when a new chat message is received.
        /// </summary>
        public event ChatMessageEventHandler ChatMessageEvent;

        /// <summary>
        /// An event which is triggered when the connection status changes.
        /// </summary>
        public event ConnectionAlertEventHandler ConnectionAlertEvent;

        /// <summary>
        /// Initalizes a connection to Twitch and starts the send, receive, and check connection threads.
        /// </summary>
        public void Begin()
        {
            if (ConnectionStatus != Status.Disconnected) return;
            _connectionProcess = ConnectionProcess();
        }

        /// <summary>
        /// Close the connection.
        /// </summary>
        public void End() => _ = EndAsync();

        /// <summary>
        /// Close the connection asynchronously.
        /// </summary>
        public async Task EndAsync()
        {
            if (ConnectionStatus == Status.Disconnected || ConnectionStatus == Status.Disconnecting) return;
            TerminateConnection();
            await _connectionProcess;
        }

        /// <summary>
        /// Queues a PING command to be sent to the IRC server.
        /// </summary>
        public void Ping() => Send("PING :tmi.twitch.tv");

        /// <summary>
        /// Queues a command to be sent to the IRC server.
        /// </summary>
        public void SendCommand(string command) => Send(command);

        /// <summary>
        /// Queues a chat message to be sent to the IRC server.
        /// </summary>
        public void SendChatMessage(string message)
        {
            // Ensure message is not empty
            if (string.IsNullOrEmpty(message)) return;

            // Send the message
            Send($"PRIVMSG #{Credentials.Channel.ToLower()} :{message}");
        }

        /// <summary>
        /// The parent process which manages the receive and send processes as well as keeping the connection alive.
        /// </summary>
        private async Task ConnectionProcess()
        {
            _maintainConnection = true;
            ConnectionStatus = Status.Connecting;

            while (_maintainConnection)
            {
                try
                {
                    // Create a new continuation task
                    _cancellationTokenSource = new CancellationTokenSource();

                    // Initialize the connection
                    _webSocketClient = new ClientWebSocket();
                    Debug.Log("Connecting...");
                    await _webSocketClient.ConnectAsync(new Uri(TWITCH_WEBSOCKET_URL), _cancellationTokenSource.Token);

                    // Begin receive processes
                    Debug.Log("Starting receive process...");
                    _receiveProcess = ReceiveProcess(_cancellationTokenSource.Token);

                    // Queue login commands
                    Debug.Log("Sending initial commands...");
                    Send("CAP REQ :twitch.tv/tags twitch.tv/commands");
                    Send("PASS oauth:" + Credentials.Token);
                    Send("NICK " + Credentials.Username);

                    // NOTE: Updating the connection status to "Connected" is handled in the method HandleRPL

                    // Await each process, discard errors
                    try
                    {
                        await _receiveProcess;
                    }
                    catch { }

                    try
                    {
                        await _sendProcess;
                    }
                    catch { }

                    // Close the connection
                    if (_webSocketClient.State == WebSocketState.Open)
                        await _webSocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                finally
                {
                    await Task.Delay(RETRY_CONNECTION_TIME);
                }
            }

            ConnectionStatus = Status.Disconnected;
            Debug.Log("Disconnected");
        }

        /// <summary>
        /// The process which manages sending messages from the queue.
        /// </summary>
        private async Task SendProcess(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _outputQueue.Count > 0)
                {
                    // Clear timestamps that are older than the rate limit period
                    var minTime = DateTime.Now - RateLimit.TimeSpan;
                    while (_outputTimestamps.TryPeek(out var next) && next < minTime) _outputTimestamps.TryDequeue(out _);

                    // Send prioritized outputs
                    while (_outputTimestamps.Count < RateLimit.Count && _outputQueue.Count > 0)
                    {
                        // Get the next message
                        var message = _outputQueue.Dequeue();
                        var bytes = Encoding.UTF8.GetBytes(message);

                        // Send the message
                        await _webSocketClient.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);

                        // Record the time that this message was sent
                        _outputTimestamps.Enqueue(DateTime.Now);

                        // Log the output
                        if (LogIrcMessages) Debug.Log("<color=#c91b00><b>[IRC OUTPUT]</b></color> Sending: " + message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // This is thrown when disconnecting
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                RetryConnection();
            }
        }

        /// <summary>
        /// A helper method which queues outgoing messages and sends an alert if the rate limit is reached.
        /// </summary>
        /// <param name="value">The message to send.</param>
        private void Send(string value)
        {
            // Warn if the output will surpass the rate limit
            if (_outputTimestamps.Count + 1 > RateLimit.Count) ConnectionAlertEvent?.Invoke(ConnectionAlert.RateLimitWarning);

            // Place message in queue
            _outputQueue.Enqueue(value);

            // Start the send process if it is not running
            if (_sendProcess == null || _sendProcess.IsCompleted) _sendProcess = SendProcess(_cancellationTokenSource.Token);
        }

        /// <summary>
        /// The process which manages receiving messages from the server.
        /// </summary>
        private async Task ReceiveProcess(CancellationToken cancellationToken)
        {
            var buffer = new byte[RECEIVE_BUFFER_SIZE];
            var offset = 0;
            var builder = new StringBuilder();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var more = true;
                    while (more)
                    {
                        // Receive data
                        var arraySegment = new ArraySegment<byte>(buffer, offset, RECEIVE_BUFFER_SIZE - offset);
                        var result = await _webSocketClient.ReceiveAsync(arraySegment, cancellationToken);

                        // Handle connection closed
                        if (result.CloseStatus != null) throw new WebSocketException("Connection closed by server.");

                        // Decode the string
                        var received = Encoding.UTF8.GetString(buffer, 0, result.Count + offset);

                        // Copy any leftover bytes back to the beginning of the buffer
                        var length = Encoding.UTF8.GetByteCount(received);
                        for (var i = 0; i < result.Count - length; i++) buffer[i] = buffer[length + i];

                        // Append to the string builder
                        builder.Append(received);

                        // Check if this is the end of the message
                        more = !result.EndOfMessage;
                    }

                    // Submit the received content
                    var lines = builder.ToString().Split('\n','\r');
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrEmpty(line)) continue;
                        HandleLine(line);
                    }
                    builder.Clear();
                }
            }
            catch (OperationCanceledException)
            {
                // This is thrown when disconnecting
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                RetryConnection();
            }
        }

        /// <summary>
        /// Handles a completed line from the network stream.
        /// </summary>
        private void HandleLine(string raw)
        {
            raw = raw.Trim(' ', '\n', '\r');

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
                type = type.Split(' ')[0];

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
        /// Handles a NOTICE message from the server.
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
        /// Handles an RPL message from the server.
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
        /// Handles a PRIVMSG command form the server.
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
        /// Handles a USERSTATE command form the server.
        /// </summary>
        private void HandleUSERSTATE(string ircString, string tagString)
        {
            // Update the client user tags
            var tags = ParseHelper.ParseTags(tagString);
            ClientUserTags = tags;
            UpdateRateLimits(tags);
        }

        /// <summary>
        /// Cancels the child processes and instructs the parent process to start a new connection.
        /// </summary>
        private void RetryConnection()
        {
            ConnectionStatus = Status.Connecting;
            _cancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Cancels the child processes and instructs the parent process to terminate.
        /// </summary>
        private void TerminateConnection()
        {
            _maintainConnection = false;
            ConnectionStatus = Status.Disconnecting;
            _cancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Updates the rate limit based on the user's tags
        /// </summary>
        /// <param name="tags">The user's tags.</param>
        private void UpdateRateLimits(Tags tags)
        {
            RateLimit = (tags.HasBadge("broadcaster") || tags.HasBadge("moderator")) ? RateLimit.ChatModerator : RateLimit.ChatRegular;
        }

        /// <summary>
        /// A delegate which handles a new chat message from the server.
        /// </summary>
        public delegate void ChatMessageEventHandler(Chatter chatter);

        /// <summary>
        /// A delegate which handles a status update from the Twitch IRC client.
        /// </summary>
        public delegate void ConnectionAlertEventHandler(ConnectionAlert connectionAlert);

        /// <summary>
        /// A enumeration of connection states for the Twtch IRC client.
        /// </summary>
        public enum Status
        {
            Disconnected,
            Connecting,
            Connected,
            Disconnecting
        }
    }
}