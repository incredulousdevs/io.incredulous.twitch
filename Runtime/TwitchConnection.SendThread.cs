using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Incredulous.Twitch
{
    public partial class TwitchConnection
    {
        private Queue<string> _outputQueue = new Queue<string>();
        private Queue<DateTime> _outputTimestamps = new Queue<DateTime>();

        /// <summary>
        /// The IRC output process which will run on the send thread.
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
            catch (IOException ex)
            {
                Debug.LogError("Error while writing to NetworkStream.");
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

        private void Send(string value)
        {
            // Warn if the output will surpass the rate limit
            if (_outputTimestamps.Count + 1 > RateLimit.Count) ConnectionAlertEvent?.Invoke(ConnectionAlert.RateLimitWarning);

            // Place message in queue
            _outputQueue.Enqueue(value);

            // Start the send process if it is not running
            if (_sendProcess == null || _sendProcess.IsCompleted) _sendProcess = SendProcess(_cancellationTokenSource.Token);
        }
    }
}