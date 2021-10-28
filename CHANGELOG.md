# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased
### Added
- TwitchIRC.SendPing method (ContextMenu action) for debugging connection
- TwitchIRC.priorityOutputQueue for important messages which should be sent first
- TwitchIRC.taskQueue, a ConcurrentQueue&lt;System.Action> which lives on the main thread (replaces functionality of the MainThread class)
- TwitchIRC.BlockingDisconnect which blocks the main thread while any remainig send/receive threads close (important for closing connecting during OnDisable and OnDestroy)
- XML descriptions to several class members
### Changed
- Versioning in CHANGELOG to reflect that the API may still be unstable
- TwitchIRC.Disconnect is now a coroutine which yields for each thread to terminate before closing the TCPClient
- TwitchIRC.PrepareConnection now checks for a current connected status and yields for a disconnect. This prevents accidentally establishing multiple connections on a single TwitchIRC instance and has the added benefit of allowing TwitchIRC.IRC_Connect to also act as a "reconnect" method.
- TwitchIRC.connected is now a property which implements Interlocked.Exchange for thread safety
- TwitchIRC.outputQueue is now a ConcurrentQueue (thread safe)
- TwitchIRC.SendCommand no longer sends any messages instantly but uses priorityOutputQueue to send important messages first
- TwitchIRC.IRCInputProc
    - Now uses Socket.Available to check for new data
    - Retreives data using Socket.Receive
    - Received data is run through a UTF8 Decoder, then a StringBuilder
- Send/receive threads now sleep for a configurable number of milliseconds before reattempting sends/receives to reduce CPU usage
### Removed
- MainThread class and prefab (functionality has been intergrated into TwitchIRC class)

## [0.2.0] - 2021-10-26
### Added
- CHANGELOG.md
- package.json
- Lexonegit.UnityTwitchChat.asmdef
### Changed
- All scripts now belong to the Lexonegit.UnityTwitchChat namespace
### Removed
- Sample scene (temporarily)

## [0.1.0] - 2021-06-12
- Last release from lexonegit