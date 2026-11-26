#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
assets="${1:-macos/Assets}"
if [[ ! -f "$assets/characters.json" ]]; then
  echo 'Missing character artwork. Export it with tools/ExportMacAssets.cs on Windows first.' >&2
  exit 1
fi
mkdir -p dist/stemCAT.app/Contents/{MacOS,Resources/Characters} dist/icon.iconset
cp macos/Info.plist dist/stemCAT.app/Contents/Info.plist
cp "$assets/"*.png "$assets/characters.json" dist/stemCAT.app/Contents/Resources/Characters/
for arch in arm64 x86_64; do
  xcrun swiftc -swift-version 5 -O -framework AppKit -framework CoreGraphics -framework ImageIO \
    -target "$arch-apple-macos12.0" macos/main.swift -o "dist/stemCAT-$arch"
done
lipo -create dist/stemCAT-arm64 dist/stemCAT-x86_64 -output dist/stemCAT.app/Contents/MacOS/stemCAT
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$assets/icon.png" --out "dist/icon.iconset/icon_${size}x${size}.png" >/dev/null
  double=$((size * 2))
  sips -z "$double" "$double" "$assets/icon.png" --out "dist/icon.iconset/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns dist/icon.iconset -o dist/stemCAT.app/Contents/Resources/stemCAT.icns
# Ad-hoc signing enables local execution; this is not Developer ID signing or notarization.
codesign --force --deep --sign - dist/stemCAT.app
codesign --verify --deep --strict dist/stemCAT.app
lipo -verify_arch arm64 x86_64 dist/stemCAT.app/Contents/MacOS/stemCAT
dist/stemCAT.app/Contents/MacOS/stemCAT --self-test
ditto -c -k --sequesterRsrc --keepParent dist/stemCAT.app dist/stemCAT-macOS-universal.zip
(cd dist && shasum -a 256 stemCAT-macOS-universal.zip > SHA256SUMS-macOS.txt)
