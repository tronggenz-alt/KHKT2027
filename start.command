#!/bin/bash
DIR="$(cd "$(dirname "$0")" && pwd)"
exec "$DIR/scripts/macos/start.command" "$@"
