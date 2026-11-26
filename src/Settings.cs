// stemCAT - settings persisted to %APPDATA%\stemCAT\settings.ini
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

namespace stemCAT
{
    public class Settings
    {
        public Coat Coat;
        public List<Coat> CustomCoats = new List<Coat>();
        public Color KeyboardColor = ColorTranslator.FromHtml("#5E6A7D");
        public Color KeycapColor = ColorTranslator.FromHtml("#F4F1EA");
        public Color GlowColor = ColorTranslator.FromHtml("#FFC94D");
        public Color MouseColor = ColorTranslator.FromHtml("#F4F1EA");
        public float Scale = 1.0f;
        public int Opacity = 100;
        public bool ClickThrough;
        public bool Privacy;
        public bool GreenScreen;
        public bool FlipKeyboard = true;
        public bool KeyPops = true;
        public bool LockPosition;
        public bool HasPosition;
        public int X, Y;

        public Settings()
        {
            Coat = Coat.Presets()[0];
        }

        public static string FolderPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "stemCAT"); }
        }

        public static string FilePath
        {
            get { return Path.Combine(FolderPath, "settings.ini"); }
        }

        static string Hex(Color c) { return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B); }

        public Character Character
        {
            get { return Character.Get(Coat.CharacterId); }
        }

        // switch character: take its first colour palette and its desk colours
        public void UseCharacter(Character c)
        {
            Coat = c.Palettes[0].Clone();
            KeyboardColor = c.Keyboard; KeycapColor = c.Keycap; GlowColor = c.Glow; MouseColor = c.Mouse;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(FolderPath);
                StringBuilder sb = new StringBuilder();
                CultureInfo ci = CultureInfo.InvariantCulture;
                sb.AppendLine("# stemCAT settings. Key presses are never stored here or anywhere else.");
                sb.AppendLine("coat=" + Coat.Serialize());
                foreach (Coat c in CustomCoats) sb.AppendLine("custom=" + c.Serialize());
                sb.AppendLine("keyboard=" + Hex(KeyboardColor));
                sb.AppendLine("keycap=" + Hex(KeycapColor));
                sb.AppendLine("glow=" + Hex(GlowColor));
                sb.AppendLine("mouse=" + Hex(MouseColor));
                sb.AppendLine("scale=" + Scale.ToString(ci));
                sb.AppendLine("opacity=" + Opacity.ToString(ci));
                sb.AppendLine("clickthrough=" + (ClickThrough ? "1" : "0"));
                sb.AppendLine("privacy=" + (Privacy ? "1" : "0"));
                sb.AppendLine("greenscreen=" + (GreenScreen ? "1" : "0"));
                sb.AppendLine("flipkeyboard=" + (FlipKeyboard ? "1" : "0"));
                sb.AppendLine("keypops=" + (KeyPops ? "1" : "0"));
                sb.AppendLine("lockposition=" + (LockPosition ? "1" : "0"));
                if (HasPosition) sb.AppendLine("pos=" + X.ToString(ci) + "," + Y.ToString(ci));
                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                string loadPath = FilePath;
                // Import settings from the previous app name on first launch.
                if (!File.Exists(loadPath))
                    loadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskCat", "settings.ini");
                if (!File.Exists(loadPath)) return s;
                CultureInfo ci = CultureInfo.InvariantCulture;
                foreach (string raw in File.ReadAllLines(loadPath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string v = line.Substring(eq + 1).Trim();
                    try
                    {
                        switch (k)
                        {
                            case "coat": { Coat c = Coat.Deserialize(v); if (c != null) s.Coat = c; break; }
                            case "custom": { Coat c = Coat.Deserialize(v); if (c != null) s.CustomCoats.Add(c); break; }
                            case "keyboard": s.KeyboardColor = ColorTranslator.FromHtml(v); break;
                            case "keycap": s.KeycapColor = ColorTranslator.FromHtml(v); break;
                            case "glow": s.GlowColor = ColorTranslator.FromHtml(v); break;
                            case "mouse": s.MouseColor = ColorTranslator.FromHtml(v); break;
                            case "scale": s.Scale = Math.Max(0.4f, Math.Min(2.5f, float.Parse(v, ci))); break;
                            case "opacity": s.Opacity = Math.Max(15, Math.Min(100, int.Parse(v, ci))); break;
                            case "clickthrough": s.ClickThrough = v == "1"; break;
                            case "privacy": s.Privacy = v == "1"; break;
                            case "greenscreen": s.GreenScreen = v == "1"; break;
                            case "flipkeyboard": s.FlipKeyboard = v == "1"; break;
                            case "keypops": s.KeyPops = v == "1"; break;
                            case "lockposition": s.LockPosition = v == "1"; break;
                            case "pos":
                                {
                                    string[] p = v.Split(',');
                                    s.X = int.Parse(p[0], ci); s.Y = int.Parse(p[1], ci); s.HasPosition = true;
                                    break;
                                }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return s;
        }
    }
}
