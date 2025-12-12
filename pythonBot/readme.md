launch with 


export PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION=python && /home/alex/projects/sc2/.venv/bin/python /home/alex/projects/sc2/pythonBot/play_host.py



$env:PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION="python"; uv run --python 3.11 play_host.py

$env:PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION="python"; uv run --python 3.11 join_host.py