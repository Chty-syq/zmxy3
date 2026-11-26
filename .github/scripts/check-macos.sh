#!/usr/bin/env bash
set -euo pipefail

if [[ $# == 0 ]]; then
  test_command=(npm --prefix macos test -- --foreground-test)
else
  mount=$(mktemp -d)
  hdiutil attach "$1" -mountpoint "$mount" -nobrowse -readonly
  trap 'hdiutil detach "$mount"' EXIT
  app="$mount/造梦工坊.app"
  codesign --verify --deep --strict "$app"
  executable="$app/Contents/MacOS/造梦工坊"
  lipo "$executable" -verify_arch arm64 x86_64
  test_command=("$executable" --integration-test --foreground-test)
fi

log="$RUNNER_TEMP/zmxy-macos-test.log"
if ! "${test_command[@]}" 2>&1 | tee "$log"; then
  # Show the failing assertion in the public job summary as well as the full log.
  message=$(tail -n 18 "$log" | sed 's/%/%25/g' | awk 'NR>1 {printf "%%0A"} {printf "%s", $0}')
  echo "::error::$message"
  exit 1
fi
