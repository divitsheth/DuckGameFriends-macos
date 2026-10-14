import Cocoa

final class AppDelegate: NSObject, NSApplicationDelegate {
    var window: NSWindow!
    let status = NSTextField(wrappingLabelWithString: "Checking Tailscale…")
    let address = NSTextField(string: "")
    let room = NSTextField(string: "")
    var ownIP: String?
    var game: Process?
    let fm = FileManager.default
    let resources = Bundle.main.resourceURL!
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
        window = NSWindow(contentRect: NSRect(x: 0,y: 0,width: 600,height: 570), styleMask: [.titled,.closable,.miniaturizable], backing: .buffered, defer: false)
        window.title = "Duck Game Friends"
        let stack = NSStackView()
        stack.orientation = .vertical; stack.alignment = .leading; stack.spacing = 14
        stack.translatesAutoresizingMaskIntoConstraints = false
        window.contentView!.addSubview(stack)
        NSLayoutConstraint.activate([stack.leadingAnchor.constraint(equalTo: window.contentView!.leadingAnchor, constant: 28), stack.trailingAnchor.constraint(equalTo: window.contentView!.trailingAnchor, constant: -28), stack.topAnchor.constraint(equalTo: window.contentView!.topAnchor, constant: 26)])
        stack.addArrangedSubview(label("Duck Game Friends", 30))
        stack.addArrangedSubview(label("A private room for up to four Macs.", 17))
        stack.addArrangedSubview(status)
        let setup = NSStackView(views: [button("Set up Tailscale", #selector(setupNetwork)),button("Refresh", #selector(refresh)),button("Quick guide", #selector(guide))])
        setup.spacing = 10; stack.addArrangedSubview(setup)
        stack.addArrangedSubview(label("HOST A ROOM", 12))
        stack.addArrangedSubview(button("Host four-player game", #selector(host)))
        address.isEditable = false; address.isSelectable = true; address.placeholderString = "Your room code appears here"
        address.widthAnchor.constraint(equalToConstant: 530).isActive = true
        stack.addArrangedSubview(address)
        stack.addArrangedSubview(button("Copy room code", #selector(copyRoom)))
        stack.addArrangedSubview(label("JOIN A FRIEND", 12))
        room.placeholderString = "Paste your friend’s room code"
        room.stringValue = UserDefaults.standard.string(forKey: "lastRoom") ?? ""
        room.widthAnchor.constraint(equalToConstant: 530).isActive = true
        stack.addArrangedSubview(room)
        stack.addArrangedSubview(button("Join game", #selector(join)))
        stack.addArrangedSubview(button("Play locally", #selector(local)))
        stack.addArrangedSubview(label("Everyone must join the same Tailscale network once.\nFive custom maps are included. The host stays online while you play.", 12))
        let menu = NSMenu(); let appMenu = NSMenu(); let item = NSMenuItem()
        menu.addItem(item); item.submenu = appMenu
        appMenu.addItem(withTitle: "Quit Duck Game Friends", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        let edit = NSMenu(title: "Edit"); let editItem = NSMenuItem(); editItem.submenu = edit; menu.addItem(editItem)
        edit.addItem(withTitle: "Copy",action:#selector(NSText.copy(_:)),keyEquivalent:"c")
        edit.addItem(withTitle: "Paste",action:#selector(NSText.paste(_:)),keyEquivalent:"v")
        edit.addItem(withTitle: "Select All",action:#selector(NSText.selectAll(_:)),keyEquivalent:"a")
        NSApp.mainMenu = menu
        window.center(); window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true)
        refresh()
    }
    @objc func refresh() {
        ownIP = nil
        let binary = "/Applications/Tailscale.app/Contents/MacOS/Tailscale"
        guard fm.isExecutableFile(atPath: binary) else { status.stringValue = "Step 1: install Tailscale, then sign in."; return }
        status.stringValue = "Checking your private connection…"
        DispatchQueue.global().async {
            let p = Process(); let pipe = Pipe()
            p.executableURL = URL(fileURLWithPath: binary); p.arguments = ["status", "--json"]
            p.standardOutput = pipe; p.standardError = FileHandle.nullDevice
            do {
                try p.run()
                DispatchQueue.global().asyncAfter(deadline: .now()+8) { if p.isRunning { p.terminate() } }
                let bytes = pipe.fileHandleForReading.readDataToEndOfFile(); p.waitUntilExit()
                let json = (try? JSONSerialization.jsonObject(with: bytes)) as? [String: Any]
                let ips = json?["TailscaleIPs"] as? [String] ?? []
                let ip = ips.first(where: { $0.contains(".") })
                let running = json?["BackendState"] as? String == "Running"
                DispatchQueue.main.async {
                    self.ownIP = running ? ip : nil
                    self.status.stringValue = running && ip != nil ? "Connected to Tailscale · \(ip!)" : "Finish Tailscale sign-in and connect, then click Refresh."
                }
            } catch { DispatchQueue.main.async { self.status.stringValue = "Open Tailscale and finish setup, then click Refresh." } }
        }
    }
    @objc func setupNetwork() {
        if fm.fileExists(atPath: "/Applications/Tailscale.app") { NSWorkspace.shared.open(URL(fileURLWithPath: "/Applications/Tailscale.app")) }
        else { NSWorkspace.shared.open(URL(string: "https://tailscale.com/download/mac")!) }
    }
    @objc func guide() { NSWorkspace.shared.open(resources.appendingPathComponent("Quick Start.html")) }
    @objc func copyRoom() {
        guard !address.stringValue.isEmpty else { return }
        NSPasteboard.general.clearContents(); NSPasteboard.general.setString(address.stringValue, forType: .string)
    }
    func error(_ text: String) { let a=NSAlert();a.messageText="Duck Game Friends";a.informativeText=text;a.runModal() }
    @objc func host() {
        guard let ip = ownIP else { error("Connect Tailscale first, then click Refresh."); return }
        let password = UUID().uuidString.replacingOccurrences(of: "-", with: "")
        let code = "duckfriends://\(ip):1337/\(password)"
        if launch("host", endpoint: "", password: password) { address.stringValue = code; copyRoom() }
    }
    @objc func join() {
        guard ownIP != nil else { error("Connect Tailscale first, then click Refresh."); return }
        let code = room.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)
        guard let u = URLComponents(string: code), u.scheme == "duckfriends", let host = u.host, u.port == 1337, u.query == nil, u.fragment == nil, u.user == nil, u.password == nil else { error("Paste the full room code copied from your friend’s launcher."); return }
        let parts = host.split(separator: ".")
        let secret = String(u.path.dropFirst())
        guard parts.count == 4, parts.allSatisfy({ UInt8($0) != nil }), parts[0] == "100", let second=UInt8(parts[1]), second >= 64, second <= 127, secret.count == 32, secret.allSatisfy({ $0.isHexDigit }) else { error("That room code is invalid. Ask the host to copy it again."); return }
        if launch("join",endpoint:"\(host):1337",password:secret) { UserDefaults.standard.set(code,forKey:"lastRoom") }
    }
    @objc func local() { _ = launch("local",endpoint:"",password:"") }
    func launch(_ mode: String, endpoint: String, password: String) -> Bool {
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
            env["DUCK_FRIENDS_ADDRESS"]=endpoint;env["DUCK_FRIENDS_PASSWORD"]=password
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
            status.stringValue = mode == "host" ? "Opening your lobby. Share the copied room code with your friends." : mode == "join" ? "Opening the game and joining your friend’s lobby." : "Opening local play."
            return true
        } catch { self.error("Could not start the game: \(error.localizedDescription)"); return false }
    }
}
let app=NSApplication.shared
let delegate=AppDelegate();app.delegate=delegate
app.run()
