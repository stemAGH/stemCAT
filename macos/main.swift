import AppKit
import CoreGraphics
import ImageIO

struct Palette: Decodable { let name: String; let file: String }
struct Character: Decodable { let id: String; let name: String; let palettes: [Palette] }

// No typed text or key history is retained; only short-lived left/right activity.
struct Activity {
    var lastInput = 0.0
    var leftUntil = 0.0
    var rightUntil = 0.0
    var mouseUntil = 0.0
    var petting = false
    mutating func key(_ code: Int, now: Double) {
        let right = [0,1,2,3,5,6,7,8,9,11,12,13,14,15,17,18,19,20,21,23,50,48,49].contains(code)
        // Character faces the user: keyboard hands are mirrored.
        if right { rightUntil = now + 0.22 } else { leftUntil = now + 0.22 }
        lastInput = now
    }
    func pose(now: Double) -> Int {
        if petting { return 7 }
        if leftUntil > now && rightUntil > now { return 3 }
        if leftUntil > now { return 1 }
        if rightUntil > now { return 2 }
        if mouseUntil > now { return 4 }
        if now - lastInput > 45 { return 6 }
        if now - lastInput > 12 { return 5 }
        return 0
    }
}

func readCharacters(_ directory: URL) throws -> [Character] {
    let list = try JSONDecoder().decode([Character].self, from: Data(contentsOf: directory.appendingPathComponent("characters.json")))
    guard list.count == 18, Set(list.map { $0.id }).count == list.count, list.allSatisfy({ !$0.palettes.isEmpty }) else {
        throw NSError(domain: "stemCAT", code: 1, userInfo: [NSLocalizedDescriptionKey: "Invalid character catalog"])
    }
    return list
}

func frames(at url: URL) throws -> [NSImage] {
    guard let source = CGImageSourceCreateWithURL(url as CFURL, nil),
          let image = CGImageSourceCreateImageAtIndex(source, 0, nil), image.width == 2560, image.height == 1920 else {
        throw NSError(domain: "stemCAT", code: 2, userInfo: [NSLocalizedDescriptionKey: "Missing or damaged character artwork"])
    }
    var result: [NSImage] = []
    for row in 0..<8 { for column in 0..<8 {
        guard let crop = image.cropping(to: CGRect(x: CGFloat(column * 320), y: CGFloat(row * 240), width: 320, height: 240)) else {
            throw NSError(domain: "stemCAT", code: 3)
        }
        result.append(NSImage(cgImage: crop, size: NSSize(width: 320, height: 240)))
    }}
    return result
}

final class CompanionPanel: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}

