#!/bin/bash
# Dựng BioWebViewMac.bundle (arm64 + x86_64) cho bản Mac của VERA Lab.
set -euo pipefail
cd "$(dirname "$0")"
OUT=../BioWebViewMac.bundle
rm -rf "$OUT"; mkdir -p "$OUT/Contents/MacOS"
cp Info.plist "$OUT/Contents/Info.plist"
clang++ -std=c++17 -fobjc-arc -O2 -bundle -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 \
  -framework Cocoa -framework WebKit BioWebViewMac.mm -o "$OUT/Contents/MacOS/BioWebViewMac"
echo "Xong: $OUT"
