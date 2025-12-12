#!/usr/bin/env python
import sys
import time
import importlib
import socket
import portpicker
import json
import struct
import os
from absl import flags

# Define command-line flags
flags.DEFINE_bool("render", False, "Enable rendering")
flags.DEFINE_bool("realtime", True, "Run game in realtime mode")
flags.DEFINE_string("map_name", "Simple64", "Map to play on")
flags.DEFINE_string("user_name", "HostPlayer", "Player name")
flags.DEFINE_string("user_race", "terran", "Player race (terran/zerg/protoss)")
flags.DEFINE_float("fps", 22.4, "Frames per second")
flags.DEFINE_integer("step_mul", 1, "Step multiplier")
flags.DEFINE_string("host", "0.0.0.0", "Host address to bind to")
flags.DEFINE_string("host_ip", "127.0.0.1", "Host IP address")
flags.DEFINE_string("client_ip", "127.0.0.1", "Expected client IP address")
flags.DEFINE_string("sc2_host", "127.0.0.1", "SC2 host address")
flags.DEFINE_integer("config_port", 14381, "Configuration port")

# Configuration Constants (defaults, can be overridden by flags)
RENDER = False
REALTIME = True
MAP_NAME = "Simple64"
USER_NAME = "HostPlayer"
USER_RACE = "terran"
FPS = 22.4
STEP_MUL = 1
HOST = "0.0.0.0"
HOST_IP = "127.0.0.1"
CLIENT_IP = "127.0.0.1"
SC2_HOST = "127.0.0.1"
CONFIG_PORT = 14381

# Patch pysc2 for Python 3.13+ compatibility
try:
    from patch_pysc2 import patch_colors_py
    patch_colors_py()
except Exception as e:
    print(f"Warning: Could not apply patch: {e}")

from pysc2 import maps
from pysc2 import run_configs
from pysc2.env import lan_sc2_env
from pysc2.env import sc2_env
from pysc2.lib import renderer_human
from pysc2.lib import remote_controller
from s2clientprotocol import sc2api_pb2 as sc_pb

FLAGS = flags.FLAGS
FLAGS(sys.argv)

def write_tcp(conn, msg):
    conn.sendall(struct.pack("@I", len(msg)))
    conn.sendall(msg)

def read_tcp(conn):
    size_data = b""
    while len(size_data) < 4:
        chunk = conn.recv(4 - len(size_data))
        if not chunk: raise Exception("Connection closed while reading size")
        size_data += chunk
    size = struct.unpack("@I", size_data)[0]
    
    data = b""
    while len(data) < size:
        chunk = conn.recv(size - len(data))
        if not chunk: raise Exception("Incomplete data")
        data += chunk
    return data

