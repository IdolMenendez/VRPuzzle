#!/bin/bash
set -euo pipefail

if [[ $# -lt 2 || $# -gt 3 ]]; then
  echo "usage: $0 <action> <absolute_output_prefix> [numeric_argument]" >&2
  exit 64
fi

ACTION="$1"
PREFIX="$2"
ARGUMENT="${3:-}"
SEND="$(cd "$(dirname "$0")" && pwd)/send_player_manual_step.sh"

mkdir -p "$(dirname "$PREFIX")"
if [[ -n "$ARGUMENT" ]]; then
  "$SEND" "$ACTION" "$ARGUMENT" > "${PREFIX}_ack.txt" 2>&1 &
else
  "$SEND" "$ACTION" > "${PREFIX}_ack.txt" 2>&1 &
fi
SEND_PID=$!

sleep 0.16
screencapture -x -R0,34,788,816 "${PREFIX}_T016.png"
sleep 0.30
screencapture -x -R0,34,788,816 "${PREFIX}_T046.png"
sleep 0.46
screencapture -x -R0,34,788,816 "${PREFIX}_T092.png"
wait "$SEND_PID"
cat "${PREFIX}_ack.txt"
