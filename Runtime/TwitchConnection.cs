using System.Collections;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Incredulous.Twitch
{
    public partial class TwitchConnection
    {
        public TwitchConnection(string ircAddress, int port, TwitchCredentials credentials)
        {
            _ircAddress = ircAddress;
            _ircPort = port;

            Credentials = new TwitchCredentials
            {
                Username = credentials.Username.ToLower(),
                Channel = credentials.Channel.ToLower(),
                Token = credentials.Token.StartsWith("oauth:") ? credentials.Token.Substring(6) : credentials.Token
            };

            RateLimit = Credentials.Username == Credentials.Channel ? RateLimit.ChatModerator : RateLimit.ChatRegular;

            LogIrcMessages = true;
        }

        private TcpClient _tcpClient;
        private string _ircAddress;
        private int _ircPort;

        private Task _connectionProcess;
        private Task _receiveProcess;
        private Task _sendProcess;
        private TaskCompletionSource<bool> _parentProcessContinuationTcs;
        private CancellationTokenSource _childProcessCancellationSource;

        public TwitchCredentials Credentials { get; }

        public RateLimit RateLimit { get; private set; }

        public Status ConnectionStatus { get; private set; }

        public bool LogIrcMessages { get; set; }

        /// <summary>
        /// The client user's Twitch tags.
        /// </summary>
        public IRCTags ClientUserTags { get; private set; }

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
        public void End()
        {
            if (ConnectionStatus == Status.Disconnected || ConnectionStatus == Status.Disconnecting) return;
            TerminateConnection();
        }

        /// <summary>
        /// Close the connection asynchronously.
        /// </summary>
        public async Task EndAsync()
        {
            if (ConnectionStatus == Status.Disconnected || ConnectionStatus == Status.Disconnecting) return;
            await Task.Yield();

            TerminateConnection();
            await _connectionProcess;
        }

        /// <summary>
        /// Updates the rate limit based on the tags received from a USERSTATE message.
        /// </summary>
        private void UpdateRateLimits(IRCTags tags)
        {
            RateLimit = (tags.HasBadge("broadcaster") || tags.HasBadge("moderator")) ? RateLimit.ChatModerator : RateLimit.ChatRegular;
        }

        private async Task ConnectionProcess()
        {
            var maintainConnection = true;
            ConnectionStatus = Status.Connecting;

            while (maintainConnection)
            {
                // Create a new continuation task
                _parentProcessContinuationTcs = new TaskCompletionSource<bool>();

                // Initialize the connection
                _tcpClient = new TcpClient
                {
                    ReceiveTimeout = 1000,
                    SendTimeout = 5000
                };
                Debug.Log("Connecting...");
                await _tcpClient.ConnectAsync(_ircAddress, _ircPort);

                // Begin receive processes
                Debug.Log("Starting receive process...");
                _childProcessCancellationSource = new CancellationTokenSource();
                _receiveProcess = ReceiveProcess(_childProcessCancellationSource.Token);

                // Queue login commands
                Debug.Log("Sending initial commands...");
                Send("CAP REQ :twitch.tv/tags twitch.tv/commands");
                Send("PASS oauth:" + Credentials.Token);
                Send("NICK " + Credentials.Username);

                // NOTE: Updating the connection status to "Connected" is handled in the method HandleRPL

                // Wait while the connection is live
                maintainConnection = await _parentProcessContinuationTcs.Task;

                // Update the connection status
                ConnectionStatus = maintainConnection ? Status.Connecting : Status.Disconnecting;

                // Terminate the send/receive processes
                _childProcessCancellationSource.Cancel();

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

                _tcpClient.Close();
            }

            ConnectionStatus = Status.Disconnected;
            Debug.Log("Disconnected");
        }

        private void RetryConnection()
        {
            _parentProcessContinuationTcs.TrySetResult(true);
        }

        private void TerminateConnection()
        {
            _parentProcessContinuationTcs.TrySetResult(false);
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