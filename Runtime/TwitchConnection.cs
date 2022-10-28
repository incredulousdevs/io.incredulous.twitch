using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Incredulous.Twitch
{

    public partial class TwitchConnection
    {
        public TwitchConnection(string ircAddress, int port, TwitchCredentials twitchCredentials)
        {
            _ircAddress = ircAddress;
            _ircPort = port;

            Username = twitchCredentials.username;
            Token = twitchCredentials.oauth;
            Channel = twitchCredentials.channel;

            //clientUserTags = twitchIRC.clientUserTags;

            _chatRateLimit = twitchCredentials.username == twitchCredentials.channel ? RateLimit.ChatModerator : RateLimit.ChatRegular;
            _outputTimestamps = new ConcurrentQueue<DateTime>();

            LogIrcMessages = true;
        }

        private TcpClient _tcpClient;
        private string _ircAddress;
        private int _ircPort;

        private IRCTags _clientUserTags;
        private int _isConnected;

        public string Username { get; }
        public string Token { get; }
        public string Channel { get; }

        //private readonly ConcurrentQueue<ConnectionAlert> _alertQueue = new ConcurrentQueue<ConnectionAlert>();
        //private readonly ConcurrentQueue<Chatter> _chatterQueue = new ConcurrentQueue<Chatter>();
        private readonly ConcurrentQueue<DateTime> _outputTimestamps = new ConcurrentQueue<DateTime>();

        private bool _maintainConnection;
        private Task _connectionTask;
        private CancellationTokenSource _cancellationTokenSource;

        private object _rateLimitLock = new object();
        private RateLimit _chatRateLimit;

        public bool LogIrcMessages;

        /// <summary>
        /// The client user's Twitch tags.
        /// </summary>
        public IRCTags ClientUserTags
        {
            get => _clientUserTags;
            private set => Interlocked.Exchange(ref _clientUserTags, value);
        }

        /// <summary>
        /// Whether this instance is currently connected to Twitch.
        /// </summary>
        public bool isConnected
        {
            get => _isConnected == 1;
            private set => Interlocked.Exchange(ref _isConnected, value ? 1 : 0);
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
            if (_maintainConnection) return;
            _maintainConnection = true;
            _connectionTask = MaintainConnection();
        }

        /// <summary>
        /// A coroutine which closes the connection and threads without blocking the main thread.
        /// </summary>
        public async Task End()
        {
            if (!_maintainConnection) return;
            _maintainConnection = false;
            _cancellationTokenSource.Cancel();
            await _connectionTask;

            isConnected = false;
        }

        /// <summary>
        /// A coroutine which closes the connection and threads without blocking the main thread.
        /// </summary>
        public void BlockingEndAndClose()
        {
            if (!_maintainConnection) return;
            _maintainConnection = false;
            _cancellationTokenSource.Cancel();
            _connectionTask.Wait();

            isConnected = false;
        }

        /// <summary>
        /// Updates the rate limit based on the tags received from a USERSTATE message.
        /// </summary>
        private void UpdateRateLimits(IRCTags tags)
        {
            if (tags.HasBadge("broadcaster") || tags.HasBadge("moderator"))
            {
                lock (_rateLimitLock) _chatRateLimit = RateLimit.ChatModerator;
            }
            else
            {
                lock (_rateLimitLock) _chatRateLimit = RateLimit.ChatRegular;
            }
        }

        private async Task MaintainConnection()
        {
            while (_maintainConnection)
            {
                // Initialize the connection
                _tcpClient = new TcpClient();
                Debug.Log("Connecting...");
                await _tcpClient.ConnectAsync(_ircAddress, _ircPort);

                // Begin send and receive processes
                Debug.Log("Starting send/receive routines...");
                _cancellationTokenSource = new CancellationTokenSource();
                var receiveTask = ReceiveProcess(_cancellationTokenSource.Token);
                var sendTask = SendProcess(_cancellationTokenSource.Token);

                // Queue login commands
                Debug.Log("Sending initial commands...");
                SendCommand("PASS oauth:" + Token.ToLower(), true);
                SendCommand("NICK " + Username.ToLower(), true);
                SendCommand("CAP REQ :twitch.tv/tags twitch.tv/commands", true);

                // Wait for any of the tasks to complete
                var task = await Task.WhenAny(receiveTask, sendTask);
                if (task.IsFaulted) Debug.LogException(task.Exception);
                _cancellationTokenSource.Cancel();

                // Await each process separately
                try
                {
                    await receiveTask;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                try
                {
                    await sendTask;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }

}