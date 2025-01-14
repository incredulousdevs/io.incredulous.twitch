﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Incredulous.Twitch
{
    /// <summary>
    /// A MonoBehaviour controller which manages a connection to the Twitch IRC server.
    /// </summary>
    public class TwitchController : MonoBehaviour
    {
        [field: Tooltip("The information used to authenticate with Twitch.")]
        [field: SerializeField] public TwitchCredentials Credentials { get; private set; }

        [field: Tooltip("Whether a connection to Twitch should be established on Start.")]
        [field: SerializeField] public bool ConnectOnStart { get; set; } = true;

        [field: Tooltip("Whether all IRC messages should be logged to the debug console.")]
        [field: SerializeField] public bool DebugIrc { get; set; }

        [field: Space]
        [field: Tooltip("An event which is triggered when a new chat message is received.")]
        [field: SerializeField] public ChatMessageUnityEvent ChatMessageEvent { get; private set; } = new ChatMessageUnityEvent();

        [field: Tooltip("An event which is triggered when a new bot command message is received.")]
        [field: SerializeField] public BotCommandMessageUnityEvent BotCommandMessageEvent { get; private set; } = new BotCommandMessageUnityEvent();

        [field: Tooltip("An event which is triggered when the connection status changes.")]
        [field: SerializeField] public ConnectionAlertUnityEvent ConnectionAlertEvent { get; private set; } = new ConnectionAlertUnityEvent();

        /// <summary>
        /// The underlying Twitch client.
        /// </summary>
        public TwitchClient Client { get; private set; }

        /// <summary>
        /// The client user's Twitch tags.
        /// </summary>
        public Tags ClientUserTags => Client?.ClientUserTags;

        /// <summary>
        /// Whether the Twitch client is successfully connected to Twitch.
        /// </summary>
        public bool IsConnected => Client?.ConnectionStatus == TwitchClient.Status.Connected;

        /// <summary>
        /// A queue for connection alerts.
        /// </summary>
        private readonly Queue<ConnectionAlert> _alertQueue = new Queue<ConnectionAlert>();

        /// <summary>
        /// A queue for incoming chat messages.
        /// </summary>
        private readonly Queue<ChatMessage> _chatMessageQueue = new Queue<ChatMessage>();

        /// <summary>
        /// A queue for incoming bot command messages.
        /// </summary>
        private readonly Queue<BotCommandMessage> _botCommandMessageQueue = new Queue<BotCommandMessage>();

        /// <summary>
        /// Whether the controller has been instructed to maintain a connection.
        /// </summary>
        private bool _shouldConnect;

        /// <summary>
        /// Connect to Twitch IRC.
        /// </summary>
        [ContextMenu("Connect")]
        public void Connect()
        {
            _shouldConnect = true;

            // Start the connection
            Client.Begin();
        }

        /// <summary>
        /// Disconnect from Twitch IRC.
        /// </summary>
        [ContextMenu("Disconnect")]
        public void Disconnect()
        {
            _shouldConnect = false;

            // Finish processing any pending information
            HandlePendingInformation();
            
            // End the connection
            Client.End();
        }

        /// <summary>
        /// Formats a message as a chat message and sends it to the IRC server.
        /// </summary>
        /// <param name="message">The message to send.</param>
        public void SendChatMessage(string message) => Client?.SendChatMessage(message);

        /// <summary>
        /// Queues a pre-formatted command to be sent to the IRC server.
        /// </summary>
        /// <param name="command">The pre-formatted command to send,</param>
        public void SendCommand(string command) => Client?.SendCommand(command);

        /// <summary>
        /// Sends a PING command to the Twitch IRC server.
        /// </summary>
        [ContextMenu("Ping")]
        public void Ping() => Client?.Ping();

        /// <summary>
        /// Sets the credentials used to connect to Twitch. This will cause the connection to be reset.
        /// </summary>
        /// <param name="credentials">The new credentials to use.</param>
        public void SetCredentials(TwitchCredentials credentials)
        {
            Client?.End();
            Credentials = credentials;
            SetupClient();
            if (_shouldConnect) Client.Begin();
        }

        private void Start()
        {
            SetupClient();
            if (ConnectOnStart) Connect();
        }

        private void Update()
        {
            HandlePendingInformation();
            Client.LogIrcMessages = DebugIrc;
        }

        private void OnDisable()
        {
            Disconnect();
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void SetupClient()
        {
            Client = new TwitchClient(Credentials);
            Client.LogIrcMessages = DebugIrc;
            Client.ChatMessageEvent += message => _chatMessageQueue.Enqueue(message);
            Client.BotCommandMessageEvent += message => _botCommandMessageQueue.Enqueue(message);
            Client.ConnectionAlertEvent += alert => _alertQueue.Enqueue(alert);
        }

        /// <summary>
        /// Handles pending information received from the current connection
        /// </summary>
        private void HandlePendingInformation()
        {
            while (_botCommandMessageQueue.Count > 0) BotCommandMessageEvent.Invoke(_botCommandMessageQueue.Dequeue());
            while (_chatMessageQueue.Count > 0) ChatMessageEvent.Invoke(_chatMessageQueue.Dequeue());
            while (_alertQueue.Count > 0) HandleConnectionAlert(_alertQueue.Dequeue());
        }

        /// <summary>
        /// Handles a connection alert and propogates it to any listeners.
        /// </summary>
        private void HandleConnectionAlert(ConnectionAlert alert)
        {
            switch (alert.status)
            {
                case ConnectionAlert.BAD_LOGIN:
                case ConnectionAlert.MISSING_LOGIN:
                case ConnectionAlert.NO_CONNECTION:
                case ConnectionAlert.CONNECTION_INTERRUPTED:
                    Debug.LogError(alert.message);
                    break;

                default:
                    Debug.Log(alert.message);
                    break;
            }
            ConnectionAlertEvent.Invoke(alert);
        }

        [Serializable]
        public class ChatMessageUnityEvent : UnityEvent<ChatMessage> { }

        [Serializable]
        public class BotCommandMessageUnityEvent : UnityEvent<BotCommandMessage> { }

        [Serializable]
        public class ConnectionAlertUnityEvent : UnityEvent<ConnectionAlert> { }
    }

}