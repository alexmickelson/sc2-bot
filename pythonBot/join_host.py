#!/usr/bin/env python
import sys
import time
import socket
import struct
import json
import os
from absl import flags

# Define command-line flags
flags.DEFINE_string("game_host", "127.0.0.1", "Remote game server IP")
flags.DEFINE_string("client_ip", "127.0.0.1", "This machine's IP")
flags.DEFINE_integer("config_port", 14381, "Configuration port for connecting to host")
flags.DEFINE_integer("local_game_port", 14390, "Local game port (0 to use host-assigned)")
flags.DEFINE_integer("local_base_port", 14391, "Local base port (0 to use host-assigned)")
flags.DEFINE_string("user_name", "JoinPlayer", "Player name")
flags.DEFINE_string("user_race", "zerg", "Player race (terran/zerg/protoss)")
flags.DEFINE_float("fps", 22.4, "Frames per second")
flags.DEFINE_integer("step_mul", 1, "Step multiplier")
flags.DEFINE_bool("render", False, "Enable rendering")

# Configuration Constants (defaults, can be overridden by flags)
GAME_HOST = "127.0.0.1"  # Remote game server
CLIENT_IP = "127.0.0.1"  # This machine's IP
CONFIG_PORT = 14381
LOCAL_GAME_PORT = 14390  # Override ports for this client (set to 0 to use host-assigned ports)
LOCAL_BASE_PORT = 14391
USER_NAME = "JoinPlayer"
USER_RACE = "zerg"
FPS = 22.4
STEP_MUL = 1
RENDER = False

# Patch pysc2 for Python 3.13+ compatibility
try:
    from patch_pysc2 import patch_colors_py
    patch_colors_py()
except Exception as e:
    print(f"Warning: Could not apply patch: {e}")

from pysc2 import run_configs
from pysc2.env import sc2_env
from pysc2.lib import renderer_human
from pysc2.lib import features
from pysc2.lib import actions
from pysc2.lib import remote_controller
from s2clientprotocol import sc2api_pb2 as sc_pb

FLAGS = flags.FLAGS
FLAGS(sys.argv)

def write_tcp(conn, msg):
    conn.sendall(struct.pack("@I", len(msg)))
    conn.sendall(msg)

def connect_to_host(ip, port, local_game_port, local_base_port):
    print(f"Attempting to connect to {ip}:{port}...")
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.settimeout(120) # 120 second timeout to allow for host game creation
    try:
        sock.connect((ip, port))
        print("Connected to host! Waiting for settings (this may take a minute if host is starting)...")
        
        # Read map data size
        print("Waiting for map size...")
        size_data = b""
        while len(size_data) < 4:
            chunk = sock.recv(4 - len(size_data))
            if not chunk: raise Exception("Connection closed while reading map size")
            size_data += chunk
            
        size = struct.unpack("@I", size_data)[0]
        print(f"Map size: {size} bytes. Receiving map data...")
        
        # Read map data
        map_data = b""
        while len(map_data) < size:
            chunk = sock.recv(size - len(map_data))
            if not chunk: raise Exception("Incomplete map data")
            map_data += chunk
        print("Map data received.")
            
        # Read settings size
        print("Waiting for settings size...")
        size_data = b""
        while len(size_data) < 4:
            chunk = sock.recv(4 - len(size_data))
            if not chunk: raise Exception("Connection closed while reading settings size")
            size_data += chunk
            
        size = struct.unpack("@I", size_data)[0]
        
        # Read settings
        print(f"Settings size: {size} bytes. Receiving settings...")
        settings_data = b""
        while len(settings_data) < size:
            chunk = sock.recv(size - len(settings_data))
            if not chunk: raise Exception("Incomplete settings data")
            settings_data += chunk
            
        settings = json.loads(settings_data.decode())
        settings["map_data"] = map_data
        
        # Override ports if configured
        if local_game_port != 0:
            settings["ports"]["client_join"]["game"] = local_game_port
        if local_base_port != 0:
            settings["ports"]["client_join"]["base"] = local_base_port
            
        # Send back the ports we are using
        print(f"Sending client ports to host: {settings['ports']['client_join']}")
        write_tcp(sock, json.dumps(settings["ports"]["client_join"]).encode())
        
        print("Settings received successfully.")
        return sock, settings
        
    except Exception as e:
        print(f"Connection failed: {e}")
        return None, None

