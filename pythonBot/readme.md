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

### Example with Flags

```powershell
# Host with custom settings (running on 144.17.71.47)
$env:PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION="python"; uv run --python 3.11 play_host.py --host_ip "144.17.71.47" --client_ip "144.17.71.76" --map_name "Simple64" --user_name "Player1" --user_race "zerg"

# Join with custom settings (running on 144.17.71.76, connecting to host)
$env:PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION="python"; uv run --python 3.11 join_host.py --game_host "144.17.71.47" --client_ip "144.17.71.76" --user_name "Player2" --user_race "protoss"
```

### Linux Usage

```bash
export PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION=python && python play_host.py

export PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION=python && python join_host.py
```