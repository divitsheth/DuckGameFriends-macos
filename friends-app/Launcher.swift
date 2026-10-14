import Cocoa

// Starts the game straight into the Duck Game Friends menu, where players host,
// join with a room code, or play locally (FriendsMenu.cs). This launcher has no
// window of its own: it installs the game runtime, starts it, brings it back to
// the front if the app is opened again, and reports a crash.
let defaultRelay = "duckfriends-relay.fly.dev:7777"

final class AppDelegate: NSObject, NSApplicationDelegate {
    var game: Process?
    let fm = FileManager.default
    let resources = Bundle.main.resourceURL!
    let relay = ProcessInfo.processInfo.environment["DUCK_FRIENDS_RELAY"] ?? defaultRelay
    var support: URL { fm.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/DuckGameFriends") }
    var log: URL { fm.homeDirectoryForCurrentUser.appendingPathComponent("Library/Logs/DuckGameFriends/game.log") }

    func applicationDidFinishLaunching(_ notification: Notification) {
        let menu = NSMenu(); let item = NSMenuItem(); let appMenu = NSMenu()
        menu.addItem(item); item.submenu = appMenu
        appMenu.addItem(withTitle: "Quit Duck Game Friends", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        NSApp.mainMenu = menu
        do { try launch() } catch { fail("Could not start the game: \(error.localizedDescription)") }
    }
    // Opening the app again while the game runs brings the game forward.
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows: Bool) -> Bool { focusGame(); return false }
    func applicationDidBecomeActive(_ notification: Notification) { focusGame() }
    func applicationWillTerminate(_ notification: Notification) { if let game, game.isRunning { game.terminate() } }
    func focusGame() {
        guard let game, game.isRunning else { return }
        NSRunningApplication(processIdentifier: game.processIdentifier)?.activate()
    }
    func fail(_ text: String) {
        NSApp.setActivationPolicy(.regular); NSApp.activate(ignoringOtherApps: true)
        let a = NSAlert(); a.messageText = "Duck Game Friends"; a.informativeText = text; a.runModal()
        NSApp.terminate(nil)
    }
    func launch() throws {
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
        p.arguments=[runtime.appendingPathComponent("FriendsBootstrap.exe").path,"menu"];p.currentDirectoryURL=runtime
        var env=ProcessInfo.processInfo.environment
        env["MONO_CFG_DIR"]=mono.appendingPathComponent("etc").path
        env["MONO_GAC_PREFIX"]=mono.path
        env["MONO_PATH"]=runtime.appendingPathComponent("OSX-Linux-x64").path
        env["DYLD_FALLBACK_LIBRARY_PATH"]=runtime.path+":"+mono.appendingPathComponent("lib").path
        env["DGR_SKIP_STEAMWORKS_COPY"]="1"
        env["DUCK_FRIENDS_RELAY"]=relay
        env["DUCK_FRIENDS_GUIDE"]=resources.appendingPathComponent("Quick Start.html").path
        env.removeValue(forKey:"DUCK_FRIENDS_TEST")
        p.environment=env
        try fm.createDirectory(at:log.deletingLastPathComponent(),withIntermediateDirectories:true)
        fm.createFile(atPath:log.path,contents:nil)
        let handle=try FileHandle(forWritingTo:log);p.standardOutput=handle;p.standardError=handle
        p.terminationHandler={ process in
            try? handle.close()
            DispatchQueue.main.async {
                if process.terminationStatus == 0 || process.terminationReason == .uncaughtSignal && process.terminationStatus == SIGTERM { NSApp.terminate(nil) }
                else { self.fail("The game stopped unexpectedly. The log is at ~/Library/Logs/DuckGameFriends/game.log.") }
            }
        }
        try p.run();game=p
        // The game has its own window and Dock icon; keep this launcher out of the way.
        NSApp.setActivationPolicy(.accessory)
    }
}
let app=NSApplication.shared
let delegate=AppDelegate();app.delegate=delegate
app.run()
