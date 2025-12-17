# Multiplayer Setup Steps for SC2 Bot

This document outlines the sequence of operations, connections, and messages exchanged between **Player 1** (running `play_host.py`) and **Player 2** (running `join_host.py`) to establish a multiplayer StarCraft II game.

## 1. Player 1 Initialization (`play_host.py`)

1.  **Configuration Port Setup**:
    *   Player 1 selects a **Configuration Port** (default: 14381).
    *   Player 1 binds a TCP socket to `0.0.0.0:Configuration Port` and listens for incoming connections.

2.  **SC2 Launch & Port Assignment**:
    *   Player 1 reserves a block of ports for the game instance.
    *   **Server Game Port** (e.g., 14382) and **Server Base Port** (e.g., 14383): Used by the SC2 server instance to communicate game state.
    *   **Player 1 Client Game Port** (e.g., 14384) and **Player 1 Client Base Port** (e.g., 14385): Used by Player 1's SC2 client to communicate with the server.
    *   Player 1 launches the StarCraft II process, passing these ports to it.

3.  **Connect Controller**: Player 1 connects its `RemoteController` to the local SC2 API.

4.  **Create Game**: Player 1 sends a `RequestCreateGame` to SC2.
    *   Sets map, realtime mode, and participant types (Player 1 & Player 2).

5.  **Wait for Player 2**: Player 1 blocks, waiting for a TCP connection on the **Configuration Port**.

## 2. Player 2 Initialization (`join_host.py`)

1.  **SSH Tunneling (Optional)**: If Player 1 is remote, Player 2 sets up an SSH tunnel.
    *   Forwards the **Configuration Port**, **Server Ports**, and **Player 1 Client Ports** from Local -> Remote.
    *   Forwards the **Player 2 Client Ports** (see below) from Remote -> Local.

2.  **Connect to Player 1**: Player 2 establishes a TCP connection to Player 1's IP at the **Configuration Port**.

## 3. Handshake & Configuration Exchange

Once the TCP connection is established on the **Configuration Port**:

### Step 3.1: Map Data Transfer (Player 1 -> Player 2)
1.  **Player 1** sends the size of the map data (4 bytes).
2.  **Player 1** sends the raw map data.
3.  **Player 2** reads the size, then reads the full map data.

### Step 3.2: Settings Transfer (Player 1 -> Player 2)
1.  **Player 1** prepares a settings dictionary. This includes:
    *   Map details and game version.
    *   The assigned **Server Ports** and **Player 1 Client Ports**.
    *   Proposed **Player 2 Client Ports** (e.g., 14386, 14387).
2.  **Player 1** sends the size of the JSON-encoded settings (4 bytes).
3.  **Player 1** sends the JSON-encoded settings string.
4.  **Player 2** reads and decodes the settings.

### Step 3.3: Port Confirmation (Player 2 -> Player 1)
1.  **Player 2** determines which local ports to use for itself: **Player 2 Client Game Port** and **Player 2 Client Base Port**.
    *   It can use the proposed ports from Player 1.
    *   Or it can override them with local flags.
2.  **Player 2** sends the JSON-encoded dictionary of these actual ports back to Player 1.
3.  **Player 1** reads Player 2's port configuration.

## 4. Game Join

### Player 1 Side
1.  **Player 1** sends `RequestJoinGame` to its local SC2 instance.
    *   Uses **Server Ports** for the server configuration.
    *   Uses **Player 1 Client Ports** for its own client connection.

### Player 2 Side
1.  **Player 2** launches its own StarCraft II process.
2.  **Player 2** connects its `RemoteController` to its local SC2 API.
3.  **Player 2** sends `RequestJoinGame` to its local SC2 instance.
    *   Connects to the **Server Ports** (tunnelled or direct).
    *   Uses **Player 2 Client Ports** for its own client connection.

## 5. Gameplay Loop

1.  Both Player 1 and Player 2 enter their main loops.
2.  SC2 handles synchronization using the established port connections.
