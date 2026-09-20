using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace Patstrap
{
    [CustomEditor(typeof(PatstrapSdkManager))]
    public class PatstrapSdkInspector : Editor
    {
        // ── State ────────────────────────────────────────────────────────────

        private static PatstrapManager _manager;
        private static List<PatstrapDevice> _scannedDevices = new List<PatstrapDevice>();
        private static string _scanStatus = "";
        private static bool _isScanning = false;

        // Devices currently mid-connection (running Connect() on a background thread)
        private static readonly HashSet<int> _connecting = new HashSet<int>();

        // Incremented on every new scan; background callbacks check this before
        // touching any shared state so stale results from the previous scan are
        // silently discarded instead of crashing on disposed native handles.
        private static int _scanGeneration = 0;

        // Used by the animation ticker registered on EditorApplication.update
        private static bool _tickerRegistered = false;

        // Per-device haptic motor slider strengths: device-index → motor-id → strength
        private static Dictionary<int, Dictionary<int, float>> _strengths =
            new Dictionary<int, Dictionary<int, float>>();

        // Per-device cached haptic lists (refreshed once after connect)
        private static Dictionary<int, List<PatstrapHaptic>> _haptics =
            new Dictionary<int, List<PatstrapHaptic>>();

        // Per-device cached battery values: device-index → (battery, lastFetchTime)
        private static Dictionary<int, (float level, double time)> _battery =
            new Dictionary<int, (float, double)>();
        private const double BatteryRefreshInterval = 15.0;

        // Per-device cached RSSI values: device-index → (rssi dBm, lastFetchTime)
        private static Dictionary<int, (short rssi, double time)> _rssi =
            new Dictionary<int, (short, double)>();
        private const double RssiRefreshInterval = 10.0;

        // ── Styles (lazy init) ───────────────────────────────────────────────

        private static GUIStyle _panelStyle;
        private static GUIStyle _titleStyle;
        private static GUIStyle _subtitleStyle;
        private static GUIStyle _deviceNameStyle;
        private static GUIStyle _statusStyle;
        private static GUIStyle _hapticLabelStyle;
        private static GUIStyle _addressStyle;

        private static Color _accentBlue     = new Color(0.27f, 0.57f, 1.00f);
        private static Color _accentGreen    = new Color(0.24f, 0.85f, 0.55f);
        private static Color _accentRed      = new Color(1.00f, 0.35f, 0.35f);
        private static Color _accentOrange   = new Color(1.00f, 0.65f, 0.15f);
        private static Color _bgDark         = new Color(0.13f, 0.14f, 0.17f);
        private static Color _bgCard         = new Color(0.17f, 0.19f, 0.23f);
        private static Color _bgHaptic       = new Color(0.20f, 0.22f, 0.27f);
        private static Color _textPrimary    = new Color(0.92f, 0.93f, 0.95f);
        private static Color _textSecondary  = new Color(0.55f, 0.60f, 0.68f);

        private void InitStyles()
        {
            if (_panelStyle != null) return;

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding  = new RectOffset(14, 14, 12, 12),
                margin   = new RectOffset(0, 0, 6, 6),
                normal   = { background = MakeTex(1, 1, _bgCard) }
            };

            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = _textPrimary }
            };

            _subtitleStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 11,
                normal    = { textColor = _textSecondary }
            };

            _deviceNameStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize  = 13,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = _textPrimary }
            };

            _addressStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                normal   = { textColor = _textSecondary }
            };

            _statusStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize  = 11,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = _textSecondary }
            };

            _hapticLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                normal   = { textColor = _accentBlue }
            };
        }

        // ── Texture helpers ───────────────────────────────────────────────────

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h);
            var pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private static void DrawHRule(Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUILayout.Box(GUIContent.none, GUILayout.ExpandWidth(true), GUILayout.Height(1));
            GUI.color = old;
        }

        // ── Inspector GUI ─────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            InitStyles();
            EnsureManager();

            // ── Header ───────────────────────────────────────────────────────
            GUILayout.Space(6);
            DrawHeader();
            GUILayout.Space(10);

            // ── Scan button ──────────────────────────────────────────────────
            DrawScanSection();
            GUILayout.Space(8);

            // ── Device cards ─────────────────────────────────────────────────
            if (_scannedDevices.Count == 0 && !_isScanning && _scanStatus == "")
            {
                EditorGUILayout.LabelField("No devices found. Press Scan to search.", _statusStyle);
            }
            else if (_scannedDevices.Count == 0 && _scanStatus != "")
            {
                EditorGUILayout.LabelField(_scanStatus, _statusStyle);
            }

            for (int i = 0; i < _scannedDevices.Count; i++)
            {
                DrawDeviceCard(i, _scannedDevices[i]);
                GUILayout.Space(4);
            }
        }

        // ── Header ───────────────────────────────────────────────────────────

        private void DrawHeader()
        {
            Color old = GUI.color;
            GUI.color = _bgDark;
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                GUI.color = old;
                GUILayout.Space(8);
                GUILayout.Label("⚡ PATSTRAP SDK", _titleStyle);
                GUILayout.Label("BLE Haptic Device Manager", _subtitleStyle);
                GUILayout.Space(6);
            }
            GUI.color = old;
        }

        // ── Scan section ──────────────────────────────────────────────────────

        private void DrawScanSection()
        {
            // Block scanning while a background connect is in-flight — touching
            // device handles from two threads simultaneously crashes the native lib.
            bool busyConnecting = _connecting.Count > 0;

            EditorGUI.BeginDisabledGroup(busyConnecting);
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = busyConnecting ? _textSecondary : _accentBlue;
            bool doScan = GUILayout.Button(
                _isScanning ? "Scanning…" :
                busyConnecting ? "Scan (wait for connect…)" : "Scan for Devices",
                GUILayout.Height(38));
            GUI.backgroundColor = prevBg;
            EditorGUI.EndDisabledGroup();

            if (doScan && !_isScanning && !busyConnecting)
            {
                DoScan();
            }

            if (_scanStatus != "" && !_isScanning)
            {
                Color c = GUI.color;
                GUI.color = _textSecondary;
                GUILayout.Label(_scanStatus, _statusStyle);
                GUI.color = c;
            }
        }

        private void DoScan()
        {
            _isScanning = true;
            _scanStatus = "Scanning…";
            // Advance the generation so any still-running Task.Run callbacks
            // from a previous scan know their results are no longer wanted.
            _scanGeneration++;

            // Disconnect and clear old devices
            foreach (var d in _scannedDevices)
            {
                try { if (d.IsConnected) d.Disconnect(); } catch { }
                try { d.Dispose(); } catch { }
            }
            _scannedDevices.Clear();
            _strengths.Clear();
            _haptics.Clear();
            _battery.Clear();
            _connecting.Clear();

            try
            {
                _scannedDevices = _manager.Scan();
                _scanStatus = _scannedDevices.Count > 0
                    ? $"Found {_scannedDevices.Count} device(s)."
                    : "No devices found.";
            }
            catch (Exception e)
            {
                _scanStatus = $"Scan failed: {e.Message}";
                Debug.LogError(e);
            }
            finally
            {
                _isScanning = false;
                Repaint();
            }
        }

        // ── Device card ───────────────────────────────────────────────────────

        private void DrawDeviceCard(int idx, PatstrapDevice device)
        {
            bool connected  = device.IsConnected;
            bool connecting = _connecting.Contains(idx);

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = _bgCard;

            using (new EditorGUILayout.VerticalScope(_panelStyle))
            {
                GUI.backgroundColor = oldBg;

                // — Device header row —
                using (new EditorGUILayout.HorizontalScope())
                {
                    // Status dot — pulses via a simple time-based sine while connecting
                    Color dotColor;
                    if (connecting)
                    {
                        float pulse = (Mathf.Sin((float)EditorApplication.timeSinceStartup * 4f) + 1f) * 0.5f;
                        dotColor = Color.Lerp(_textSecondary, _accentBlue, pulse);
                    }
                    else
                    {
                        dotColor = connected ? _accentGreen : _textSecondary;
                    }

                    Color c = GUI.color;
                    GUI.color = dotColor;
                    GUILayout.Label("●", GUILayout.Width(16));
                    GUI.color = c;

                    // Name + address / connecting label
                    using (new EditorGUILayout.VerticalScope())
                    {
                        string name = TryGet(() => device.Name) ?? "(unknown)";
                        string addr = TryGet(() => device.Address) ?? "";
                        GUILayout.Label(name, _deviceNameStyle);

                        if (connecting)
                        {
                            int dots = (int)(EditorApplication.timeSinceStartup * 2.5) % 4;
                            GUILayout.Label("Connecting" + new string('.', dots), _addressStyle);
                        }
                        else
                        {
                            GUILayout.Label(addr, _addressStyle);
                        }
                    }

                    GUILayout.FlexibleSpace();

                    // Connect / Disconnect button
                    EditorGUI.BeginDisabledGroup(connecting);
                    if (connecting)
                    {
                        GUI.backgroundColor = _accentBlue;
                        GUILayout.Button("Connecting…", GUILayout.Width(90), GUILayout.Height(28));
                    }
                    else
                    {
                        GUI.backgroundColor = connected ? _accentRed : _accentGreen;
                        if (GUILayout.Button(connected ? "Disconnect" : "Connect",
                                             GUILayout.Width(90), GUILayout.Height(28)))
                        {
                            ToggleConnection(idx, device);
                            connected = device.IsConnected;
                        }
                    }
                    EditorGUI.EndDisabledGroup();
                    GUI.backgroundColor = oldBg;
                }

                if (!connected || connecting) return;

                GUILayout.Space(8);
                DrawHRule(new Color(0.30f, 0.34f, 0.42f));
                GUILayout.Space(8);

                // — Battery —
                DrawBattery(idx, device);

                GUILayout.Space(6);

                // — RSSI —
                DrawRssi(idx, device);

                GUILayout.Space(8);
                DrawHRule(new Color(0.30f, 0.34f, 0.42f));
                GUILayout.Space(8);

                // — Haptics —
                DrawHaptics(idx, device);
            }

            GUI.backgroundColor = oldBg;
        }

        private void ToggleConnection(int idx, PatstrapDevice device)
        {
            if (device.IsConnected)
            {
                try
                {
                    device.Disconnect();
                    _haptics.Remove(idx);
                    _battery.Remove(idx);
                    _strengths.Remove(idx);
                    Debug.Log($"[PatstrapSDK] Disconnected from device {idx}.");
                }
                catch (Exception e) { Debug.LogError(e); }
                Repaint();
            }
            else
            {
                // Run the blocking Connect call on a background thread so the
                // editor UI stays responsive with an animated connecting indicator.
                _connecting.Add(idx);
                EnsureAnimationTicker();
                Repaint();

                // Capture the current generation so the callback can detect
                // whether a new scan invalidated this connection attempt.
                int capturedGeneration = _scanGeneration;

                Task.Run(() =>
                {
                    try
                    {
                        device.Connect();
                        List<PatstrapHaptic> haptics = device.GetHaptics();
                        // Post results back to the main Unity/Editor thread
                        EditorApplication.delayCall += () =>
                        {
                            // Drop stale results if a new scan already ran
                            if (_scanGeneration != capturedGeneration) return;
                            _connecting.Remove(idx);
                            _haptics[idx] = haptics;
                            Debug.Log($"[PatstrapSDK] Connected to device {idx} — {haptics.Count} haptic(s).");
                            Repaint();
                        };
                    }
                    catch (Exception e)
                    {
                        EditorApplication.delayCall += () =>
                        {
                            if (_scanGeneration != capturedGeneration) return;
                            _connecting.Remove(idx);
                            Debug.LogError($"[PatstrapSDK] {e.Message}");
                            Repaint();
                        };
                    }
                });
            }
        }

        // Register a single EditorApplication.update callback that repaints
        // the inspector while any device is connecting, so the dot animation runs.
        private void EnsureAnimationTicker()
        {
            if (_tickerRegistered) return;
            _tickerRegistered = true;
            EditorApplication.update += AnimationTick;
        }

        private static void AnimationTick()
        {
            if (_connecting.Count == 0)
            {
                EditorApplication.update -= AnimationTick;
                _tickerRegistered = false;
                return;
            }
            // Force all inspector windows to repaint so the animation is smooth
            EditorApplication.QueuePlayerLoopUpdate();
            foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
                w.Repaint();
        }

        // ── Battery ───────────────────────────────────────────────────────────

        private void DrawBattery(int idx, PatstrapDevice device)
        {
            // Refresh if stale
            double now = EditorApplication.timeSinceStartup;
            if (!_battery.ContainsKey(idx) || now >= _battery[idx].time + BatteryRefreshInterval)
            {
                try
                {
                    float b = device.GetBattery();
                    _battery[idx] = (b, now);
                }
                catch { /* not available yet */ }
            }

            if (!_battery.ContainsKey(idx)) return;

            float level = _battery[idx].level;
            int pct = Mathf.RoundToInt(level * 100f);

            Color barColor = Color.Lerp(_accentRed, _accentGreen, level);

            using (new EditorGUILayout.HorizontalScope())
            {
                Color c = GUI.color;
                GUI.color = _textSecondary;
                GUILayout.Label("🔋 Battery", GUILayout.Width(70));
                GUI.color = c;

                Rect fullRect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(fullRect, new Color(0.10f, 0.11f, 0.14f));
                Rect fill = new Rect(fullRect.x, fullRect.y, fullRect.width * level, fullRect.height);
                EditorGUI.DrawRect(fill, barColor);
                EditorGUI.LabelField(fullRect, $"{pct}%", new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize  = 10,
                    normal    = { textColor = Color.white }
                });

                GUILayout.Space(4);
                Color c2 = GUI.color;
                GUI.color = barColor;
                GUILayout.Label($"{pct}%", GUILayout.Width(36));
                GUI.color = c2;
            }
        }

        // ── RSSI ──────────────────────────────────────────────────────────────

        private void DrawRssi(int idx, PatstrapDevice device)
        {
            double now = EditorApplication.timeSinceStartup;
            if (!_rssi.ContainsKey(idx) || now >= _rssi[idx].time + RssiRefreshInterval)
            {
                try
                {
                    short r = device.GetRssi();
                    _rssi[idx] = (r, now);
                }
                catch { /* not available yet */ }
            }

            if (!_rssi.ContainsKey(idx)) return;

            short rssi = _rssi[idx].rssi;

            // Map RSSI to 0-1: treat -50 dBm as perfect, -100 dBm as worst
            const float rssiMin = -100f;
            const float rssiMax = -50f;
            float t = Mathf.Clamp01((rssi - rssiMin) / (rssiMax - rssiMin));
            Color sigColor = Color.Lerp(_accentRed, _accentGreen, t);

            // Determine quality label
            string quality = t >= 0.8f ? "Excellent" :
                             t >= 0.6f ? "Good" :
                             t >= 0.4f ? "Fair" :
                             t >= 0.2f ? "Weak" : "Poor";

            using (new EditorGUILayout.HorizontalScope())
            {
                Color c = GUI.color;
                GUI.color = _textSecondary;
                GUILayout.Label("📶 Signal", GUILayout.Width(70));
                GUI.color = c;

                // 5-bar signal strength display
                int filledBars = Mathf.RoundToInt(t * 5f);
                for (int b = 1; b <= 5; b++)
                {
                    Rect barRect = GUILayoutUtility.GetRect(10, 18, GUILayout.Width(10));
                    float heightFrac = b / 5f;
                    // Each bar grows taller as b increases (stepped style)
                    Rect bg = new Rect(barRect.x, barRect.y, barRect.width - 2, barRect.height);
                    EditorGUI.DrawRect(bg, new Color(0.10f, 0.11f, 0.14f));

                    if (b <= filledBars)
                    {
                        float barH = barRect.height * heightFrac;
                        Rect fill = new Rect(
                            barRect.x,
                            barRect.yMax - barH,
                            barRect.width - 2,
                            barH);
                        EditorGUI.DrawRect(fill, sigColor);
                    }
                }

                GUILayout.Space(6);

                // dBm + quality label
                Color c2 = GUI.color;
                GUI.color = sigColor;
                GUILayout.Label($"{rssi} dBm", GUILayout.Width(58));
                GUI.color = _textSecondary;
                GUILayout.Label(quality);
                GUI.color = c2;
            }
        }


        private void DrawHaptics(int idx, PatstrapDevice device)
        {
            if (!_haptics.ContainsKey(idx))
            {
                try  { _haptics[idx] = device.GetHaptics(); }
                catch { return; }
            }

            var hapticList = _haptics[idx];
            if (hapticList == null || hapticList.Count == 0)
            {
                EditorGUILayout.LabelField("No haptic motors found.", _statusStyle);
                return;
            }

            GUILayout.Label("Haptic Motors", new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal   = { textColor = _textPrimary }
            });
            GUILayout.Space(4);

            if (!_strengths.ContainsKey(idx))
                _strengths[idx] = new Dictionary<int, float>();

            Color oldBg = GUI.backgroundColor;

            for (int m = 0; m < hapticList.Count; m++)
            {
                var haptic = hapticList[m];
                if (!_strengths[idx].ContainsKey(m))
                    _strengths[idx][m] = 1f;

                GUI.backgroundColor = _bgHaptic;
                using (new EditorGUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUI.backgroundColor = oldBg;

                    GUILayout.Label($"Motor {m}", _hapticLabelStyle, GUILayout.Width(64));
                    GUILayout.Space(4);

                    float newStrength = EditorGUILayout.Slider(_strengths[idx][m], 0f, 1f);
                    _strengths[idx][m] = newStrength;

                    GUILayout.Space(6);

                    GUI.backgroundColor = _accentOrange;
                    if (GUILayout.Button("Vibrate", GUILayout.Width(80), GUILayout.Height(22)))
                    {
                        try
                        {
                            haptic.Vibrate(_strengths[idx][m], 1000);
                            Debug.Log($"[PatstrapSDK] Motor {m} on device {idx}: vibrate {_strengths[idx][m]:P0} for 1 s.");
                        }
                        catch (Exception e) { Debug.LogError(e); }
                    }
                    GUI.backgroundColor = oldBg;
                }

                GUILayout.Space(2);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void EnsureManager()
        {
            if (_manager != null) return;
            try { _manager = new PatstrapManager(); }
            catch (Exception e)
            {
                Debug.LogError($"[PatstrapSDK] Could not create PatstrapManager: {e.Message}");
            }
        }

        private static T TryGet<T>(Func<T> fn) where T : class
        {
            try { return fn(); } catch { return null; }
        }
    }
}