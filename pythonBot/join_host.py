#!/usr/bin/env python
import sys
import time
from absl import app, flags, logging

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
flags.DEFINE_string("host", "127.0.0.1", "Host IP address.")
flags.DEFINE_integer("config_port", 14380, "Host config port.")
flags.DEFINE_string("user_name", "JoinPlayer", "Name of the human player.")
flags.DEFINE_enum("user_race", "zerg", sc2_env.Race._member_names_, "User's race.")
flags.DEFINE_float("fps", 22.4, "Frames per second to run the game.")
flags.DEFINE_integer("step_mul", 1, "Game steps per agent step.")
flags.DEFINE_bool("render", True, "Whether to render with pygame.")

def main(unused_argv):
    print(f"Connecting to Host at {FLAGS.host}:{FLAGS.config_port}...")
    
    # Define interface format for human play (needs raw data)
    interface_format = features.AgentInterfaceFormat(
        feature_dimensions=features.Dimensions(screen=84, minimap=64),
        rgb_dimensions=features.Dimensions(screen=256, minimap=128),
        action_space=actions.ActionSpace.RAW,
        use_raw_units=True,
        use_feature_units=True
    )

    try:
        with lan_sc2_env.LanSC2Env(
            host=FLAGS.host,
            config_port=FLAGS.config_port,
            race=sc2_env.Race[FLAGS.user_race],
            name=FLAGS.user_name,
            step_mul=FLAGS.step_mul,
            visualize=FLAGS.render,
            agent_interface_format=interface_format
        ) as env:
            
            print("Connected to game! Starting renderer...")
            
            renderer = renderer_human.RendererHuman(
                fps=FLAGS.fps, render_feature_grid=False)
            
            # We need to pass the controller to the renderer
            # LanSC2Env wraps the controller, we can access it via env.controller
            
            # We also need map_name, which LanSC2Env knows but doesn't expose directly easily
            # except via env._map_name if available, or we can just pass "Unknown" as it's mostly for display
            map_name = getattr(env, "_map_name", "Unknown Map")
            
            renderer.run(run_configs.get(), env.controller, map_name, max_episodes=1)
            
    except KeyboardInterrupt:
        print("Interrupted.")
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    app.run(main)
