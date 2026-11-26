# stemCAT for macOS — beta 1

Download `stemCAT-macOS-universal.zip`, extract it, and move `stemCAT.app` to Applications before launching. Requires macOS 12 or later; includes Apple silicon and Intel binaries.

This is an early Mac port, not full Windows feature parity. It includes all twelve characters and their colour presets, a floating desktop companion, keyboard/mouse activity reactions, idle/sleep/petting poses, dragging, size, opacity, click-through, and green-screen background.

Use the **stemCAT menu-bar item** or right-click the companion for controls. Choose **Enable keyboard reactions** to request Input Monitoring permission. Enable stemCAT in System Settings → Privacy & Security → Input Monitoring, then quit and reopen it. If permission is declined, idle animations and local interactions remain available. The menu-bar control always lets you turn click-through off; Control + Option + L also works when keyboard monitoring is enabled.

The beta uses pre-rendered poses from the Windows artwork. It does not yet offer exact per-key positioning, key labels, pointer-following eyes, live custom colour editing, keyboard flipping, launch at login, or distinct mouse-button/scroll animations. It retains only short-lived activity flags, not typed text, and has no network features.

**Distribution status:** ad-hoc signed, not signed with an Apple Developer ID or notarized. macOS Gatekeeper may block downloaded builds. This is a testing build, not a verified Mac installer; do not disable Gatekeeper. A Developer ID and notarization are still needed for normal consumer distribution.

Build checks verify both architectures, code-signature integrity, every character/palette atlas, and activity-state logic. Real-Mac testing of the overlay, permissions, multiple displays, and input hooks is still needed.
