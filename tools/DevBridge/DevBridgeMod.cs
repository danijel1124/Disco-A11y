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
            // what a blind player would have heard.
            try
            {
                var speak = AccessTools.Method(typeof(TolkScreenReader), "Speak");
                HarmonyInstance.Patch(speak, postfix: new HarmonyMethod(typeof(DevBridgeMod), nameof(SpeakPostfix)));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BRIDGE] Could not hook speech output: {ex.Message}");
            }

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

        private static string lastSpokenText;
        private static DateTime lastSpokenAt;

        private static void SpeakPostfix(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            lock (spokenHistory)
            {
                // Queued announcements pass through Speak twice (once on enqueue, once
                // when the audio-aware queue actually plays them) - collapse the pair.
                var now = DateTime.Now;
                if (text == lastSpokenText && (now - lastSpokenAt).TotalSeconds < 2) return;
                lastSpokenText = text;
                lastSpokenAt = now;

                spokenHistory.Add($"{now:HH:mm:ss.fff} {text}");
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
                           "key <i|t|j|c|m|f1|escape|...> (real keypress: game's own menu keys)\n" +
                           "select npcs|locations|loot|all | cycle [back] | category next|prev\n" +
                           "navigate | interact | stop | announce\n" +
                           "dialog | continue\n" +
                           "teleport <x> <y> <z> | quickload | devmode\n" +
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
                        // Fully transparent text is present in the hierarchy but not readable.
                        if (t.color.a < 0.05f) continue;

                        sb.AppendLine($"[{t.transform.parent?.name ?? "?"}] {t.text.Replace("\n", " / ").Trim()}");
                        if (++shown >= 120) { sb.AppendLine("... (truncated)"); break; }
                    }
                    return shown == 0 ? "(no visible text)" : sb.ToString().TrimEnd();
                }

                case "key":
                {
                    // Presses a real keyboard key at OS level, so the game's own input
                    // system sees it exactly like a player's keypress (Unity's Input is
                    // read-only, and the game's menus are driven by its own bindings:
                    // i=inventory, t=thought cabinet, j=journal, c=character sheet, m=map).
                    if (parts.Length < 2) return "usage: key <i|t|j|c|m|f1|escape|tab|space|...>";
                    if (!TryPressKey(parts[1], out var error)) return error;
                    return $"pressed {parts[1]}";
                }

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
                    if (verb == "quickload") persistence.DoQuickLoad();
                    else persistence.LoadNewest();
                    return $"{verb} triggered";
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

        private const uint KEYEVENTF_KEYUP = 0x0002;

        private static readonly Dictionary<string, byte> VirtualKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            ["escape"] = 0x1B, ["tab"] = 0x09, ["space"] = 0x20, ["return"] = 0x0D, ["enter"] = 0x0D,
            ["up"] = 0x26, ["down"] = 0x28, ["left"] = 0x25, ["right"] = 0x27,
            ["f1"] = 0x70, ["f5"] = 0x74, ["f9"] = 0x78,
        };

        /// <summary>Presses a key at OS level (the game reads real OS input, which Unity's Input API cannot fake).</summary>
        private static bool TryPressKey(string name, out string error)
        {
            error = null;
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

            keybd_event(vk, 0, 0, System.UIntPtr.Zero);
            keybd_event(vk, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
            return true;
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
