namespace SC2Bot.Multiplayer;

// Configuration required for Player 1 (Host) to initialize and host a multiplayer game.
// This record encapsulates all settings used in the `play_host.py` script.
public record Player1Config(
  // Game Settings

  // The name of the StarCraft II map to play (e.g., "Simple64").
  // Used during the "Create Game" step to tell the SC2 process which map to load.
  string MapName = "Simple64",
  // Whether the game runs in realtime mode or stepped mode.
  // Used in `RequestCreateGame`. True for human-speed gameplay, False for fast training.
  bool Realtime = true,
  // Target frames per second for the game loop.
  // Used to control the loop speed in the main gameplay loop.
  float Fps = 22.4f,
  // Step multiplier. How many game steps to advance per agent step.
  // Used when stepping the environment in the gameplay loop.
  int StepMul = 1,
  // Whether to enable the PySC2 feature layer rendering.
  // Used when initializing the environment/agent interface.
  bool Render = false,
  // Player Settings

  // The name of Player 1.
  string UserName = "HostPlayer",
  // The race for Player 1 (e.g., "terran", "zerg", "protoss", "random").
  // Used in `RequestCreateGame` to set up the player participant.
  string UserRace = "terran",
  // Network Settings

  // The IP address to bind the configuration socket to (usually "0.0.0.0").
  // Used in "Host Initialization" to listen for Player 2's connection.
  string HostBindAddress = "0.0.0.0",
  // The public or LAN IP address of Player 1.
  // Sent to Player 2 or used for logging/verification.
  string HostIp = "127.0.0.1",
  // The expected IP address of Player 2.
  // Used to validate the incoming connection on the Configuration Port.
  string ClientIp = "127.0.0.1",
  // The starting TCP port for the handshake.
  // Used to listen for the initial connection from Player 2.
  int ConfigPort = 14381,
  // Port Configuration
  // These defaults follow the logic: ConfigPort + 1, + 2, etc.

  // Port used by the SC2 server instance to communicate game state.
  // The Game Port is responsible for synchronizing the game simulation between clients.
  // Passed to the SC2 process on launch and sent to Player 2 during settings transfer.
  int ServerGamePort = 14382,
  // Base port used by the SC2 server instance.
  // The Base Port is used for secondary communication, such as exchanging observations and actions.
  // Passed to the SC2 process on launch and sent to Player 2 during settings transfer.
  int ServerBasePort = 14383,
  // Port used by Player 1's SC2 client to communicate with the server.
  // The Game Port is responsible for synchronizing the game simulation between clients.
  // Passed to the SC2 process on launch.
  int Player1ClientGamePort = 14384,
  // Base port used by Player 1's SC2 client.
  // The Base Port is used for secondary communication, such as exchanging observations and actions.
  // Passed to the SC2 process on launch.
  int Player1ClientBasePort = 14385,
  // Proposed port for Player 2's SC2 client.
  // The Game Port is responsible for synchronizing the game simulation between clients.
  // Sent to Player 2 during settings transfer as a suggestion. Player 2 may override this.
  int Player2ClientGamePort = 14386,
  // Proposed base port for Player 2's SC2 client.
  // The Base Port is used for secondary communication, such as exchanging observations and actions.
  // Sent to Player 2 during settings transfer as a suggestion. Player 2 may override this.
  int Player2ClientBasePort = 14387
);
