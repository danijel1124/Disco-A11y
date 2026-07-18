using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using AccessibilityMod;
using AccessibilityMod.Navigation;
using AccessibilityMod.Patches;
using AccessibilityMod.Settings;
using AccessibilityMod.UI;
using AccessibilityMod.Utils;

[assembly: MelonInfo(typeof(DevBridge.DevBridgeMod), "Disco Elysium AI Dev Bridge", "0.1.0", "danijel1124")]
[assembly: MelonGame("ZAUM Studio", "Disco Elysium")]

namespace DevBridge
{
    /// <summary>
    /// Development-only companion mod: a file-based remote-control channel so an AI
    /// assistant (or any script) can drive the game and the accessibility mod without
    /// keyboard/screen access. NOT part of the mod release zip - installed only via the
    /// installer's "Enable AI dev bridge" option or a local build.
    ///
    /// Protocol (two transports, same commands - send "help" for the list):
    ///  - File: write one command line to UserData/DevBridge/command.txt; the bridge
    ///    polls a few times per second, executes, deletes the file, and writes the full
    ///    result to UserData/DevBridge/response.txt.
    ///  - Socket: TCP on 127.0.0.1, port in UserData/DevBridge/port.txt. One command
    ///    per line; the response is written back terminated by a line "&lt;&lt;END&gt;&gt;".
    ///    Commands run on the next frame, so latency is one frame instead of up to the
    ///    file poll interval - and the socket also PUSHES events without being asked:
    ///    lines starting with "! " (spoken text, dialogue started/ended, scene loads).
    /// </summary>
    public class DevBridgeMod : MelonMod
    {
        private const float POLL_INTERVAL = 0.2f;
        private const int PREFERRED_PORT = 48610;
        private const string RESPONSE_END = "<<END>>";

        private static readonly List<string> spokenHistory = new();
        private const int SPOKEN_HISTORY_MAX = 100;

        private string bridgeDir;
        private string commandPath;
        private string responsePath;
        private float lastPoll;

        // Socket transport. Accept/read run on background threads that only touch
        // sockets and strings (never Il2Cpp objects); commands are queued and executed
        // on the Unity main thread in OnUpdate, which also does all writing.
        private static TcpListener listener;
        private static volatile bool listenerRunning;
        private static readonly List<StreamWriter> socketClients = new();
        private static readonly ConcurrentQueue<(string Command, StreamWriter Client)> pendingSocketCommands = new();
        private bool lastDialogUiActive;

        public override void OnInitializeMelon()
        {
            bridgeDir = Path.Combine("UserData", "DevBridge");
            Directory.CreateDirectory(bridgeDir);
            commandPath = Path.Combine(bridgeDir, "command.txt");
            responsePath = Path.Combine(bridgeDir, "response.txt");

            // Capture everything the accessibility mod speaks, so the bridge can report
            // what a blind player would have heard. The mod raises this once per line at
            // the moment it goes to the screen reader - hooking Speak() instead sees every
            // queued line twice (enqueue + play) and invents duplicate speech that nobody
            // heard, which is exactly the false trail this bridge is supposed to prevent.
            try
            {
                TolkScreenReader.Spoken += OnSpoken;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BRIDGE] Could not hook speech output: {ex.Message}");
            }

            // Lets the bridge fire the mod's own hotkeys without the shipped mod carrying
            // a remote-control hatch: IsPressed answers true once for an injected key.
            try
            {
                var isPressed = AccessTools.Method(typeof(KeyBindings), nameof(KeyBindings.IsPressed));
                HarmonyInstance.Patch(isPressed, prefix: new HarmonyMethod(typeof(DevBridgeMod), nameof(IsPressedPrefix)));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BRIDGE] Could not hook mod hotkeys: {ex.Message}");
            }

            // No hook into the game's own input here on purpose. Both routes were tried
            // and both drove the game into its crash reporter: writing InControl's state
            // (PlayerAction.UpdateWithState/Commit), and hooking the query side
            // (OneAxisInputControl.get_WasPressed - a virtual method Il2Cpp cannot patch
            // safely, and one the game reads every frame for every action). The game's own
            // menus are therefore driven with "key", which posts a real OS keystroke.

            StartSocketServer();

