#!/usr/bin/env python
import sys
import time
from absl import flags

# Configuration Constants
HOST = "127.0.0.1"
CONFIG_PORT = 14381
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
from pysc2.env import lan_sc2_env
from pysc2.env import sc2_env
from pysc2.lib import renderer_human
from pysc2.lib import features
from pysc2.lib import actions

FLAGS = flags.FLAGS
FLAGS(sys.argv)

def main():
    print(f"Connecting to Host at {HOST}:{CONFIG_PORT}...")
    
    # Define interface format for human play (needs raw data)
    interface_format = features.AgentInterfaceFormat(
        feature_dimensions=features.Dimensions(screen=84, minimap=64),
        # rgb_dimensions=features.Dimensions(screen=256, minimap=128),
        action_space=actions.ActionSpace.RAW,
        use_raw_units=True,
        use_feature_units=True
    )

    try:
        with lan_sc2_env.LanSC2Env(
            host=HOST,
            config_port=CONFIG_PORT,
            race=sc2_env.Race[USER_RACE],
            name=USER_NAME,
            step_mul=STEP_MUL,
            visualize=RENDER,
            agent_interface_format=interface_format
        ) as env:
            
            print("Connected to game! Running loop...")
            
            try:
                while True:
                    # Just step the environment to keep it alive
                    # In a real bot, you would get observations and send actions here
                    env.step([actions.FUNCTIONS.no_op()])
                    time.sleep(1/FPS)
            except KeyboardInterrupt:
                pass
            
    except KeyboardInterrupt:
        print("Interrupted.")
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    main()