final class CompanionView: NSView {
    weak var owner: AppDelegate?
    var images: [NSImage] = []
    var frameIndex = 0
    var green = false
    var press: NSPoint?
    var origin = NSPoint.zero
    override var isFlipped: Bool { true }
    override func draw(_ dirtyRect: NSRect) {
        if green { NSColor.green.setFill(); bounds.fill() }
        guard images.indices.contains(frameIndex) else { return }
        images[frameIndex].draw(in: bounds, from: .zero, operation: .sourceOver, fraction: 1, respectFlipped: true, hints: [.interpolation: NSImageInterpolation.high])
    }
    override func mouseDown(with event: NSEvent) {
        press = NSEvent.mouseLocation
        origin = window?.frame.origin ?? .zero
        let p = convert(event.locationInWindow, from: nil)
        owner?.activity.petting = p.y < bounds.height * 0.6
        owner?.activity.lastInput = ProcessInfo.processInfo.systemUptime
    }
    override func mouseDragged(with event: NSEvent) {
        guard let first = press else { return }
        let now = NSEvent.mouseLocation
        let dx = now.x - first.x, dy = now.y - first.y
        if abs(dx) + abs(dy) > 10 {
            owner?.activity.petting = false
            window?.setFrameOrigin(NSPoint(x: origin.x + dx, y: origin.y + dy))
        }
    }
    override func mouseUp(with event: NSEvent) {
        press = nil; owner?.activity.petting = false; owner?.savePosition()
    }
    override func rightMouseDown(with event: NSEvent) {
        guard let owner = owner else { return }
        NSMenu.popUpContextMenu(owner.makeMenu(), with: event, for: self)
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    let defaults = UserDefaults.standard
    var characters: [Character] = []
    var selected = 0
    var palette = 0
    var scale = 1.0
    var activity = Activity(lastInput: ProcessInfo.processInfo.systemUptime)
    var panel: CompanionPanel!
    var view: CompanionView!
    var status: NSStatusItem!
    var timer: Timer?
    var tap: CFMachPort?
    var tapSource: CFRunLoopSource?
    var mouseMonitor: Any?
    var lastPermissionAttempt = 0.0
    var resources: URL { Bundle.main.resourceURL!.appendingPathComponent("Characters") }

    func applicationDidFinishLaunching(_ notification: Notification) {
        if let id = Bundle.main.bundleIdentifier, NSRunningApplication.runningApplications(withBundleIdentifier: id).count > 1 {
            NSApp.terminate(nil); return
        }
        do { characters = try readCharacters(resources) }
        catch { showError(error); NSApp.terminate(nil); return }
        selected = characters.firstIndex { $0.id == defaults.string(forKey: "character") } ?? 0
        palette = max(0, min(defaults.integer(forKey: "palette"), characters[selected].palettes.count - 1))
        let savedScale = defaults.double(forKey: "scale")
        scale = savedScale > 0 ? min(2, max(0.6, savedScale)) : 1
        panel = CompanionPanel(contentRect: NSRect(x: 0, y: 0, width: 320 * scale, height: 240 * scale), styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        panel.isOpaque = false; panel.backgroundColor = .clear; panel.hasShadow = false
        panel.level = .floating; panel.hidesOnDeactivate = false
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.title = "stemCAT"
        let savedOpacity = defaults.double(forKey: "opacity")
        panel.alphaValue = savedOpacity > 0 ? min(1, max(0.3, savedOpacity)) : 1
        view = CompanionView(frame: NSRect(origin: .zero, size: panel.frame.size))
        view.owner = self; view.autoresizingMask = [.width, .height]
        view.green = defaults.bool(forKey: "green")
        panel.contentView = view
        loadCharacter()
        moveToCorner(nil)
        if defaults.object(forKey: "x") != nil {
            let proposed = NSRect(x: defaults.double(forKey: "x"), y: defaults.double(forKey: "y"), width: panel.frame.width, height: panel.frame.height)
            if NSScreen.screens.contains(where: { $0.visibleFrame.contains(proposed) }) { panel.setFrameOrigin(proposed.origin) }
        }
        panel.orderFrontRegardless()
        status = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        status.button?.title = "stemCAT"
        refreshMenu()
        mouseMonitor = NSEvent.addGlobalMonitorForEvents(matching: [.mouseMoved, .leftMouseDragged, .rightMouseDragged, .leftMouseDown, .rightMouseDown, .otherMouseDown, .scrollWheel]) { [weak self] event in
            guard let self = self else { return }
            let now = ProcessInfo.processInfo.systemUptime
            self.activity.lastInput = now
            self.activity.mouseUntil = now + 0.35
        }
        installKeyboardMonitor()
        timer = Timer.scheduledTimer(withTimeInterval: 1.0 / 15, repeats: true) { [weak self] _ in self?.tick() }
    }
    func showError(_ error: Error) {
        let alert = NSAlert(); alert.messageText = "stemCAT"; alert.informativeText = error.localizedDescription; alert.runModal()
    }
    func loadCharacter() {
        do {
            let images = try frames(at: resources.appendingPathComponent(characters[selected].palettes[palette].file))
            view.images = images; view.needsDisplay = true
            defaults.set(characters[selected].id, forKey: "character"); defaults.set(palette, forKey: "palette")
        } catch { showError(error) }
    }
    func tick() {
        let now = ProcessInfo.processInfo.systemUptime
        let index = activity.pose(now: now) * 8 + Int(now * 3.75) % 8
        if view.frameIndex != index { view.frameIndex = index; view.needsDisplay = true }
        if tap == nil && now - lastPermissionAttempt > 3 {
            lastPermissionAttempt = now; installKeyboardMonitor()
        }
    }
    func installKeyboardMonitor() {
        guard tap == nil, CGPreflightListenEventAccess() else { return }
        let mask = CGEventMask(1) << CGEventType.keyDown.rawValue
        tap = CGEvent.tapCreate(tap: .cgSessionEventTap, place: .headInsertEventTap, options: .listenOnly, eventsOfInterest: mask, callback: { _, type, event, info in
            guard let info = info else { return Unmanaged.passUnretained(event) }
            let app = Unmanaged<AppDelegate>.fromOpaque(info).takeUnretainedValue()
            if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
                if let tap = app.tap { CGEvent.tapEnable(tap: tap, enable: true) }
            } else if type == .keyDown {
                let code = Int(event.getIntegerValueField(.keyboardEventKeycode))
                if code == 37 && event.getIntegerValueField(.keyboardEventAutorepeat) == 0 && event.flags.contains(.maskControl) && event.flags.contains(.maskAlternate) {
                    app.toggleClickThrough(nil)
                }
                app.activity.key(code, now: ProcessInfo.processInfo.systemUptime)
            }
            return Unmanaged.passUnretained(event)
        }, userInfo: Unmanaged.passUnretained(self).toOpaque())
        if let tap = tap {
            tapSource = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0)
            if let source = tapSource { CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes) }
            CGEvent.tapEnable(tap: tap, enable: true)
            refreshMenu()
        }
    }
    func item(_ title: String, _ action: Selector?, tag: Int = 0) -> NSMenuItem {
        let item = NSMenuItem(title: title, action: action, keyEquivalent: "")
        item.target = self; item.tag = tag; return item
    }
    func makeMenu() -> NSMenu {
        let menu = NSMenu()
        let title = item("stemCAT · macOS beta", nil); title.isEnabled = false; menu.addItem(title)
        menu.addItem(.separator())
        let characterMenu = NSMenu()
        for (i, ch) in characters.enumerated() {
            let entry = item(ch.name, #selector(selectCharacter(_:)), tag: i)
            entry.state = i == selected ? .on : .off; characterMenu.addItem(entry)
        }
        let characterItem = item("Character", nil); characterItem.submenu = characterMenu; menu.addItem(characterItem)
        let paletteMenu = NSMenu()
        for (i, pal) in characters[selected].palettes.enumerated() {
            let entry = item(pal.name, #selector(selectPalette(_:)), tag: i)
            entry.state = i == palette ? .on : .off; paletteMenu.addItem(entry)
        }
        let paletteItem = item("Colours", nil); paletteItem.submenu = paletteMenu; menu.addItem(paletteItem)
        let sizes = NSMenu()
        for value in [75,100,125,150,200] { sizes.addItem(item("\(value)%", #selector(setSize(_:)), tag: value)) }
        let sizeItem = item("Size", nil); sizeItem.submenu = sizes; menu.addItem(sizeItem)
        let opacityMenu = NSMenu()
        for value in [40,60,80,100] { opacityMenu.addItem(item("\(value)%", #selector(setOpacity(_:)), tag: value)) }
        let opacity = item("Opacity", nil); opacity.submenu = opacityMenu; menu.addItem(opacity)
        menu.addItem(item("Move to bottom right", #selector(moveToCorner(_:))))
        let click = item("Click-through (Control + Option + L)", #selector(toggleClickThrough(_:)))
        click.state = panel.ignoresMouseEvents ? .on : .off; menu.addItem(click)
        let green = item("Green-screen background", #selector(toggleGreen(_:)))
        green.state = view.green ? .on : .off; menu.addItem(green)
        menu.addItem(.separator())
        menu.addItem(item(tap == nil ? "Enable keyboard reactions…" : "Keyboard reactions enabled", #selector(requestKeyboard(_:))))
        menu.addItem(item("About this Mac beta…", #selector(about(_:))))
        menu.addItem(item("Quit stemCAT", #selector(quit(_:))))
        return menu
    }
    func refreshMenu() { if status != nil { status.menu = makeMenu() } }
    @objc func selectCharacter(_ sender: NSMenuItem) { selected = sender.tag; palette = 0; loadCharacter(); refreshMenu() }
    @objc func selectPalette(_ sender: NSMenuItem) { palette = sender.tag; loadCharacter(); refreshMenu() }
    @objc func setSize(_ sender: NSMenuItem) {
        scale = Double(sender.tag) / 100
        panel.setContentSize(NSSize(width: 320 * scale, height: 240 * scale))
        defaults.set(scale, forKey: "scale"); savePosition()
    }
    @objc func setOpacity(_ sender: NSMenuItem) { panel.alphaValue = Double(sender.tag) / 100; defaults.set(panel.alphaValue, forKey: "opacity") }
    @objc func moveToCorner(_ sender: Any?) {
        guard let screen = NSScreen.screens.first(where: { $0.frame.contains(NSEvent.mouseLocation) }) ?? NSScreen.main else { return }
        let f = screen.visibleFrame
        panel.setFrameOrigin(NSPoint(x: f.maxX - panel.frame.width - 20, y: f.minY + 15)); if sender != nil { savePosition() }
    }
    func savePosition() { guard panel != nil else { return }; defaults.set(panel.frame.minX, forKey: "x"); defaults.set(panel.frame.minY, forKey: "y") }
    @objc func toggleClickThrough(_ sender: Any?) { panel.ignoresMouseEvents.toggle(); refreshMenu() }
    @objc func toggleGreen(_ sender: Any?) { view.green.toggle(); defaults.set(view.green, forKey: "green"); view.needsDisplay = true; refreshMenu() }
    @objc func requestKeyboard(_ sender: Any?) {
        if !CGPreflightListenEventAccess() {
            _ = CGRequestListenEventAccess()
            let alert = NSAlert(); alert.messageText = "Enable keyboard reactions"
            alert.informativeText = "Allow stemCAT in System Settings → Privacy & Security → Input Monitoring, then quit and reopen stemCAT. Only left/right typing activity is used; typed text is never collected or saved."
            alert.runModal()
        }
        installKeyboardMonitor()
    }
    @objc func about(_ sender: Any?) {
        let alert = NSAlert(); alert.messageText = "stemCAT for macOS · beta"
        alert.informativeText = "Eighteen companions and their colour presets. Drag to move; hold the head to pet.\n\nThis beta uses preset animation poses. Exact key positioning, pointer-following eyes, custom colour editing, keyboard flipping, and launch at login are Windows-only for now. No key labels are shown.\n\nNo network features or keystroke logs. Built for Intel and Apple silicon."
        alert.runModal()
    }
    @objc func quit(_ sender: Any?) { NSApp.terminate(nil) }
    func applicationWillTerminate(_ notification: Notification) {
        timer?.invalidate()
        if let monitor = mouseMonitor { NSEvent.removeMonitor(monitor) }
        if let source = tapSource { CFRunLoopRemoveSource(CFRunLoopGetMain(), source, .commonModes) }
        if let tap = tap { CFMachPortInvalidate(tap) }
        savePosition()
    }
}

func selfTest() throws {
    let base = Bundle.main.resourceURL!.appendingPathComponent("Characters")
    let chars = try readCharacters(base)
    for ch in chars { for pal in ch.palettes {
        let images = try frames(at: base.appendingPathComponent(pal.file))
        precondition(images.count == 64)
    }}
    var a = Activity(lastInput: 0)
    precondition(a.pose(now: 1) == 0 && a.pose(now: 13) == 5 && a.pose(now: 46) == 6)
    a.key(37, now: 50); precondition(a.pose(now: 50.1) == 1)
    a.key(12, now: 50); precondition(a.pose(now: 50.1) == 3)
    precondition(a.pose(now: 51) == 0)
    a.petting = true; precondition(a.pose(now: 52) == 7)
    a.petting = false; a.mouseUntil = 53; precondition(a.pose(now: 52) == 4)
    print("Catalog, all palette atlases, and activity-state checks passed.")
}

if CommandLine.arguments.contains("--self-test") {
    do { try selfTest() } catch { fputs("\(error)\n", stderr); exit(1) }
} else {
    let application = NSApplication.shared
    application.setActivationPolicy(.accessory)
    let delegate = AppDelegate()
    application.delegate = delegate
    withExtendedLifetime(delegate) { application.run() }
}