def main():
    # Use flag values if provided, otherwise use defaults
    render = FLAGS.render
    realtime = FLAGS.realtime
    map_name = FLAGS.map_name
    user_name = FLAGS.user_name
    user_race = FLAGS.user_race
    fps = FLAGS.fps
    step_mul = FLAGS.step_mul
    host = FLAGS.host
    host_ip = FLAGS.host_ip
    client_ip = FLAGS.client_ip
    sc2_host = FLAGS.sc2_host
    config_port = FLAGS.config_port
    
    print(f"Starting Host on {host}:{config_port}...")
    
    # Bind early to ensure port is open
    server_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    try:
        server_sock.bind((host, config_port))
        server_sock.listen(1)
        print(f"Listening on {host}:{config_port}...")
    except Exception as e:
        print(f"Failed to bind to {host}:{config_port}: {e}")
        return

    run_config = run_configs.get()
    map_inst = maps.get(map_name)
    
    # Reserve ports: 1 for config, 2 for server, 2 for host client, 2 for join client
    # We need 7 ports starting from config_port
    ports = [config_port + p for p in range(7)]
    
    # Check if ports are free (simple check)
    # In a real scenario, we might want to use portpicker to find free ports dynamically
    # but for simplicity we stick to the requested range or fail.
    
    proc = None
    tcp_conn = None
    
    try:
        # Start SC2 process
        print("Launching StarCraft II...")
        # Bind to 0.0.0.0 so we can accept remote connections for the game
        # But connect=False so we can manually connect to localhost (avoiding 0.0.0.0 connection issues)
        # Bind server ports AND host client ports
        proc = run_config.start(extra_ports=ports[1:5], timeout_seconds=300,
                                host="0.0.0.0", window_loc=(50, 50), connect=False)
        
        # Manually connect the controller to localhost
        print("Connecting to SC2 API on localhost...")
        proc._controller = remote_controller.RemoteController(
            "127.0.0.1", proc._port, proc, timeout_seconds=300)
            
        print(f"StarCraft II launched. Version: {proc.version.game_version}")
        
        tcp_port = ports[0]
        settings = {
            "map_name": map_inst.name,
            "map_path": os.path.basename(map_inst.path),
            "map_data": map_inst.data(run_config),
            "game_version": proc.version.game_version,
            "realtime": realtime,
            "remote": False,
            "ports": {
                "server": {"game": ports[1], "base": ports[2]},
                "client_host": {"game": ports[3], "base": ports[4]},
                "client_join": {"game": ports[5], "base": ports[6]},
            }
        }
        
        # Create Game
        print(f"Creating game on map {map_inst.name}...")
        create = sc_pb.RequestCreateGame(
            realtime=realtime,
            local_map=sc_pb.LocalMap(map_path=settings["map_path"]))
        create.player_setup.add(type=sc_pb.Participant) # Host
        create.player_setup.add(type=sc_pb.Participant) # Client
        
        controller = proc.controller
        controller.save_map(settings["map_path"], settings["map_data"])
        controller.create_game(create)
        print("Game created successfully.")
        
        print("-" * 80)
        print(f"Waiting for opponent to join on {host}:{config_port}...")
        print(f"Run on client: python join_host.py --game_host <HOST_IP> --config_port {tcp_port}")
        print("-" * 80)
        
        # Accept connection
        conn, addr = server_sock.accept()
        print(f"Opponent connected from {addr}!")
        if addr[0] != client_ip:
            print(f"Warning: Connection from unexpected IP {addr[0]}. Expected {client_ip}.")
        
        tcp_conn = conn
        
        # Send map data
        print(f"Sending map data ({len(settings['map_data'])} bytes)...")
        write_tcp(conn, settings["map_data"])
        
        # Send settings (excluding map_data to save space/complexity in JSON)
        send_settings = {k: v for k, v in settings.items() if k != "map_data"}
        print(f"Sending settings: {send_settings}")
        write_tcp(conn, json.dumps(send_settings).encode())
        
        # Wait for client to confirm ports
        print("Waiting for client port confirmation...")
        client_ports_data = read_tcp(conn)
        client_ports = json.loads(client_ports_data.decode())
        print(f"Client confirmed ports: {client_ports}")
        
        # Update settings with actual client ports
        settings["ports"]["client_join"] = client_ports
        
        print("Settings sent. Joining game...")
        
        # Join Game
        join = sc_pb.RequestJoinGame()
        join.shared_port = 0 
        join.server_ports.game_port = settings["ports"]["server"]["game"]
        join.server_ports.base_port = settings["ports"]["server"]["base"]
        
        # Add client ports for Host (first) and Joiner (second)
        join.client_ports.add(game_port=settings["ports"]["client_host"]["game"],
                              base_port=settings["ports"]["client_host"]["base"])
        join.client_ports.add(game_port=settings["ports"]["client_join"]["game"],
                              base_port=settings["ports"]["client_join"]["base"])
        
        join.race = sc2_env.Race[user_race]
        join.player_name = user_name
        join.host_ip = host_ip
        
        # Setup rendering options
        join.options.raw = True
        join.options.score = True
        join.options.raw_affects_selection = True
        join.options.raw_crop_to_playable_area = True
        join.options.show_cloaked = True
        join.options.show_burrowed_shadows = True
        join.options.show_placeholders = True
        
        controller.join_game(join)
        
        print("Game joined. Waiting for other players...")
        
        # Wait for game to start (all players joined)
        while True:
            if controller.status == sc_pb.Status.in_game:
                print("Game started!")
                break
            controller.ping() # Keep connection alive and update status
            time.sleep(0.5)
        
        print("Running loop...")
        
        # Simple loop to keep the game running without rendering
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
    finally:
        if tcp_conn:
            tcp_conn.close()
        if server_sock:
            server_sock.close()
        if proc:
            proc.close()

if __name__ == "__main__":
    main()