def main():
    # Use flag values if provided, otherwise use defaults
    game_host = FLAGS.game_host
    client_ip = FLAGS.client_ip
    config_port = FLAGS.config_port
    local_game_port = FLAGS.local_game_port
    local_base_port = FLAGS.local_base_port
    user_name = FLAGS.user_name
    user_race = FLAGS.user_race
    fps = FLAGS.fps
    step_mul = FLAGS.step_mul
    render = FLAGS.render
    
    print(f"Connecting to game host at {game_host}:{config_port} from {client_ip}...")
    
    run_config = run_configs.get()
    proc = None
    tcp_conn = None
    settings = None
    
    try:
        # Get game settings from remote host via TCP
        tcp_conn, settings = connect_to_host(game_host, config_port, local_game_port, local_base_port)
        
        if not settings:
            print("Could not get settings from host. Trying to join with default ports...")
            # Fallback: Assume default ports if handshake fails
            # This assumes the host is running and waiting for join on these ports
            settings = {
                "map_name": "Unknown",
                "ports": {
                    "server": {"game": config_port + 1, "base": config_port + 2},
                    "client_host": {"game": config_port + 3, "base": config_port + 4},
                    "client_join": {"game": config_port + 5, "base": config_port + 6},
                }
            }
        else:
            print(f"Received settings from host:")
            print(f"  Map: {settings['map_name']}")
            print(f"  Ports: {settings['ports']}")
        
        # Start local SC2 process
        print("Launching local StarCraft II client...")
        # Bind to 0.0.0.0 so we can accept remote connections for the game
        # But connect=False so we can manually connect to localhost
        proc = run_config.start(
            extra_ports=[settings["ports"]["client_join"]["game"], settings["ports"]["client_join"]["base"]],
            timeout_seconds=300,
            host="0.0.0.0",
            window_loc=(50, 50),
            connect=False
        )
        
        # Manually connect the controller to localhost
        print("Connecting to SC2 API on localhost...")
        proc._controller = remote_controller.RemoteController(
            "127.0.0.1", proc._port, proc, timeout_seconds=300)
        
        controller = proc.controller
        print(f"Saving map to {os.path.basename(settings['map_path'])}...")
        controller.save_map(os.path.basename(settings["map_path"]), settings["map_data"])
       
        # Join the game
        print("Joining multiplayer game...")
        join = sc_pb.RequestJoinGame()
        join.shared_port = 0
        
        # Use the ports from the host
        join.server_ports.game_port = settings["ports"]["server"]["game"]
        join.server_ports.base_port = settings["ports"]["server"]["base"]
        
        # Add client ports for Host (first) and Joiner (second)
        join.client_ports.add(game_port=settings["ports"]["client_host"]["game"],
                              base_port=settings["ports"]["client_host"]["base"])
        join.client_ports.add(game_port=settings["ports"]["client_join"]["game"],
                              base_port=settings["ports"]["client_join"]["base"])
        
        # Set player info
        join.race = sc2_env.Race[user_race]
        join.player_name = user_name
        join.host_ip = game_host
        
        # Setup interface options
        join.options.raw = True
        join.options.score = True
        join.options.raw_affects_selection = True
        join.options.raw_crop_to_playable_area = True
        join.options.show_cloaked = True
        join.options.show_burrowed_shadows = True
        join.options.show_placeholders = True
        
        controller.join_game(join)
        
        print("Successfully joined game! Waiting for game start...")
        
        # Wait for game to start (all players joined)
        while True:
            if controller.status == sc_pb.Status.in_game:
                print("Game started!")
                break
            controller.ping() # Keep connection alive and update status
            time.sleep(0.5)
            
        print("Running game loop...")
        
        # Game loop
        try:
            while True:
                controller.observe()
                controller.step(step_mul)
                time.sleep(1/fps)
        except KeyboardInterrupt:
            pass
            
    except KeyboardInterrupt:
        print("Interrupted.")
    except Exception as e:
        print(f"Error: {e}")
        import traceback
        traceback.print_exc()
    finally:
        if tcp_conn:
            tcp_conn.close()
        if proc:
            proc.close()

if __name__ == "__main__":
    main()
