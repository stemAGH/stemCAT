// stemCAT - entry point
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("stemCAT")]
[assembly: AssemblyDescription("A small cat that lives on your desktop and types along with you.")]
[assembly: AssemblyProduct("stemCAT")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

namespace stemCAT
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--render-test")
                return RenderTest(args[1]);

            bool created;
            using (Mutex m = new Mutex(true, "stemCAT_SingleInstance_v1", out created))
            {
                if (!created) return 0;   // already running: the cat is in your tray
                if (Native.IsWindows) { try { Native.SetProcessDPIAware(); } catch { } }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new CatForm());
            }
            return 0;
        }

        // Renders each state of every character to PNG files (used for automated visual testing).
        static int RenderTest(string dir)
        {
            Directory.CreateDirectory(dir);
            int n = 0;
            string[] states = { "idle", "type_left", "type_right", "type_both", "mouse_click", "pointer_near",
                                "daydream", "sleep", "pet", "privacy", "greenscreen", "noflip", "nopops" };
            foreach (string st in states)
            {
                Save(Simulate(st, Coat.Presets()[0]), Path.Combine(dir, st + ".png"), 1.5f, st == "greenscreen"); n++;
            }
            foreach (Character ch in Character.All())
            {
                int i = 0;
                foreach (Coat c in ch.Palettes)
                {
                    if (ch.Id == "cat" && i > 2) break;
                    Save(Simulate("type_both", c, ch), Path.Combine(dir, string.Format("char_{0}_{1}.png", ch.Id, i++)), 1.0f, false); n++;
                }
                foreach (string st in new string[] { "sleep", "pet", "pointer_near" })
                {
                    Save(Simulate(st, ch.Palettes[0], ch), Path.Combine(dir, string.Format("state_{0}_{1}.png", ch.Id, st)), 1.0f, false); n++;
                }
                using (Bitmap icon = new CatScene(ch.Palettes[0]).RenderIcon(64))
                    icon.Save(Path.Combine(dir, "icon_" + ch.Id + ".png"), ImageFormat.Png);
            }
            Console.WriteLine("rendered " + n + " images to " + dir);
            return 0;
        }

        static CatScene Simulate(string state, Coat coat)
        {
            return Simulate(state, coat, Character.Get(coat.CharacterId));
        }

        static CatScene Simulate(string state, Coat coat, Character ch)
        {
            CatScene s = new CatScene(coat.Clone());
            s.Char = ch;
            s.KeyboardColor = ch.Keyboard; s.KeycapColor = ch.Keycap; s.GlowColor = ch.Glow; s.MouseColor = ch.Mouse;
            double t = 0;
            s.Now = 0; s.Poke();
            s.Cursor = new PointF(20, 10);
            Action<double> run = delegate (double secs)
            {
                int k = (int)(secs / 0.033);
                for (int j = 0; j < k; j++) { t += 0.033; s.Update(t); }
            };
            run(0.5);
            switch (state)
            {
                case "type_left": s.KeyDown(0x41); run(0.12); break;
                case "type_right": s.KeyDown(0x4F); run(0.12); break;
                case "type_both": s.KeyDown(0x52); run(0.05); s.KeyDown(0xBA); run(0.12); break;
                case "mouse_click": s.MouseNorm = new PointF(0.6f, -0.4f); s.MouseMoved(); s.MouseButton(0, true); s.KeyDown(0x20); run(0.15); break;
                case "pointer_near": s.Cursor = new PointF(205, 70); s.MouseMoved(); run(0.4); break;
                case "daydream": s.SetIdleFor(20); run(1.5); break;
                case "sleep": s.SetIdleFor(60); run(3.0); break;
                case "pet": s.Petting = true; s.PetPoint = new PointF(185, 60); run(1.2); break;
                case "privacy": s.Privacy = true; s.KeyDown(0x52); s.KeyDown(0x4C); run(0.12); break;
                case "greenscreen": s.GreenScreen = true; s.KeyDown(0x20); run(0.12); break;
                case "noflip": s.FlipKeyboard = false; s.KeyDown(0x51); run(0.05); s.KeyDown(0x0D); run(0.12); break;
                case "nopops": s.KeyPops = false; s.KeyDown(0x51); run(0.05); s.KeyDown(0x0D); run(0.12); break;
            }
            return s;
        }

        static void Save(CatScene s, string path, float scale, bool opaque)
        {
            int w = (int)Math.Ceiling(CatScene.W * scale), h = (int)Math.Ceiling(CatScene.H * scale);
            using (Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(b))
                {
                    // checkerboard so transparency is visible in test images
                    if (!opaque)
                        for (int y = 0; y < h; y += 16)
                            for (int x = 0; x < w; x += 16)
                                using (SolidBrush cb = new SolidBrush(((x + y) / 16) % 2 == 0 ? Color.FromArgb(255, 70, 110, 160) : Color.FromArgb(255, 82, 124, 176)))
                                    g.FillRectangle(cb, x, y, 16, 16);
                    g.ScaleTransform(scale, scale);
                    s.Draw(g);
                }
                s.ApplyGlitch(b);
                b.Save(path, ImageFormat.Png);
            }
        }
    }
}
