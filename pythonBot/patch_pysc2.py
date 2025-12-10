#!/usr/bin/env python3
"""
Patch pysc2 to work with Python 3.13+
The random.shuffle() function no longer accepts a 'random' parameter in Python 3.13
"""

import os
import pysc2

def patch_colors_py():
    colors_path = os.path.join(os.path.dirname(pysc2.__file__), 'lib', 'colors.py')
    
    print(f"Patching: {colors_path}")
    
    with open(colors_path, 'r') as f:
        content = f.read()
    
    # Fix the random.shuffle call - remove the lambda parameter
    old_code = "random.shuffle(palette, lambda: 0.5)  # Return a fixed shuffle"
    new_code = "random.Random(42).shuffle(palette)  # Return a fixed shuffle"
    
    if old_code in content:
        content = content.replace(old_code, new_code)
        
        with open(colors_path, 'w') as f:
            f.write(content)
        
        print("✓ Successfully patched pysc2 for Python 3.13 compatibility")
    else:
        print("⚠ Pattern not found - file may already be patched or version mismatch")
        print("Checking if already using new syntax...")
        if "random.Random" in content and "shuffle(palette)" in content:
            print("✓ File appears to already be patched")
        else:
            print("✗ Unable to patch - please use Python 3.11 or 3.12 instead")

if __name__ == "__main__":
    patch_colors_py()
