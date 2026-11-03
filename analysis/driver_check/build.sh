#!/bin/sh
# Build the offline driver check against the OpenRGB checkout (fake hidapi, no device access)
set -e
cd "$(dirname "$0")"
OPENRGB=${OPENRGB:-$HOME/Projects/OpenRGB}
DRV=$OPENRGB/Controllers/SinowealthController/SinowealthKeyboard49Controller
g++ -std=c++17 -Wall -Wextra -I include -I "$DRV" -I "$OPENRGB" driver_check.cpp "$DRV/SinowealthKeyboard49Controller.cpp" -o driver_check
