#!/usr/bin/env python
import sys
import time
import importlib
import socket
import portpicker
from absl import flags

# Configuration Constants
RENDER = False
REALTIME = True
MAP_NAME = "Simple64"
USER_NAME = "HostPlayer"
USER_RACE = "terran"
FPS = 22.4
STEP_MUL = 1
HOST = "127.0.0.1"
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
from s2clientprotocol import sc2api_pb2 as sc_pb

FLAGS = flags.FLAGS
FLAGS(sys.argv)

def main():
    print(f"Starting Host on {HOST}:{CONFIG_PORT}...")
    
    run_config = run_configs.get()
    map_inst = maps.get(MAP_NAME)
    
    # Reserve ports: 1 for config (already defined), 2 for server (game+base), 2 for client (game+base)
    # We need 4 ports starting from config_port + 1
    ports = [CONFIG_PORT + p for p in range(5)]
    
    # Check if ports are free (simple check)
    # In a real scenario, we might want to use portpicker to find free ports dynamically
    # but for simplicity we stick to the requested range or fail.
    
    proc = None
    tcp_conn = None
    
    try:
        # Start SC2 process
        print("Launching StarCraft II...")
        proc = run_config.start(extra_ports=ports[1:], timeout_seconds=300,
                                host=HOST, window_loc=(50, 50))
        
        tcp_port = ports[0]
        settings = {
            "map_name": map_inst.name,
            "map_path": map_inst.path,
            "map_data": map_inst.data(run_config),
            "game_version": proc.version.game_version,
            "realtime": REALTIME,
            "remote": False,
            "ports": {
                "server": {"game": ports[1], "base": ports[2]},
                "client": {"game": ports[3], "base": ports[4]},
            }
        }
        
        # Create Game
        print(f"Creating game on map {map_inst.name}...")
        create = sc_pb.RequestCreateGame(
            realtime=settings["realtime"],
            local_map=sc_pb.LocalMap(map_path=settings["map_path"]))
        create.player_setup.add(type=sc_pb.Participant) # Host
        create.player_setup.add(type=sc_pb.Participant) # Client
        
        controller = proc.controller
        controller.save_map(settings["map_path"], settings["map_data"])
        controller.create_game(create)
        
        print("-" * 80)
        print(f"Game Created. Waiting for opponent to join...")
        print(f"Run on client: python join_host.py --host <HOST_IP> --config_port {tcp_port}")
        print("-" * 80)
        
        # Start TCP Server to exchange settings with client
        tcp_conn = lan_sc2_env.tcp_server(
            lan_sc2_env.Addr(HOST, tcp_port), settings)
        
        print("Opponent connected! Joining game...")
        
        # Join Game
        join = sc_pb.RequestJoinGame()
        join.shared_port = 0 
        join.server_ports.game_port = settings["ports"]["server"]["game"]
        join.server_ports.base_port = settings["ports"]["server"]["base"]
        join.client_ports.add(game_port=settings["ports"]["client"]["game"],
                              base_port=settings["ports"]["client"]["base"])
        
        join.race = sc2_env.Race[USER_RACE]
        join.player_name = USER_NAME
        
        # Setup rendering options
        join.options.raw = True
        join.options.score = True
        join.options.raw_affects_selection = True
        join.options.raw_crop_to_playable_area = True
        join.options.show_cloaked = True
        join.options.show_burrowed_shadows = True
        join.options.show_placeholders = True
        
        controller.join_game(join)
        
        print("Game joined. Running loop...")
        
        # Simple loop to keep the game running without rendering
        try:
            while True:
                controller.observe()
                controller.step(1)
                time.sleep(1/FPS)
        except KeyboardInterrupt:
            pass
        
    except KeyboardInterrupt:
        print("Interrupted.")
    except Exception as e:
        print(f"Error: {e}")
    finally:
        if tcp_conn:
            tcp_conn.close()
        if proc:
            proc.close()

if __name__ == "__main__":
    main()
