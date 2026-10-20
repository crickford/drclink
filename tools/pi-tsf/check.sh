#!/bin/bash
set -euo pipefail
task_dir=$(cd -- "$(dirname -- "$0")" && pwd)
test_dir=$(mktemp -d)
trap 'rm -rf -- "$test_dir"' EXIT
gcc -std=c99 -Wall -Wextra -Werror -fsanitize=address,undefined -g \
    "$task_dir/tests/test_protocol.c" -o "$test_dir/protocol"
"$test_dir/protocol"
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s "$task_dir/tests" -v
bash -n "$task_dir/inventory.sh" "$task_dir/build.sh"
