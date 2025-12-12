#!/usr/bin/env python
import sys
import time
import socket
import struct
import json
from absl import flags

# Configuration Constants
GAME_HOST = "144.17.71.47"  # Remote game server
CONFIG_PORT = 14000
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
from s2clientprotocol import sc2api_pb2 as sc_pb

FLAGS = flags.FLAGS
FLAGS(sys.argv)

def connect_to_host(ip, port):
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
        print("Settings received successfully.")
        return sock, settings
        
    except Exception as e:
        print(f"Connection failed: {e}")
        return None, None

def main():
    print(f"Connecting to game host at {GAME_HOST}:{CONFIG_PORT}...")
    
    run_config = run_configs.get()
    proc = None
    tcp_conn = None
    settings = None
    
    try:
        # Get game settings from remote host via TCP
        tcp_conn, settings = connect_to_host(GAME_HOST, CONFIG_PORT)
        
        if not settings:
            print("Could not get settings from host. Trying to join with default ports...")
            # Fallback: Assume default ports if handshake fails
            # This assumes the host is running and waiting for join on these ports
            # and NOT waiting for the TCP handshake (which is unlikely if it's play_host.py)
            # But if the user is running a different host, this might work.
            settings = {
                "map_name": "Unknown",
                "ports": {
                    "server": {"game": CONFIG_PORT + 1, "base": CONFIG_PORT + 2},
                    "client": {"game": CONFIG_PORT + 3, "base": CONFIG_PORT + 4},
                }
            }
        else:
            print(f"Received settings from host:")
            print(f"  Map: {settings['map_name']}")
            print(f"  Ports - Server: {settings['ports']['server']}, Client: {settings['ports']['client']}")
        
        # Start local SC2 process
        print("Launching local StarCraft II client...")
        proc = run_config.start(
            extra_ports=[settings["ports"]["client"]["game"], settings["ports"]["client"]["base"]],
            timeout_seconds=300,
            host="127.0.0.1",
            window_loc=(50, 50)
        )
        
        # Join the game
        print("Joining multiplayer game...")
        join = sc_pb.RequestJoinGame()
        join.shared_port = 0
        
        # Use the ports from the host
        join.server_ports.game_port = settings["ports"]["server"]["game"]
        join.server_ports.base_port = settings["ports"]["server"]["base"]
        join.client_ports.add(
            game_port=settings["ports"]["client"]["game"],
            base_port=settings["ports"]["client"]["base"]
        )
        
        # Set player info
        join.race = sc2_env.Race[USER_RACE]
        join.player_name = USER_NAME
        join.host_ip = GAME_HOST # Important for remote play
        
        # Setup interface options
        join.options.raw = True
        join.options.score = True
        join.options.raw_affects_selection = True
        join.options.raw_crop_to_playable_area = True
        join.options.show_cloaked = True
        join.options.show_burrowed_shadows = True
        join.options.show_placeholders = True
        
        controller = proc.controller
        controller.join_game(join)
        
        print("Successfully joined game! Running game loop...")
        
        # Game loop
        try:
            while True:
                controller.observe()
                controller.step(STEP_MUL)
                time.sleep(1/FPS)
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
