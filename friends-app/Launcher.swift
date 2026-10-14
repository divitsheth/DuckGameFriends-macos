import Cocoa

// Room codes are eight characters from an alphabet without look-alikes (no 0/O, 1/I).
let roomAlphabet = Array("23456789ABCDEFGHJKLMNPQRSTUVWXYZ")
let defaultRelay = "duckfriends-relay.fly.dev:7777"

enum RoomStatus { case open, missing, full, unreachable }

// Asks the relay whether a room exists and has a free slot. Protocol: relay/relay.js.
func queryRoom(_ code: String, relay: String) -> RoomStatus {
    let parts = relay.split(separator: ":")
    guard parts.count == 2 else { return .unreachable }
    var hints = addrinfo(ai_flags: 0, ai_family: AF_INET, ai_socktype: SOCK_DGRAM, ai_protocol: 0, ai_addrlen: 0, ai_canonname: nil, ai_addr: nil, ai_next: nil)
    var info: UnsafeMutablePointer<addrinfo>?
    guard getaddrinfo(String(parts[0]), String(parts[1]), &hints, &info) == 0, let addr = info else { return .unreachable }
    defer { freeaddrinfo(info) }
    let fd = socket(AF_INET, SOCK_DGRAM, 0)
    guard fd >= 0 else { return .unreachable }
    defer { close(fd) }
    var timeout = timeval(tv_sec: 1, tv_usec: 0)
    setsockopt(fd, SOL_SOCKET, SO_RCVTIMEO, &timeout, socklen_t(MemoryLayout<timeval>.size))
    let query = [UInt8(0x51)] + Array(code.utf8)
    var reply = [UInt8](repeating: 0, count: 16)
    for _ in 0..<3 {
        _ = query.withUnsafeBytes { sendto(fd, $0.baseAddress, query.count, 0, addr.pointee.ai_addr, addr.pointee.ai_addrlen) }
        let n = recv(fd, &reply, reply.count, 0)
        if n == 10 && reply[0] == 0x52 && Array(reply[2..<10]) == Array(code.utf8) {
            return reply[1] == 0 ? .open : reply[1] == 1 ? .missing : .full
        }
    }
    return .unreachable
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    var window: NSWindow!
    let status = NSTextField(wrappingLabelWithString: "Host a game, or enter a friend’s room code.")
    let code = NSTextField(labelWithString: "")
    let room = NSTextField(string: "")
    var game: Process?
    let fm = FileManager.default
    let resources = Bundle.main.resourceURL!
    let relay = ProcessInfo.processInfo.environment["DUCK_FRIENDS_RELAY"] ?? defaultRelay
    var support: URL { fm.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/DuckGameFriends") }

    func label(_ text: String, _ size: CGFloat = 14) -> NSTextField {
        let v = NSTextField(wrappingLabelWithString: text)
        v.font = .systemFont(ofSize: size)
        v.preferredMaxLayoutWidth = 530
        return v
    }
    func button(_ title: String, _ action: Selector) -> NSButton {
        let b = NSButton(title: title, target: self, action: action)
        b.bezelStyle = .rounded
        return b
    }
    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.regular)
        window = NSWindow(contentRect: NSRect(x: 0,y: 0,width: 600,height: 480), styleMask: [.titled,.closable,.miniaturizable], backing: .buffered, defer: false)
        window.title = "Duck Game Friends"
        let stack = NSStackView()
        stack.orientation = .vertical; stack.alignment = .leading; stack.spacing = 14
        stack.translatesAutoresizingMaskIntoConstraints = false
        window.contentView!.addSubview(stack)
        NSLayoutConstraint.activate([stack.leadingAnchor.constraint(equalTo: window.contentView!.leadingAnchor, constant: 28), stack.trailingAnchor.constraint(equalTo: window.contentView!.trailingAnchor, constant: -28), stack.topAnchor.constraint(equalTo: window.contentView!.topAnchor, constant: 26)])
        stack.addArrangedSubview(label("Duck Game Friends", 30))
        stack.addArrangedSubview(label("A private room for up to four Macs.", 17))
        stack.addArrangedSubview(status)
        stack.addArrangedSubview(label("HOST A ROOM", 12))
        stack.addArrangedSubview(button("Host a game", #selector(host)))
        code.font = .monospacedSystemFont(ofSize: 28, weight: .semibold); code.isSelectable = true
        stack.addArrangedSubview(code)
        stack.addArrangedSubview(button("Copy room code", #selector(copyRoom)))
        stack.addArrangedSubview(label("JOIN A FRIEND", 12))
        room.placeholderString = "Room code, like K7QD-2MXH"
        room.font = .monospacedSystemFont(ofSize: 16, weight: .regular)
        room.widthAnchor.constraint(equalToConstant: 260).isActive = true
        room.target = self; room.action = #selector(join)
        let joinRow = NSStackView(views: [room, button("Join game", #selector(join))])
        joinRow.spacing = 10; stack.addArrangedSubview(joinRow)
        let other = NSStackView(views: [button("Play locally", #selector(local)), button("Quick guide", #selector(guide))])
        other.spacing = 10; stack.addArrangedSubview(other)
        stack.addArrangedSubview(label("Everyone joins the lobby before the host starts the match. The host keeps the game open while you play.", 12))
        let menu = NSMenu(); let appMenu = NSMenu(); let item = NSMenuItem()
        menu.addItem(item); item.submenu = appMenu
        appMenu.addItem(withTitle: "Quit Duck Game Friends", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        let edit = NSMenu(title: "Edit"); let editItem = NSMenuItem(); editItem.submenu = edit; menu.addItem(editItem)
        edit.addItem(withTitle: "Copy",action:#selector(NSText.copy(_:)),keyEquivalent:"c")
        edit.addItem(withTitle: "Paste",action:#selector(NSText.paste(_:)),keyEquivalent:"v")
        edit.addItem(withTitle: "Select All",action:#selector(NSText.selectAll(_:)),keyEquivalent:"a")
        NSApp.mainMenu = menu
        window.center(); window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true)
    }
    @objc func guide() { NSWorkspace.shared.open(resources.appendingPathComponent("Quick Start.html")) }
    @objc func copyRoom() {
        guard !code.stringValue.isEmpty else { return }
        NSPasteboard.general.clearContents(); NSPasteboard.general.setString(code.stringValue, forType: .string)
    }
    func error(_ text: String) { let a=NSAlert();a.messageText="Duck Game Friends";a.informativeText=text;a.runModal() }
    // Checks the relay off the main thread, then reports back on it.
    func check(_ room: String, then: @escaping (RoomStatus) -> Void) {
        status.stringValue = "Contacting the game server…"
        DispatchQueue.global().async { let result = queryRoom(room, relay: self.relay); DispatchQueue.main.async { then(result) } }
    }
    let unreachable = "Can’t reach the Duck Game Friends server. Check your internet connection and try again."
    @objc func host() {
        let raw = String((0..<8).map { _ in roomAlphabet.randomElement()! })
        check(raw) { result in
            guard result == .missing else { self.status.stringValue = ""; self.error(result == .unreachable ? self.unreachable : "Please try hosting again."); return }
            if self.launch("host", room: raw) {
                self.code.stringValue = raw.prefix(4) + "-" + raw.suffix(4); self.copyRoom()
                self.status.stringValue = "Your lobby is opening. The room code is copied. Send it to your friends."
            }
        }
    }
    @objc func join() {
        let raw = room.stringValue.uppercased().filter { $0.isLetter || $0.isNumber }
        guard raw.count == 8, raw.allSatisfy({ roomAlphabet.contains($0) }) else { error("Room codes are eight letters and numbers, like K7QD-2MXH. Ask the host to send it again."); return }
        check(raw) { result in
            switch result {
            case .open:
                if self.launch("join", room: raw) { self.status.stringValue = "Opening the game and joining your friend’s lobby." }
            case .missing: self.status.stringValue = ""; self.error("No game is open with that code. Check it with the host, and make sure their game is still running.")
            case .full: self.status.stringValue = ""; self.error("That room already has four players.")
            case .unreachable: self.status.stringValue = ""; self.error(self.unreachable)
            }
        }
    }
    @objc func local() { if launch("local", room: "") { status.stringValue = "Opening local play." } }
    func launch(_ mode: String, room: String) -> Bool {
        if let game, game.isRunning { error("A game is already running. Quit that game before hosting or joining another."); return false }
        do {
            try fm.createDirectory(at: support, withIntermediateDirectories: true)
            let version = try String(contentsOf: resources.appendingPathComponent("runtime-version.txt"), encoding: .utf8).trimmingCharacters(in:.whitespacesAndNewlines)
            let runtime = support.appendingPathComponent("game-"+version)
            if !fm.fileExists(atPath: runtime.path) {
                let staging=support.appendingPathComponent("install-"+UUID().uuidString)
                try fm.copyItem(at: resources.appendingPathComponent("game"), to: staging)
                try fm.moveItem(at: staging, to: runtime)
            }
            let config = ProcessInfo.processInfo.environment["XDG_CONFIG_HOME"].map { URL(fileURLWithPath: $0) } ?? fm.homeDirectoryForCurrentUser.appendingPathComponent(".config")
            let maps = config.appendingPathComponent("DuckGame/Levels/Divits Favorite Remixes")
            try fm.createDirectory(at: maps, withIntermediateDirectories: true)
            for file in try fm.contentsOfDirectory(at: resources.appendingPathComponent("Maps"),includingPropertiesForKeys:nil) where file.pathExtension == "lev" {
                let dest=maps.appendingPathComponent(file.lastPathComponent)
                if !fm.fileExists(atPath:dest.path) { try fm.copyItem(at:file,to:dest) }
            }
            let mono=resources.appendingPathComponent("Runtime")
            let p=Process();p.executableURL=mono.appendingPathComponent("bin/mono-sgen")
            p.arguments=[runtime.appendingPathComponent("FriendsBootstrap.exe").path,mode];p.currentDirectoryURL=runtime
            var env=ProcessInfo.processInfo.environment
            env["MONO_CFG_DIR"]=mono.appendingPathComponent("etc").path
            env["MONO_GAC_PREFIX"]=mono.path
            env["MONO_PATH"]=runtime.appendingPathComponent("OSX-Linux-x64").path
            env["DYLD_FALLBACK_LIBRARY_PATH"]=runtime.path+":"+mono.appendingPathComponent("lib").path
            env["DGR_SKIP_STEAMWORKS_COPY"]="1"
            env["DUCK_FRIENDS_ROOM"]=room;env["DUCK_FRIENDS_RELAY"]=relay
            env.removeValue(forKey:"DUCK_FRIENDS_TEST")
            p.environment=env
            let logs=fm.homeDirectoryForCurrentUser.appendingPathComponent("Library/Logs/DuckGameFriends")
            try fm.createDirectory(at:logs,withIntermediateDirectories:true)
            let log=logs.appendingPathComponent("game.log")
            fm.createFile(atPath:log.path,contents:nil)
            let handle=try FileHandle(forWritingTo:log);p.standardOutput=handle;p.standardError=handle
            p.terminationHandler={ process in
                try? handle.close()
                DispatchQueue.main.async {
                    self.status.stringValue = process.terminationStatus == 0 ? "Game closed. You can host, join, or play locally again." : "The game stopped unexpectedly. See Quick guide for the log location."
                }
            }
            try p.run();game=p
            return true
        } catch { self.error("Could not start the game: \(error.localizedDescription)"); return false }
    }
}
let app=NSApplication.shared
let delegate=AppDelegate();app.delegate=delegate
app.run()
