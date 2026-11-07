#!/usr/bin/env bash
set -euo pipefail
script_dir="$(cd "$(dirname "$0")" && pwd)"
package_dir="$(cd "$script_dir/../.." && pwd)"
source_path="${1:-$package_dir/Plugins/iOS/Haptics.m}"
task_dir="$(mktemp -d "${TMPDIR:-/tmp}/haptics-stop.XXXXXX")"
trap 'rm -rf "$task_dir"' EXIT
printf 'Production source: %s\n' "$source_path"
shasum -a 256 "$source_path"
xcrun --sdk macosx clang -fobjc-arc -fblocks -Werror -Wno-unguarded-availability-new \
    -I "$script_dir" "-DHAPTICS_SOURCE=\"$source_path\"" "$script_dir/HapticsStopTest.m" \
    -framework Foundation -framework CoreHaptics -o "$task_dir/haptics-stop-tests"
"$task_dir/haptics-stop-tests"
