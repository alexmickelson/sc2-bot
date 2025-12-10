import sys
from absl import flags
from pysc2.env import sc2_env
from pysc2.lib import actions, features
import random
import time

# Define flags to avoid parsing errors if run directly
FLAGS = flags.FLAGS
FLAGS(sys.argv)

def main():
    print("Launching StarCraft II with bot interface...")
    
    try:
        with sc2_env.SC2Env(
            map_name="Simple64",
            players=[sc2_env.Agent(sc2_env.Race.terran),
                     sc2_env.Bot(sc2_env.Race.random, sc2_env.Difficulty.very_easy)],
            agent_interface_format=features.AgentInterfaceFormat(
                feature_dimensions=features.Dimensions(screen=84, minimap=64),
                use_feature_units=True),
            step_mul=8,
            game_steps_per_episode=0,
            visualize=True) as env: # visualize=True opens the feature layer viewer
            
            agent = None # We'll just do random actions
            
            print("Game launched! Press Ctrl+C to exit.")
            
            obs = env.reset()
            
            while True:
                # Simple random action loop
                step_actions = []
                for _ in obs:
                    # Just do a no-op or random action
                    # For simplicity, we'll just send no-ops to keep it running
                    step_actions.append(actions.FUNCTIONS.no_op())
                
                obs = env.step(step_actions)
                time.sleep(0.1) # Slow down slightly to make it watchable
                
    except KeyboardInterrupt:
        print("Exiting...")
    except Exception as e:
        print(f"Error launching SC2: {e}")
        print("Make sure SC2PATH is set correctly and Maps are installed.")

if __name__ == "__main__":
    main()
