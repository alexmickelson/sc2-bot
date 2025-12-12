## Launch Commands

### Basic Usage

**Host (start a game):**
```powershell
$env:PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION="python"; uv run --python 3.11 play_host.py
```

**Client (join a game):**
```powershell
$env:PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION="python"; uv run --python 3.11 join_host.py
```

### Command-Line Flags

Both scripts now support command-line flags for UI control:

#### play_host.py flags:
- `--render` - Enable rendering (default: False)
- `--realtime` - Run in realtime mode (default: True)
- `--map_name` - Map to play on (default: "Simple64")
- `--user_name` - Player name (default: "HostPlayer")
- `--user_race` - Player race: terran/zerg/protoss (default: "terran")
- `--fps` - Frames per second (default: 22.4)
- `--step_mul` - Step multiplier (default: 1)
- `--host` - Host address to bind to (default: "0.0.0.0")
- `--host_ip` - Host IP address (default: "127.0.0.1")
- `--client_ip` - Expected client IP (default: "127.0.0.1")
- `--sc2_host` - SC2 host address (default: "127.0.0.1")
- `--config_port` - Configuration port (default: 14381)

#### join_host.py flags:
- `--game_host` - Remote game server IP (default: "127.0.0.1")
- `--client_ip` - This machine's IP (default: "127.0.0.1")
- `--config_port` - Configuration port (default: 14381)
- `--local_game_port` - Local game port, 0 for host-assigned (default: 14390)
- `--local_base_port` - Local base port, 0 for host-assigned (default: 14391)
- `--user_name` - Player name (default: "JoinPlayer")
- `--user_race` - Player race: terran/zerg/protoss (default: "zerg")
- `--fps` - Frames per second (default: 22.4)
- `--step_mul` - Step multiplier (default: 1)
- `--render` - Enable rendering (default: False)

### Example with Flags

```powershell
# Host with custom settings
$env:PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION="python"; uv run --python 3.11 play_host.py --map_name "AcropolisLE" --user_name "Player1" --user_race "zerg"

# Join with custom settings
$env:PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION="python"; uv run --python 3.11 join_host.py --game_host "192.168.1.100" --user_name "Player2" --user_race "protoss"
```

### Linux Usage

```bash
export PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION=python && python play_host.py

export PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION=python && python join_host.py
```