            File.WriteAllText(Path.Combine(bridgeDir, "status.txt"), $"DevBridge ready {DateTime.Now:O}\n");
            MelonLogger.Msg($"[BRIDGE] Ready - command channel: {Path.GetFullPath(commandPath)}");
        }

        private void StartSocketServer()
        {
            try
            {
                try
                {
                    listener = new TcpListener(IPAddress.Loopback, PREFERRED_PORT);
                    listener.Start();
                }
                catch (SocketException)
                {
                    // Preferred port taken (second game instance / zombie) - let the OS pick.
                    listener = new TcpListener(IPAddress.Loopback, 0);
                    listener.Start();
                }
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                File.WriteAllText(Path.Combine(bridgeDir, "port.txt"), port.ToString());
                listenerRunning = true;

                var acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "DevBridgeAccept" };
                acceptThread.Start();
                MelonLogger.Msg($"[BRIDGE] Socket listening on 127.0.0.1:{port}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BRIDGE] Socket transport unavailable ({ex.Message}) - file channel still works");
            }
        }

        private static void AcceptLoop()
        {
            while (listenerRunning)
            {
                TcpClient client;
                try
                {
                    client = listener.AcceptTcpClient();
                }
                catch
                {
                    return; // listener stopped
                }

                try
                {
                    client.NoDelay = true;
                    var stream = client.GetStream();
                    var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                    lock (socketClients) socketClients.Add(writer);

                    var readerThread = new Thread(() => ReadLoop(client, writer)) { IsBackground = true, Name = "DevBridgeRead" };
                    readerThread.Start();
                }
                catch { /* client vanished during handshake */ }
            }
        }

        private static void ReadLoop(TcpClient client, StreamWriter writer)
        {
            try
            {
                using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length > 0) pendingSocketCommands.Enqueue((line, writer));
                }
            }
            catch { /* disconnect */ }
            finally
            {
                lock (socketClients) socketClients.Remove(writer);
                try { client.Close(); } catch { }
            }
        }

        /// <summary>Push one "! "-prefixed event line to every connected socket client. Main thread only.</summary>
        private static void BroadcastEvent(string text)
        {
            lock (socketClients)
            {
                if (socketClients.Count == 0) return;
                for (int i = socketClients.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        socketClients[i].WriteLine("! " + text);
                    }
                    catch
                    {
                        socketClients.RemoveAt(i);
                    }
                }
            }
        }

        public override void OnDeinitializeMelon()
        {
            listenerRunning = false;
            try { listener?.Stop(); } catch { }
            lock (socketClients)
            {
                foreach (var c in socketClients) { try { c.Dispose(); } catch { } }
                socketClients.Clear();
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            BroadcastEvent($"scene {sceneName}");
        }

        private static void OnSpoken(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            lock (spokenHistory)
            {
                spokenHistory.Add($"{DateTime.Now:HH:mm:ss.fff} {text}");
                if (spokenHistory.Count > SPOKEN_HISTORY_MAX) spokenHistory.RemoveAt(0);
            }
            BroadcastEvent($"spoken {text}");
        }

        public override void OnUpdate()
        {
            // Socket commands run every frame (that's the transport's latency win);
            // all socket writing stays on this main thread.
            while (pendingSocketCommands.TryDequeue(out var entry))
            {
                MelonLogger.Msg($"[BRIDGE] Socket command: {entry.Command}");
                string response;
                try
                {
                    response = Execute(entry.Command);
                }
                catch (Exception ex)
                {
                    response = $"ERROR: {ex}";
                }
                try
                {
                    entry.Client.WriteLine(response);
                    entry.Client.WriteLine(RESPONSE_END);
                }
                catch
                {
                    lock (socketClients) socketClients.Remove(entry.Client);
                }
            }

            try
            {
                bool dialogActive = DialogStateManager.IsDialogUiActive;
                if (dialogActive != lastDialogUiActive)
                {
                    lastDialogUiActive = dialogActive;
                    BroadcastEvent(dialogActive ? "dialog active" : "dialog inactive");
                }
            }
            catch { /* mod not ready yet */ }

            if (Time.unscaledTime - lastPoll < POLL_INTERVAL) return;
            lastPoll = Time.unscaledTime;

            try
            {
                if (!File.Exists(commandPath)) return;

                string command;
                try
                {
                    command = File.ReadAllText(commandPath).Trim();
                    File.Delete(commandPath);
                }
                catch (IOException)
                {
                    return; // writer still holds the file - retry next poll
                }

                if (command.Length == 0) return;

                MelonLogger.Msg($"[BRIDGE] Command: {command}");
                string response;
                try
                {
                    response = Execute(command);
                }
                catch (Exception ex)
                {
                    response = $"ERROR: {ex}";
                }

                File.WriteAllText(responsePath, response + Environment.NewLine);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BRIDGE] {ex}");
            }
        }

        private string Execute(string command)
        {
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var verb = parts[0].ToLowerInvariant();
            var nav = AccessibilityMod.AccessibilityMod.NavigationSystem;

            switch (verb)
            {
                case "help":
                    return "Commands:\n" +
                           "state | objects [maxCount] | spoken [count] | screenshot [file]\n" +
                           "screen (all visible on-screen text - compare against 'spoken' to find silent UI)\n" +
                           "names [n] (name sources per object: unity name vs. conversation)\n" +
                           "inventory (owned items per tab + equipped + healing charges + UI slot audit)\n" +
                           "key <i|t|j|c|m|f1|...> (game menus; posts a real keystroke, steals window focus)\n" +
                           "modkey <GameKey> | modkeys (fire one of the mod's own hotkeys)\n" +
                           "select npcs|locations|loot|all | cycle [back] | category next|prev\n" +
                           "navigate | interact | stop | announce\n" +
                           "dialog | continue\n" +
                           "teleport <x> <y> <z> | goto <scene> <marker> | scenes\n" +
                           "destinations | travel <destinationId> | view [type]\n" +
                           "save [name] | load <name> | quickload | loadnewest | saves | devmode\n" +
                           "trace on|off|tail [n]|holders   (why is the loading screen stuck?)\n" +
                           "readingmode off|full|speaker | set autoread|autointeract|captions on|off\n" +
                           "Transports: this file channel, or TCP 127.0.0.1 (port in UserData/DevBridge/port.txt);\n" +
                           "socket: one command per line, response ends with <<END>>, push events start with '! '";

                case "state":
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"scene: {SceneManager.GetActiveScene().name}");
                    var pos = GameObjectUtils.GetPlayerPosition();
                    sb.AppendLine($"player: {pos.x:F2} {pos.y:F2} {pos.z:F2}");
                    sb.AppendLine($"inConversation: {DialogStateManager.IsInConversation()}");
                    sb.AppendLine($"dialogUiActive: {DialogStateManager.IsDialogUiActive}");
                    sb.AppendLine($"dialogMode: {DialogStateManager.CurrentDialogMode}");
                    sb.AppendLine($"autoAdvance: {DialogAutoAdvance.Enabled}");
                    sb.AppendLine($"autoInteract: {AccessibilityPreferences.GetAutoInteract()}");
                    if (nav != null)
                    {
                        var info = nav.GetNavigationInfo();
                        sb.AppendLine(info.HasSelection
                            ? $"selection: {info.ObjectName} ({info.CategoryName} {info.CurrentIndex}/{info.TotalCount}, {info.Distance:F1}m {info.Direction}, reachable={info.IsReachable?.ToString() ?? "unknown"})"
                            : $"selection: none (category {info.CategoryName}, {info.TotalCount} objects)");
                    }
                    return sb.ToString().TrimEnd();
                }

                case "objects":
                {
                    int max = parts.Length > 1 && int.TryParse(parts[1], out var m) ? m : 25;
                    var registry = Il2CppFortressOccident.MouseOverHighlight.registry;
                    if (registry == null) return "no registry";
                    var playerPos = GameObjectUtils.GetPlayerPosition();
                    var entries = new List<(float Dist, string Line)>();
                    foreach (var obj in registry)
                    {
                        if (obj == null || obj.transform == null) continue;
                        var p = obj.transform.position;
                        float dist = Vector3.Distance(playerPos, p);
                        string name = ObjectNameCleaner.GetBetterObjectName(obj);
                        entries.Add((dist, $"{name} | {dist:F1}m {DirectionCalculator.GetCardinalDirection(playerPos, p)} | {p.x:F1} {p.y:F1} {p.z:F1}"));
                    }
                    return $"{entries.Count} objects (closest {Math.Min(max, entries.Count)}):\n" +
                           string.Join("\n", entries.OrderBy(e => e.Dist).Take(max).Select(e => e.Line));
                }

                case "spoken":
                {
                    int count = parts.Length > 1 && int.TryParse(parts[1], out var c) ? c : 15;
                    lock (spokenHistory)
                    {
                        return spokenHistory.Count == 0
                            ? "(nothing spoken yet)"
                            : string.Join("\n", spokenHistory.TakeLast(count));
                    }
                }

                case "screenshot":
                {
                    var file = parts.Length > 1 ? parts[1] : $"bridge_{DateTime.Now:HHmmss}.png";
                    var full = Path.GetFullPath(Path.Combine(bridgeDir, file));
                    ScreenCapture.CaptureScreenshot(full);
                    return $"screenshot queued: {full} (written within the next frames)";
                }

                case "select":
                {
                    if (nav == null) return "navigation system not ready";
                    if (parts.Length < 2) return "usage: select npcs|locations|loot|all";
                    var cat = parts[1].ToLowerInvariant() switch
                    {
                        "npcs" => ObjectCategory.NPCs,
                        "locations" => ObjectCategory.Locations,
                        "loot" => ObjectCategory.Loot,
                        _ => ObjectCategory.Everything,
                    };
                    nav.SelectCategory(cat);
                    return $"selected {cat}\n" + LastSpoken();
                }

                case "cycle":
                    if (nav == null) return "navigation system not ready";
                    nav.CycleWithinCategory(backward: parts.Length > 1 && parts[1] == "back");
                    return LastSpoken();

                case "category":
                    if (nav == null) return "navigation system not ready";
                    nav.CycleCategory(backward: parts.Length > 1 && parts[1] == "prev");
                    return LastSpoken();

                case "navigate":
                    if (nav == null) return "navigation system not ready";
                    nav.NavigateToSelectedObject();
                    return LastSpoken();

                case "interact":
                    if (nav == null) return "navigation system not ready";
                    nav.InteractWithSelectedObject();
                    return LastSpoken();

                case "stop":
                    if (nav == null) return "navigation system not ready";
                    nav.StopMovement();
                    return "stopped\n" + LastSpoken();

                case "announce":
                    if (nav == null) return "navigation system not ready";
                    return nav.GetNavigationInfo().FormatAnnouncement();

                case "ui":
                {
                    // ui selected | ui down|up|left|right | ui submit - drives the real
                    // Unity EventSystem selection, which is how the game's own menus
                    // (non-Button custom controls) are keyboard-navigated.
                    var es = UnityEngine.EventSystems.EventSystem.current;
                    if (es == null) return "no EventSystem";
                    if (parts.Length < 2) return "usage: ui selected|down|up|left|right|submit|cancel";
                    var sel = es.currentSelectedGameObject;
                    if (parts[1] == "selected") return sel == null ? "(nothing selected)" : sel.name;
                    if (sel == null) return "(nothing selected)";

                    if (parts[1] == "submit")
                    {
                        UnityEngine.EventSystems.ExecuteEvents.Execute(sel,
                            new UnityEngine.EventSystems.BaseEventData(es),
                            UnityEngine.EventSystems.ExecuteEvents.submitHandler);
                        return $"submit on {sel.name}";
                    }
                    if (parts[1] == "cancel")
                    {
                        UnityEngine.EventSystems.ExecuteEvents.Execute(sel,
                            new UnityEngine.EventSystems.BaseEventData(es),
                            UnityEngine.EventSystems.ExecuteEvents.cancelHandler);
                        return $"cancel on {sel.name}";
                    }

                    var dir = parts[1] switch
                    {
                        "up" => UnityEngine.EventSystems.MoveDirection.Up,
                        "left" => UnityEngine.EventSystems.MoveDirection.Left,
                        "right" => UnityEngine.EventSystems.MoveDirection.Right,
                        _ => UnityEngine.EventSystems.MoveDirection.Down,
                    };
                    var move = new UnityEngine.EventSystems.AxisEventData(es) { moveDir = dir };
                    UnityEngine.EventSystems.ExecuteEvents.Execute(sel, move,
                        UnityEngine.EventSystems.ExecuteEvents.moveHandler);
                    var now = es.currentSelectedGameObject;
                    return $"moved {parts[1]}; selected: {(now == null ? "(none)" : now.name)}";
                }

                case "names":
                {
                    // Name-source audit: the mod currently speaks Unity object names
                    // ("Whirling Door Kitsuragi"), which are dev-internal, not what the
                    // game displays. This lists every candidate source side by side so a
                    // proper fallback chain can be built - including for the many objects
                    // that have no conversation at all.
                    int max = parts.Length > 1 && int.TryParse(parts[1], out var mx) ? mx : 15;
                    var registry = Il2CppFortressOccident.MouseOverHighlight.registry;
                    if (registry == null) return "no registry";
                    var ppos = GameObjectUtils.GetPlayerPosition();
                    var rows = new List<(float Dist, string Line)>();
                    foreach (var obj in registry)
                    {
                        if (obj == null || obj.transform == null) continue;
                        float d = Vector3.Distance(ppos, obj.transform.position);

                        // Candidate name sources, side by side: the conversant actor (wrong -
                        // it is Cuno for Kim's paperwork), and the speaker of the
                        // conversation's first line, which is what the dialogue actually
                        // prints ("Spiegel:"). Both shown raw and localized.
                        string conv = "-", conversant = "-", speaker = "-", speakerLoc = "-";
                        try
                        {
                            var entity = obj.GetComponentInParent<Il2CppFortressOccident.BasicEntity>()
                                         ?? obj.GetComponentInChildren<Il2CppFortressOccident.BasicEntity>();
                            if (entity != null && !string.IsNullOrWhiteSpace(entity.conversation))
                            {
                                conv = entity.conversation;
                                var db = Il2CppPixelCrushers.DialogueSystem.DialogueManager.masterDatabase;
                                var conversation = db?.GetConversation(conv);
                                if (conversation != null)
                                {
                                    var ca = db.GetActor(conversation.ConversantID);
                                    if (ca != null) conversant = ca.Name ?? "-";

                                    var first = conversation.GetFirstDialogueEntry();
                                    if (first != null)
                                    {
                                        var sa = db.GetActor(first.ActorID);
                                        if (sa != null && !string.IsNullOrWhiteSpace(sa.Name))
                                        {
                                            speaker = sa.Name;
                                            var loc = Il2CppPixelCrushers.DialogueSystem.CharacterInfo
                                                .GetLocalizedDisplayNameInDatabase(sa.Name);
                                            speakerLoc = string.IsNullOrWhiteSpace(loc) ? "(none)" : loc;
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { conv = "ERR:" + ex.GetType().Name; }

                        // Loot objects are container sources: whatever they hold has a real,
                        // localized item name in the item database, even when the object
                        // itself only has a level-designer name.
                        string contents = "-";
                        try
                        {
                            var source = obj.GetComponentInParent<Il2CppSunshine.ContainerSource>()
                                         ?? obj.GetComponentInChildren<Il2CppSunshine.ContainerSource>();
                            var items = source?.containedItems;
                            if (items != null && items.Count > 0)
                            {
                                var names = new List<string>();
                                foreach (var it in items)
                                {
                                    if (it == null || string.IsNullOrWhiteSpace(it.name)) continue;
                                    var dbItem = Il2Cpp.InventoryItemList.singleton?.GetByName(it.name);
                                    names.Add(dbItem != null && !string.IsNullOrWhiteSpace(dbItem.displayName)
                                        ? dbItem.displayName
                                        : it.name);
                                }
                                if (names.Count > 0) contents = string.Join(" + ", names);
                            }
                        }
                        catch (Exception ex) { contents = "ERR:" + ex.GetType().Name; }

                        rows.Add((d, $"{d,5:F1}m | unity='{obj.gameObject.name}' | items='{contents}' | conversant='{conversant}'"));
                    }
                    string lang;
                    try { lang = Il2CppPixelCrushers.DialogueSystem.Localization.Language; }
                    catch (Exception ex) { lang = "ERR:" + ex.Message; }

                    return $"{rows.Count} objects, dialogue language='{lang}', name sources (closest {Math.Min(max, rows.Count)}):\n" +
                           string.Join("\n", rows.OrderBy(r => r.Dist).Take(max).Select(r => r.Line));
                }

                case "inventory":
                {
                    // Ground-truth audit for bug #55 ("I can't find items I received"):
                    // dumps what the player actually OWNS according to the game's own
                    // data (InventoryViewData tabContents + equipped slots + healing
                    // charges), and separately which slot objects are alive in the UI.
                    // Comparing the two shows whether items are missing from the DATA
                    // (impossible) or merely unreachable by keyboard NAVIGATION.
                    var sb = new StringBuilder();
                    var data = Il2CppSunshine.Metric.InventoryViewData.Singleton;
                    if (data == null) return "InventoryViewData.Singleton is null";

                    var tabs = data.tabContents;
                    if (tabs == null) sb.AppendLine("tabContents: null");
                    else
                    {
                        foreach (var tab in tabs)
                        {
                            var slots = tab.Value;
                            sb.AppendLine($"tab {tab.Key} ({(slots == null ? 0 : slots.Count)} items):");
                            if (slots == null) continue;
                            foreach (var slot in slots)
                                sb.AppendLine($"  slot {slot.Key}: {slot.Value}");
                        }
                    }

                    sb.AppendLine("equipped:");
                    foreach (Il2Cpp.EquipmentSlotType st in Enum.GetValues(typeof(Il2Cpp.EquipmentSlotType)))
                    {
                        try
                        {
                            if (data.IsEquipped(st)) sb.AppendLine($"  {st}: {data.GetEquipped(st)}");
                        }
                        catch { /* some enum values are not real slots */ }
                    }

                    // The mod's own shared charge lookup (-1 = pools unreadable), so the
                    // bridge reports exactly what the healing keys will see.
                    sb.AppendLine($"healing charges: health={HealingKeyActions.GetCharges(Il2CppSunshine.Metric.SkillType.ENDURANCE)}, morale={HealingKeyActions.GetCharges(Il2CppSunshine.Metric.SkillType.VOLITION)} (-1 = unreadable)");

                    // Which slot objects exist in the open UI, and can navigation reach them?
                    var highlighters = UnityEngine.Object.FindObjectsOfType<Il2Cpp.InventoryHighlighter>();
                    int active = 0;
                    var uiSb = new StringBuilder();
                    foreach (var h in highlighters)
                    {
                        if (h == null || !h.gameObject.activeInHierarchy) continue;
                        active++;
                        var selectable = h.GetComponent<UnityEngine.UI.Selectable>();
                        string navMode = selectable == null ? "no-selectable"
                            : selectable.navigation.mode.ToString();
                        uiSb.AppendLine($"  {h.gameObject.name} | nav={navMode} | interactable={(selectable != null && selectable.interactable)}");
                    }
                    sb.AppendLine($"active InventoryHighlighter slots: {active}");
                    sb.Append(uiSb);
                    var es2 = UnityEngine.EventSystems.EventSystem.current;
                    sb.AppendLine($"EventSystem selection: {(es2?.currentSelectedGameObject == null ? "(none)" : es2.currentSelectedGameObject.name)}");
                    return sb.ToString().TrimEnd();
                }

                case "thought":
                {
                    // Test rig for bug #57 (thought cabinet completion): "thought list"
                    // shows every ThoughtCabinetProject with its state; "thought discover
                    // <namepart>" finishes one through the game's own research-completed
                    // path (CharacterThoughts.DiscoverThought), so the real splash flow
                    // runs - no waiting for in-game hours to pass.
                    var sheet = UnityEngine.Object.FindObjectOfType<Il2CppSunshine.Metric.CharacterSheet>();
                    if (sheet == null || sheet.thoughts == null) return "CharacterSheet/thoughts not found";

                    var projects = UnityEngine.Object.FindObjectsOfType<Il2CppSunshine.Metric.ThoughtCabinetProject>(true);
                    if (parts.Length < 2 || parts[1] == "list")
                    {
                        var sb = new StringBuilder();
                        foreach (var p in projects)
                        {
                            if (p == null) continue;
                            sb.AppendLine($"{p.state,-10} | {p.name} | '{p.displayName}'");
                        }
                        return sb.Length == 0 ? "(no thoughts found)" : sb.ToString().TrimEnd();
                    }

                    if ((parts[1] == "discover" || parts[1] == "splash") && parts.Length >= 3)
                    {
                        string part = string.Join(" ", parts.Skip(2)).ToLowerInvariant();
                        var target = projects.FirstOrDefault(p => p != null &&
                            ((p.displayName ?? "").ToLowerInvariant().Contains(part)
                             || (p.name ?? "").ToLowerInvariant().Contains(part)));
                        if (target == null) return $"no thought matching '{part}'";
                        var before = target.state;
                        sheet.thoughts.DiscoverThought(target);

                        if (parts[1] == "splash")
                        {
                            // Replicates the REAL research-completed flow, not just the
                            // state flip: the game queues the discovered thought for the
                            // splash animation (ThoughtManager) and the HUD sense orb
                            // then runs the splash screen - the exact path a naturally
                            // completed thought takes, so the mod's splash patches get
                            // tested against reality.
                            var tm = Il2CppSunshine.ThoughtManager.Singleton;
                            if (tm == null) return "ThoughtManager.Singleton is null";
                            tm.AddDisoveredThoughtToAnimate(target);

                            var orb = UnityEngine.Object.FindObjectOfType<Il2Cpp.SenseOrb>();
                            if (orb == null) return "SenseOrb not found (HUD hidden?)";
                            bool ran = orb.RunThoughtSplashScreen();
                            return $"splash for '{target.displayName}': queued and RunThoughtSplashScreen -> {ran}";
                        }

                        return $"DiscoverThought('{target.displayName}') called (state before: {before}, now: {target.state})";
                    }

                    return "usage: thought [list | discover <namepart> | splash <namepart>]";
                }

                case "pages":
                {
                    // Diagnostic for the screen announcer: which of the game's pages exist,
                    // and which one counts as "currently open".
                    var sb = new StringBuilder();
                    var all = UnityEngine.Object.FindObjectsOfType<Il2CppPages.DiscoPage>();
                    sb.AppendLine($"FindObjectsOfType<DiscoPage>: {all.Length}");
                    foreach (var p in all)
                    {
                        if (p == null) continue;
                        var canvas = p.GetComponentInParent<Canvas>();
                        sb.AppendLine($"  {p.GetType().Name} | active={p.gameObject.activeInHierarchy} | enabled={p.enabled} | canvas={(canvas == null ? "none" : canvas.isActiveAndEnabled.ToString())}");
                    }
                    return sb.ToString().TrimEnd();
                }

                case "inspect":
                {
                    // Dumps every component and text-bearing child of the focused UI object.
                    // Used to find out why an element the game clearly shows ("BEGINNEN")
                    // is invisible to the mod's text extraction.
                    var es = UnityEngine.EventSystems.EventSystem.current;
                    var sel = es?.currentSelectedGameObject;
                    if (sel == null) return "(nothing selected)";

                    var sb = new StringBuilder();
                    sb.AppendLine($"selected: {sel.name}");
                    sb.AppendLine("components:");
                    foreach (var c in sel.GetComponents<Component>())
                    {
                        if (c != null) sb.AppendLine("  " + c.GetIl2CppType().FullName);
                    }
                    sb.AppendLine("children with components:");
                    foreach (var t in sel.GetComponentsInChildren<Transform>())
                    {
                        if (t == null) continue;
                        foreach (var c in t.GetComponents<Component>())
                        {
                            if (c == null) continue;
                            var type = c.GetIl2CppType().FullName;
                            if (type.Contains("Text") || type.Contains("Label"))
                            {
                                sb.AppendLine($"  [{t.name}] {type}");
                            }
                        }
                    }
                    return sb.ToString().TrimEnd();
                }

                case "screen":
                {
                    // Accessibility audit tool: dump every piece of text actually visible
                    // on screen. Comparing this against "spoken" shows exactly what a
                    // sighted player can read but a blind player never hears - the gaps
                    // a blind user cannot report themselves.
                    var sb = new StringBuilder();
                    int shown = 0;
                    foreach (var t in UnityEngine.Object.FindObjectsOfType<Il2CppTMPro.TextMeshProUGUI>())
                    {
                        if (t == null || !t.gameObject.activeInHierarchy) continue;
                        if (string.IsNullOrWhiteSpace(t.text)) continue;
                        if (t.color.a < 0.05f) continue;
                        // The game keeps its panels (inventory, thought cabinet, ...) alive and
                        // active while hiding them by fading the canvas - without this check the
                        // dump lists every panel in the game and the audit is worthless.
                        if (!IsActuallyVisible(t)) continue;

                        sb.AppendLine($"[{t.transform.parent?.name ?? "?"}] {t.text.Replace("\n", " / ").Trim()}");
                        if (++shown >= 120) { sb.AppendLine("... (truncated)"); break; }
                    }
                    return shown == 0 ? "(no visible text)" : sb.ToString().TrimEnd();
                }

                case "key":
                {
                    // Opens the game's own menus (i=inventory, t=thought cabinet,
                    // j=journal, c=character sheet, m=map, f1=help). This posts a real OS
                    // keystroke and therefore has to pull the game window to the front,
                    // which steals focus from whatever the user is doing - so it is for
                    // unattended sessions only. Hooking the game's input instead is not an
                    // option (see OnInitializeMelon). The mod's own hotkeys have a clean
                    // path and use "modkey".
                    if (parts.Length < 2) return "usage: key <i|ctrl+h|ctrl+shift+tab|escape|...> (steals window focus)";
                    FocusGameWindow();
                    if (!TryPressKey(parts[1], out var error)) return error;
                    return $"pressed {parts[1]} (game window was focused)";
                }

                case "modkey":
                {
                    // Fires one of the mod's own hotkeys, whatever it happens to be bound
                    // to. Done from here via a Harmony hook on KeyBindings.IsPressed rather
                    // than a test hatch inside the mod, so the shipped mod stays clean.
                    if (parts.Length < 2) return "usage: modkey <GameKey> - see 'modkeys'";
                    if (!Enum.TryParse<GameKey>(parts[1], ignoreCase: true, out var gameKey))
                        return $"unknown mod key '{parts[1]}' - see 'modkeys'";

                    lock (injectedKeys) injectedKeys.Add(gameKey);
                    return $"mod key fired: {gameKey} (bound to {KeyBindings.SpeakableName(gameKey)})";
                }

                case "modkeys":
                    return "mod hotkeys:\n" + string.Join("\n",
                        Enum.GetValues(typeof(GameKey)).Cast<GameKey>()
                            .Select(k => $"{k} = {KeyBindings.SpeakableName(k)}"));

                case "buttons":
                {
                    var sb = new StringBuilder();
                    foreach (var b in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Button>())
                    {
                        if (b == null || !b.gameObject.activeInHierarchy || !b.interactable) continue;
                        var label = b.GetComponentInChildren<Il2CppTMPro.TextMeshProUGUI>();
                        var text = label != null && !string.IsNullOrWhiteSpace(label.text) ? label.text.Trim() : b.gameObject.name;
                        sb.AppendLine(text);
                    }
                    return sb.Length == 0 ? "(no interactable buttons)" : sb.ToString().TrimEnd();
                }

                case "click":
                {
                    if (parts.Length < 2) return "usage: click <button label substring>";
                    var needle = string.Join(" ", parts.Skip(1)).ToLowerInvariant();
                    foreach (var b in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Button>())
                    {
                        if (b == null || !b.gameObject.activeInHierarchy || !b.interactable) continue;
                        var label = b.GetComponentInChildren<Il2CppTMPro.TextMeshProUGUI>();
                        var text = label != null && !string.IsNullOrWhiteSpace(label.text) ? label.text.Trim() : b.gameObject.name;
                        if (text.ToLowerInvariant().Contains(needle))
                        {
                            b.onClick.Invoke();
                            return $"clicked: {text}";
                        }
                    }
                    return $"no button matching '{needle}'";
                }

                case "dialog":
                    return $"inConversation: {DialogStateManager.IsInConversation()}\nlastLine: {DialogSystemPatches.GetLastDialogueLine()}";

                case "continue":
                {
                    var btn = Il2Cpp.SunshineContinueButton.instance;
                    var button = btn?.buttonComponent;
                    if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
                        return "continue button not available";
                    button.onClick.Invoke();
                    return "continued";
                }

                case "teleport":
                {
                    if (parts.Length < 4) return "usage: teleport <x> <y> <z>";
                    var character = GameObjectUtils.GetPlayerCharacter();
                    if (character == null) return "player not found";
                    var target = new Vector3(float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
                    character.transform.position = target;
                    return $"teleported to {target.x:F2} {target.y:F2} {target.z:F2}";
                }

                case "quickload":
                case "loadnewest":
                {
                    var persistence = UnityEngine.Object.FindObjectOfType<Il2Cpp.SunshinePersistence>();
                    if (persistence == null) return "SunshinePersistence not found";

                    // Loading with nothing to load leaves the game hanging on the loading
                    // screen forever, and the bridge used to answer "triggered" to that -
                    // the caller then waits for a game that is never coming back.
                    string newest = NewestSaveName();
                    if (newest == null) return "no save games exist - nothing to load (start a new game first)";

                    if (LoadBlockedReason() is string blocked) return blocked;

                    if (verb == "quickload") persistence.DoQuickLoad();
                    else persistence.LoadNewest();
                    return $"{verb} triggered (newest save: {newest})";
                }

                case "saves":
                {
                    var names = SaveGameNames();
                    if (names.Count == 0) return "no save games";

                    var sb = new StringBuilder();
                    foreach (var name in names) sb.AppendLine(name);
                    sb.Append($"({names.Count} saves; newest: {NewestSaveName() ?? "unknown"})");
                    return sb.ToString();
                }

                // Loads one particular save by name, so a test never has to move or delete
                // somebody else's save files to make its own the newest one.
                case "load":
                {
                    if (parts.Length < 2) return "usage: load <part of the save name>   (see 'saves')";
                    string needle = string.Join(" ", parts.Skip(1));

                    var matches = SaveGameNames()
                        .Where(n => n.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();

                    if (matches.Count == 0) return $"no save matches '{needle}'";
                    if (matches.Count > 1)
                        return $"'{needle}' matches {matches.Count} saves - be more specific:\n" + string.Join("\n", matches);

                    var persistence = UnityEngine.Object.FindObjectOfType<Il2Cpp.SunshinePersistence>();
                    if (persistence == null) return "SunshinePersistence not found";

                    if (LoadBlockedReason() is string blocked) return blocked;

                    persistence.Load(matches[0], false);
                    return $"loading '{matches[0]}'";
                }

                // Saving without the save screen: an automated test can park the game at an
                // interesting spot once and reload it, instead of replaying the intro every
                // time. The save screen's slots are not EventSystem-navigable, so 'ui' can't
                // reach them.
                case "save":
                {
                    var persistence = UnityEngine.Object.FindObjectOfType<Il2Cpp.SunshinePersistence>();
                    if (persistence == null) return "SunshinePersistence not found";
                    string name = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "devbridge";
                    persistence.SaveWithScreenshot(name);
                    return $"saving as '{name}'";
                }

                case "scenes":
                {
                    var sb = new StringBuilder();
                    int count = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
                    for (int i = 0; i < count; i++)
                    {
                        string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                        sb.AppendLine(System.IO.Path.GetFileNameWithoutExtension(path));
                    }
                    sb.Append($"({count} scenes in build settings)");
                    return sb.ToString();
                }

                // The game's own area change, the one its dialogue scripts use - so it runs
                // the real transition (loading screen, spawn point, camera) instead of
                // dropping the player into a scene that was never set up around them.
                case "goto":
                {
                    if (parts.Length < 3) return "usage: goto <sceneName> <locationMarker> (see 'destinations')";
                    Il2CppSunshine.Dialogue.AreaSpecificLuaFunctions.TeleportTo(parts[1], parts[2]);
                    return $"teleporting to {parts[1]} at {parts[2]}";
                }

                // Travel destinations are the doors/exits the game itself registers, so this
                // is the one list that is guaranteed to name places the player can be sent to.
                case "destinations":
                {
                    var sb = new StringBuilder();
                    int n = 0;
                    foreach (var dest in UnityEngine.Object.FindObjectsOfType<Il2CppFortressOccident.TravelDestination>())
                    {
                        if (dest == null) continue;
                        sb.AppendLine($"{dest.name}  (area: {dest.areaName})");
                        n++;
                    }
                    sb.Append($"({n} destinations in the loaded scenes)");
                    return sb.ToString();
                }

                // Opens any of the game's screens through its own view controller, so an
                // audit never needs a real key press (which would steal the user's window).
                case "view":
                {
                    var controller = UnityEngine.Object.FindObjectOfType<Il2CppSunshine.Views.ViewController>();
                    if (controller == null) return "ViewController not found";

                    if (parts.Length < 2)
                    {
                        var current = Il2CppSunshine.Views.ViewController.GetCurrentView();
                        string now = current != null ? current.GetViewType().ToString() : "none";
                        return $"current view: {now}\navailable: " +
                               string.Join(" ", Enum.GetNames(typeof(Il2CppSunshine.Views.ViewType)));
                    }

                    if (!Enum.TryParse<Il2CppSunshine.Views.ViewType>(parts[1], true, out var wanted))
                        return $"unknown view type '{parts[1]}' - see 'view' for the list";

                    var target = controller.GetViewByType(wanted);
                    if (target == null) return $"the game has no {wanted} view in this scene";

                    controller.SwitchToView(target, false, false,
                        Il2CppSunshine.Views.VIEW_STACK_OPERATION.STACK_PREVIOUS);
                    return $"switched to {wanted}";
                }

                case "travel":
                {
                    if (parts.Length < 2) return "usage: travel <destinationId> (see 'destinations')";
                    string id = string.Join(" ", parts.Skip(1));
                    bool ok = Il2CppFortressOccident.TravelDestination.ArriveAt(id);
                    return ok ? $"travelling to {id}" : $"no destination named '{id}'";
                }

                // The game's whole story state lives in Lua variables (PixelCrushers
                // Dialogue System), so this reads and writes anything the game knows about
                // itself: quest flags, unlocks, time of day. The debug lever, in one line.
                //   lua return Variable["day"]
                //   lua Variable["some_flag"] = true
                case "lua":
                {
                    if (parts.Length < 2) return "usage: lua <code>   e.g. lua return Variable[\"day\"]";
                    string code = command.Substring(command.IndexOf("lua", StringComparison.Ordinal) + 4).Trim();

                    var result = Il2CppPixelCrushers.DialogueSystem.Lua.Run(code, true, true);
                    string value = result.hasReturnValue ? result.asString : "(no return value)";
                    return $"lua: {value}";
                }

                // Records what the game does, so a hang can be read afterwards instead of
                // guessed at. 'trace holders' answers the loading-screen question directly:
                // the screen stays up while anything holds a delay on it.
                case "trace":
                {
                    if (parts.Length < 2)
                        return $"usage: trace on|off|tail [n]|holders    (currently {(GameTracer.Enabled ? "on" : "off")}, log: {GameTracer.LogPath})";

                    switch (parts[1])
                    {
                        case "on":
                            GameTracer.SetEnabled(true);
                            return $"trace on -> {GameTracer.LogPath}";
                        case "off":
                            GameTracer.SetEnabled(false);
                            return "trace off";
                        case "holders":
                            return GameTracer.DescribeDelayHolders();
                        case "tail":
                            int n = parts.Length > 2 && int.TryParse(parts[2], out var parsed) ? parsed : 40;
                            return GameTracer.Tail(n);
                        default:
                            return "usage: trace on|off|tail [n]|holders";
                    }
                }

                // Runs the mod's reachability check on the selected object, step by step,
                // so "reachable=unknown" stops being a shrug and starts naming the step
                // that failed.
                case "reach":
                {
                    var navInfo = nav?.StateManager?.GetCurrentSelectedObject();
                    if (navInfo == null) return "nothing selected";

                    var playerPos = GameObjectUtils.GetPlayerPosition();
                    var targetPos = navInfo.transform.position;
                    var sb = new StringBuilder();
                    sb.AppendLine($"target: {navInfo.gameObject.name} at {targetPos}");
                    sb.AppendLine($"player: {playerPos}, distance {UnityEngine.Vector3.Distance(playerPos, targetPos):F1}m");

                    bool playerOk = UnityEngine.AI.NavMesh.SamplePosition(playerPos, out var ph, 2.0f, UnityEngine.AI.NavMesh.AllAreas);
                    sb.AppendLine($"player on navmesh: {playerOk}" + (playerOk ? $" (snapped {UnityEngine.Vector3.Distance(playerPos, ph.position):F2}m)" : ""));

                    bool targetOk = UnityEngine.AI.NavMesh.SamplePosition(targetPos, out var th, 3.0f, UnityEngine.AI.NavMesh.AllAreas);
                    sb.AppendLine($"target on navmesh: {targetOk}" + (targetOk ? $" (snapped {UnityEngine.Vector3.Distance(targetPos, th.position):F2}m)" : ""));

                    if (playerOk && targetOk)
                    {
                        var path = new UnityEngine.AI.NavMeshPath();
                        bool calc = UnityEngine.AI.NavMesh.CalculatePath(ph.position, th.position, UnityEngine.AI.NavMesh.AllAreas, path);
                        sb.AppendLine($"path calculated: {calc}, status: {path.status}, corners: {path.corners?.Length ?? 0}");
                        var corners = path.corners;
                        if (corners != null && corners.Length > 0)
                        {
                            sb.AppendLine($"path ends {UnityEngine.Vector3.Distance(corners[corners.Length - 1], targetPos):F1}m from target");
                        }
                    }

                    return sb.ToString();
                }

                case "devmode":
                {
                    var modes = UnityEngine.Object.FindObjectOfType<Il2CppSunshine.DebugModes>();
                    if (modes == null) return "DebugModes object not found";
                    modes.SetDeveloperMode();
                    return $"developer mode set; current: {Il2CppSunshine.DebugModes.CurrentMode}";
                }

                case "readingmode":
                {
                    if (parts.Length < 2) return "usage: readingmode off|full|speaker";
                    var target = parts[1] switch
                    {
                        "full" => DialogReadingMode.Full,
                        "speaker" => DialogReadingMode.SpeakerOnly,
                        _ => DialogReadingMode.Disabled,
                    };
                    while (DialogStateManager.CurrentDialogMode != target)
                    {
                        DialogStateManager.ToggleDialogReading();
                    }
                    return $"reading mode: {DialogStateManager.CurrentDialogMode}";
                }

                case "set":
                {
                    if (parts.Length < 3) return "usage: set autoread|autointeract|captions on|off";
                    bool on = parts[2] == "on";
                    switch (parts[1])
                    {
                        case "autoread":
                            if (DialogAutoAdvance.Enabled != on) DialogAutoAdvance.Toggle();
                            return $"autoread: {DialogAutoAdvance.Enabled}";
                        case "autointeract":
                            AccessibilityPreferences.SetAutoInteract(on);
                            return $"autointeract: {on}";
                        case "captions":
                            AccessibilityPreferences.SetSpeakAudioCaptions(on);
                            return $"captions: {on}";
                        default:
                            return $"unknown setting '{parts[1]}'";
                    }
                }

                default:
                    return $"unknown command '{verb}' - send 'help' for the list";
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void keybd_event(byte vk, byte scan, uint flags, System.UIntPtr extra);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// Whether the game can survive being told to load, or null when it can.
        ///
        /// Traced from an actual hang: loading while the game is still booting throws a
        /// NullReferenceException inside SceneTransitionManager's load coroutine -
        /// HudToggle.FixForDreamScene, reached from LoadDataAfterLoadingArea, on a HUD that
        /// does not exist yet. An exception inside a Unity coroutine kills the coroutine, so
        /// the rest of the load simply never runs and the game sits on the loading screen
        /// forever. A human never hits this because the main menu they click "continue" in
        /// only appears once the HUD is up; a script can ask sooner. So we check for the
        /// very object whose absence causes the crash.
        /// </summary>
        private static string LoadBlockedReason()
        {
            try
            {
                // Wait for the main menu, which is the exact moment a person could click
                // "continue" - during boot the view is SPECIAL and the game is not ready.
                // (Checking for the HudToggle object is not enough: it exists during boot,
                // just not finished, and the crash is inside it.)
                var view = Il2CppSunshine.Views.ViewController.GetCurrentView();
                var type = view != null ? view.GetViewType() : Il2CppSunshine.Views.ViewType.CLEAR;

                bool inGame = type != Il2CppSunshine.Views.ViewType.SPECIAL
                              && type != Il2CppSunshine.Views.ViewType.LOBBY;
                if (!inGame)
                    return "the game is still starting up - loading now throws inside its load coroutine (HudToggle.FixForDreamScene) and leaves it stuck on the loading screen for good. Wait for the main menu.";
            }
            catch
            {
                // If we cannot tell, do not stand in the way.
            }

            return null;
        }

        /// <summary>
        /// Every save the game knows about. Read from the save directory rather than the
        /// game's cache, because the cache is only refreshed at certain moments and a save
        /// written seconds ago would be missing from it.
        /// </summary>
        private static List<string> SaveGameNames()
        {
            var names = new List<string>();
            try
            {
                var dir = System.IO.Path.Combine(Application.persistentDataPath, "SaveGames");
                if (!System.IO.Directory.Exists(dir)) return names;

                foreach (var file in System.IO.Directory.GetFiles(dir, "*.ntwtf.zip"))
                {
                    // "Name(date).ntwtf.zip" - the game loads it by the part before ".ntwtf".
                    var name = System.IO.Path.GetFileName(file);
                    int cut = name.IndexOf(".ntwtf", StringComparison.OrdinalIgnoreCase);
                    names.Add(cut > 0 ? name.Substring(0, cut) : name);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BRIDGE] Could not list saves: {ex.Message}");
            }

            return names;
        }

        /// <summary>
        /// The newest save the game itself would load, or null when there is none. Asks the
        /// game's own file manager - the same source LoadNewest() reads - rather than
        /// guessing from the save directory, which also holds Steam's bookkeeping files.
        /// </summary>
        private static string NewestSaveName()
        {
            try
            {
                string last = Il2Cpp.SunshinePersistenceFileManager.GetLastSave();
                return string.IsNullOrWhiteSpace(last) ? null : last;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BRIDGE] Could not read save list: {ex.Message}");
                return null;
            }
        }

        private const uint KEYEVENTF_KEYUP = 0x0002;

        private static readonly Dictionary<string, byte> VirtualKeys = BuildVirtualKeys();

        private static Dictionary<string, byte> BuildVirtualKeys()
        {
            var keys = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
            {
                ["escape"] = 0x1B, ["tab"] = 0x09, ["space"] = 0x20, ["return"] = 0x0D, ["enter"] = 0x0D,
                ["up"] = 0x26, ["down"] = 0x28, ["left"] = 0x25, ["right"] = 0x27,
                // "plus" = the main-keyboard +/= key (VK_OEM_PLUS); "keypadplus" = the
                // numpad +. These stay mapped for layout experiments even though healing
                // moved off Plus long ago (QWERTZ never fired it; healing is Ctrl+H /
                // Shift+H now): pressing REAL keystrokes is the only way to catch a
                // layout mismatch that "modkey" (which bypasses the keyboard) cannot see.
                ["plus"] = 0xBB, ["keypadplus"] = 0x6B, ["kpplus"] = 0x6B,
                ["home"] = 0x24, ["end"] = 0x23, ["pageup"] = 0x21, ["pagedown"] = 0x22,
            };

            // The letters and function keys the game binds its own actions to (M for the
            // map, I for inventory, ...) - VK codes for these are just their ASCII values.
            for (char c = 'a'; c <= 'z'; c++) keys[c.ToString()] = (byte)char.ToUpperInvariant(c);
            for (int i = 1; i <= 12; i++) keys["f" + i] = (byte)(0x70 + i - 1);

            return keys;
        }

        /// <summary>Brings the game's own window to the front so an emulated key reaches it.</summary>
        private static void FocusGameWindow()
        {
            try
            {
                var hwnd = GetActiveWindow(); // this process' window - we run inside the game
                if (hwnd != IntPtr.Zero) SetForegroundWindow(hwnd);
            }
            catch { /* focus is best-effort */ }
        }

        // Modifier virtual-key codes, held down around the main key so a real
        // "ctrl+plus" reaches the game exactly as the player's fingers would send it.
        private const byte VK_CONTROL = 0x11, VK_SHIFT = 0x10, VK_MENU = 0x12; // MENU = Alt

        /// <summary>
        /// Presses a key, optionally with modifiers: "ctrl+plus", "ctrl+shift+tab",
        /// "shift+plus", or a bare "i". Modifiers are held down for the duration of the
        /// key stroke and released in reverse order - a genuine chord, so the mod's
        /// KeyBindings.IsPressed sees the same Ctrl/Shift state a human would produce.
        /// This is what lets us tell a dead binding (mod never sees the key) apart from a
        /// dead action (mod sees it but does nothing).
        /// </summary>
        private static bool TryPressKey(string spec, out string error)
        {
            error = null;

            // Split "ctrl+shift+plus" into modifiers + final key. The '+' separators are
            // the ones BETWEEN tokens; the literal plus key is spelled "plus", never "+".
            var tokens = spec.Split('+');
            var mods = new List<byte>();
            for (int i = 0; i < tokens.Length - 1; i++)
            {
                switch (tokens[i].ToLowerInvariant())
                {
                    case "ctrl": case "control": mods.Add(VK_CONTROL); break;
                    case "shift": mods.Add(VK_SHIFT); break;
                    case "alt": mods.Add(VK_MENU); break;
                    default:
                        error = $"unknown modifier '{tokens[i]}' - use ctrl, shift or alt";
                        return false;
                }
            }

            string name = tokens[tokens.Length - 1];
            byte vk;
            if (VirtualKeys.TryGetValue(name, out var mapped))
            {
                vk = mapped;
            }
            else if (name.Length == 1 && char.IsLetterOrDigit(name[0]))
            {
                vk = (byte)char.ToUpperInvariant(name[0]); // VK codes for A-Z / 0-9 equal their ASCII value
            }
            else
            {
                error = $"unknown key '{name}' - use a letter, a digit, or one of: {string.Join(", ", VirtualKeys.Keys)}";
                return false;
            }

            // Press modifiers, then the key down+up, then release modifiers in reverse.
            foreach (var m in mods) keybd_event(m, 0, 0, System.UIntPtr.Zero);
            keybd_event(vk, 0, 0, System.UIntPtr.Zero);
            keybd_event(vk, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
            for (int i = mods.Count - 1; i >= 0; i--) keybd_event(mods[i], 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
            return true;
        }

        private static readonly HashSet<GameKey> injectedKeys = new();

        /// <summary>Answers "yes, pressed" once for a mod hotkey the bridge injected, then forgets it.
        /// The parameter must be named exactly as in KeyBindings.IsPressed(GameKey action).</summary>
        private static bool IsPressedPrefix(GameKey action, ref bool __result)
        {
            lock (injectedKeys)
            {
                if (!injectedKeys.Remove(action)) return true; // not injected - run the real check
            }
            __result = true;
            return false; // skip the real check for this one frame
        }

        /// <summary>
        /// True when a text element is really on screen: its own canvas renders, and no
        /// canvas group above it has faded it out. Cheap enough for a one-off dump.
        /// </summary>
        private static bool IsActuallyVisible(Il2CppTMPro.TextMeshProUGUI text)
        {
            try
            {
                var canvas = text.GetComponentInParent<Canvas>();
                if (canvas == null || !canvas.isActiveAndEnabled) return false;

                var t = text.transform;
                while (t != null)
                {
                    var group = t.GetComponent<CanvasGroup>();
                    if (group != null && (group.alpha < 0.05f || !group.gameObject.activeSelf)) return false;
                    t = t.parent;
                }
                return true;
            }
            catch
            {
                return true; // when in doubt, show it - a false positive beats a missed gap
            }
        }

        private static string LastSpoken()
        {
            lock (spokenHistory)
            {
                return spokenHistory.Count == 0 ? "(nothing spoken)" : "spoken: " + spokenHistory[^1];
            }
        }
    }
}
