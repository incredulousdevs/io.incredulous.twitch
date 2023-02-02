# Twitch IRC for Unity

This is a lightweight [Twitch.tv IRC](https://dev.twitch.tv/docs/irc/) chat client for Unity.

Twitch IRC for Unity allows you to integrate Twitch chat with your Unity projects. This solution uses a WebSocket connection to send and receive messages to and from Twitch's IRC server.

**Note:** Only normal chat messages are currently supported. Whispers, subscriber messages, etc are not implemented. Unity WebGL is not supported.

## Installation

Twitch IRC for Unity can be installed via the package manager. You can install the package directly from its GitHub URL, or from your local disk by downloading the package.

## Requirements
1. Twitch Account
2. Twitch OAuth token (You can generate one at https://twitchapps.com/tmi/)

## Quick Start

1. Create a new empty GameObject and add the **TwitchController** component
2. In the inspector, enter your Twitch login information (Username, Channel, Token)
3. Make sure that "Connect on Start" is checked in the inspector and hit Play– you should see a successful `[JOIN]` message in your Unity Console!

To start handling chat messages, use the inspector to add a listener to the TwitchController's **ChatMessageEvent**. The listener will receive a **ChatMessage** object, which contains informaton about the chat message such as the message itself and the username of the sender.

# API
## TwitchController
A MonoBehaviour controller which manages a connection to the Twitch IRC server.
### Properties
- Credentials (`TwitchCredentials`) - The information used to authenticate with Twitch.
- ConnectOnStart (`bool`) - Whether a connection to Twitch should be established on Start.
- DebugIrc (`bool`) - Whether all IRC messages should be logged to the debug console.
- Client (`TwitchClient`) - The underlying Twitch client.
- ClientUserTags (`Tags`) - The client user's Twitch tags. (readonly)
- IsConnected (`bool`) - Whether the Twitch client is successfully connected to Twitch. (readonly)
### Unity Events
- ChatMessageEvent (`ChatMessageUnityEvent`) - An event which is triggered when a new chat message is received.
- BotCommandMessageEvent (`BotCommandMessageUnityEvent`) - An event which is triggered when a new bot command message is received.
- ConnectionAlertEvent (`ConnectionAlertUnityEvent`) - An event which is triggered when the connection status changes.
### Methods
- `void` Connect() - Connect to Twitch IRC.
- `void` Disconnect() - Disconnect from Twitch IRC.
- `void` SendChatMessage(`string` message) - Formats a message as a chat message and sends it to the IRC server.
- `void` SendCommand(`string` command) - Queues a pre-formatted command to be sent to the IRC server.
- `void` Ping() - Sends a PING command to the Twitch IRC server.
- `void` SetCredentials(`TwitchCredentials` credentials) - Sets the credentials used to connect to Twitch. This will cause the connection to be reset.

## TwitchClient
A class which manages a connection to the Twitch IRC server.
### Constructors
- TwitchClient(`TwitchCredentials` credentials)
### Properties
- Credentials (`TwitchCredentials`) - The credentials used to connect to Twitch.
- RateLimit (`RateLimit`) - The current rate limit determined by the server role assigned to the credentials.
- ConnectionStatus (`TwitchClient.Status`) - The status of the connection.
- ClientUserTags (`Tags`) - The client user's Twitch tags.
- LogIrcMessages (`bool`) - Whether all IRC message should be logged.
### Events
- ChatMessageEvent (`void (ChatMessage message)`) - An event which is triggered when a new chat message is received.
- BotCommandMessageEvent (`void (BotCommandMessage message)`) - An event which is triggered when a new bot command message is received.
- ConnectionAlertEvent (`void (ConnectionAlert connectionAlert)`) - An event which is triggered when the connection status changes.
### Methods
- `void` Begin() - Initalizes a connection to Twitch and starts the send, receive, and check connection threads.
- `void` End() - Closes the connection.
- `async Task` EndAsync() - Disconnects and waits for the connection to close.
- `void` Ping() - Queues a PING command to be sent to the IRC server.
- `void` SendCommand(`string` command) - Queues a pre-formatted command to be sent to the IRC server.
- `void` SendChatMessage(`string` message) - Queues a chat message to be sent to the IRC server.
### Enumerations
- Status - An enumeration of connection states for the Twtch IRC client.
  - Disconnected - The client is fully disconnected from the server.
  - Connecting - The client is attempting to connect to the server.
  - Connected - The client is fully connected to the server.
  - Disconnecting - The client is disconnecting from the server.
## ChatMessage
A normal chat message.
### Properties
- Login (`string`) - The login username of the sender.
- Channel (`string`) - The channel where this message was sent.
- Tags (`Tags`) - The Twitch tags associate with this message.
- Message (`string`) - The content of the chat message.
### Methods
- `Color` GetRGBAColor() - Gets the user's display color as a Unity color.
- `bool` ContainsEmote(`string` emote) - Returns whether the message contain a given emote.
- `bool` HasBadge(`string` badge) - Returns whether the message has a given badge.
## BotCommandMessage
A bot command message.
### Properties
- Login (`string`) - The login username of the sender.
- Channel (`string`) - The channel where this message was sent.
- Tags (`Tags`) - The Twitch tags associate with this message.
- CommandName (`string`) - The name of the command.
- CommandParams (`string`) - The optional string of parameters following the command.
### Methods
- `Color` GetRGBAColor() - Gets the user's display color as a Unity color.
- `bool` ContainsEmote(`string` emote) - Returns whether the message contain a given emote.
- `bool` HasBadge(`string` badge) - Returns whether the message has a given badge.