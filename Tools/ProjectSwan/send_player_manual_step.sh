#!/bin/bash
set -euo pipefail

ADB_DEFAULT="/Volumes/APFS/模拟器/0709/swan_spaceos_pico_emulator_mac_20260703/platform-tools/adb"
ADB="${ADB:-$ADB_DEFAULT}"
PACKAGE="com.bytedance.personallypuzzled.xr"
FILES="/sdcard/Android/data/$PACKAGE/files"
COMMAND="$FILES/project_swan_player_manual_step.command"
ACK="$FILES/project_swan_player_manual_step.ack"

ACTION="${1:-}"
ARGUMENT="${2:-}"
if [[ ! "$ACTION" =~ ^[a-z_]+$ ]]; then
  echo "usage: $0 <status|confirm|selection_next|selection_previous|hint|grab_next|place_held|heavy_grab_left|heavy_grab_right|heavy_place|showcase_rotate|archive_push> [numeric_argument]" >&2
  exit 64
fi
if [[ -n "$ARGUMENT" && ! "$ARGUMENT" =~ ^-?[0-9]+([.][0-9]+)?$ ]]; then
  echo "argument must be numeric" >&2
  exit 64
fi

REQUEST_ID="$(date +%s%N)-$$"
PAYLOAD="$REQUEST_ID|$ACTION|$ARGUMENT"

"$ADB" shell mkdir -p "$FILES"
"$ADB" shell rm -f "$ACK" "$COMMAND.tmp"
"$ADB" shell "printf '%s\\n' '$PAYLOAD' > '$COMMAND.tmp' && mv '$COMMAND.tmp' '$COMMAND'"

for _ in $(seq 1 100); do
  if "$ADB" shell test -f "$ACK"; then
    RESPONSE="$("$ADB" shell cat "$ACK" | tr -d '\r')"
    case "$RESPONSE" in
      "$REQUEST_ID|ok|"*)
        printf '%s\n' "$RESPONSE"
        exit 0
        ;;
      "$REQUEST_ID|rejected|"*)
        printf '%s\n' "$RESPONSE" >&2
        exit 2
        ;;
    esac
  fi
  sleep 0.10
done

echo "timeout waiting for Project Swan PlayerManual ack: $REQUEST_ID" >&2
exit 3
