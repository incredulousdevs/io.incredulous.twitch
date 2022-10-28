using System;
using System.IO;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Incredulous.Twitch
{

    public partial class TwitchConnection
    {
        private ConcurrentQueue<string> priorityOutputQueue = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> outputQueue = new ConcurrentQueue<string>();

        /// <summary>
        /// The IRC output process which will run on the send thread.
        /// </summary>
        private async Task SendProcess(CancellationToken cancellationToken)
        {
            var stream = _tcpClient.GetStream();
            RateLimit rateLimit;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Send prioritized outputs
                    while (priorityOutputQueue.TryDequeue(out var priorityOutput))
                    {
                        if (LogIrcMessages) ConnectionAlertEvent?.Invoke(new ConnectionAlert(0, "<color=#c91b00><b>[IRC OUTPUT]</b></color> Sending command: " + priorityOutput));
                        stream.WriteLine(priorityOutput);
                    }

                    // If there are no queued messages, wait before checking again
                    if (outputQueue.Count == 0)
                    {
                        await Task.Delay(100);
                        continue;
                    }

                    // Update the rate limit
                    lock (_rateLimitLock) rateLimit = _chatRateLimit;

                    // Clear timestamps that are older than the rate limit period
                    var minTime = DateTime.Now - rateLimit.timeSpan;
                    while (_outputTimestamps.TryPeek(out var next) && next < minTime) _outputTimestamps.TryDequeue(out _);

                    // Send outputs up to the rate limit
                    while (_outputTimestamps.Count < rateLimit.count && outputQueue.TryDequeue(out var output))
                    {
                        if (LogIrcMessages) ConnectionAlertEvent?.Invoke(new ConnectionAlert(0, "<color=#c91b00><b>[IRC OUTPUT]</b></color> Sending command: " + output));
                        //if (debugIRC) Debug.Log("<color=#c91b00><b>[IRC OUTPUT]</b></color> Sending command: " + output);
                        stream.WriteLine(output);
                        _outputTimestamps.Enqueue(DateTime.Now);
                    }
                }
            }
            catch (IOException ex)
            {
                Debug.LogError("Error while writing to NetworkStream.");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Sends a ping to the server.
        /// </summary>
        public void Ping() => SendCommand("PING :tmi.twitch.tv");

        /// <summary>
        /// Queues a command to be sent to the IRC server. Prioritzed commands will be sent without regard for rate limits.
        /// </summary>
        public void SendCommand(string command, bool prioritized = false)
        {
            // For non-prioritized outputs, send a warning if the output will surpass the rate limit
            if (!prioritized)
            {
                lock (_rateLimitLock)
                {
                    if (_outputTimestamps.Count + 1 > _chatRateLimit.count)
                        ConnectionAlertEvent?.Invoke(ConnectionAlert.RateLimitWarning);
                }
            }

            // Place command in respective queue
            var queue = prioritized ? priorityOutputQueue : outputQueue;
            queue.Enqueue(command);
        }

        /// <summary>
        /// Sends a chat message.
        /// </summary>
        public void SendChatMessage(string message)
        {
            // Message can't be empty
            if (message.Length <= 0) return;

            // Send a warning if the output will surpass the rate limit
            lock (_rateLimitLock)
            {
                if (_outputTimestamps.Count >= _chatRateLimit.count)
                    ConnectionAlertEvent?.Invoke(ConnectionAlert.RateLimitWarning);
            }

            // Place message in queue
            outputQueue.Enqueue("PRIVMSG #" + Channel.ToLower() + " :" + message);
        }
    }

}