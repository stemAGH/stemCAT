// stemCAT - the transparent, always-on-top window that hosts the cat
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace stemCAT
{
    public class CatForm : Form
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const int HK_CLICKTHROUGH = 1, HK_PRIVACY = 2;

        readonly Settings settings;
        readonly CatScene scene;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Timer timer = new Timer();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        Bitmap frame;
        Icon appIcon;

        IntPtr kbHook = IntPtr.Zero, msHook = IntPtr.Zero;
        Native.HookProc kbProc, msProc;   // kept as fields so the GC never collects them

        bool dragging, petting;
        Point dragOffset, pressPoint;
        double lastKeyCheck, lastTopmost;
        SettingsForm settingsForm;

        public CatForm()
        {
            settings = Settings.Load();
            if (StartupEnabled) StartupEnabled = true;
            scene = new CatScene(settings.Coat.Clone());
            SyncScene();

            Text = "stemCAT";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Size = PixelSize;
            Location = StartLocation();

            appIcon = MakeIcon();
            Icon = appIcon;

            menu.Items.Add(new ToolStripMenuItem("..."));   // placeholder so Opening fires
            menu.Opening += delegate (object s, System.ComponentModel.CancelEventArgs e) { BuildMenu(); e.Cancel = false; };

            tray.Icon = appIcon;
            tray.Text = "stemCAT";
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { OpenSettings(); };
            tray.Visible = true;

            timer.Interval = 33;
            timer.Tick += OnTick;
        }

        Size PixelSize
        {
            get { return new Size((int)Math.Ceiling(CatScene.W * settings.Scale), (int)Math.Ceiling(CatScene.H * settings.Scale)); }
        }

        public void SnapTo(string where)
        {
            Size sz = PixelSize;
            Rectangle wa = Screen.FromPoint(new Point(Left + sz.Width / 2, Top + sz.Height / 2)).WorkingArea;
            int m = 16, x, y;
            switch (where)
            {
                case "Bottom left": x = wa.Left + m; y = wa.Bottom - sz.Height - 4; break;
                case "Bottom center": x = wa.Left + (wa.Width - sz.Width) / 2; y = wa.Bottom - sz.Height - 4; break;
                case "Top left": x = wa.Left + m; y = wa.Top + m; break;
                case "Top center": x = wa.Left + (wa.Width - sz.Width) / 2; y = wa.Top + m; break;
                case "Top right": x = wa.Right - sz.Width - m; y = wa.Top + m; break;
                case "Center": x = wa.Left + (wa.Width - sz.Width) / 2; y = wa.Top + (wa.Height - sz.Height) / 2; break;
                case "Next monitor":
                    {
                        Screen[] all = Screen.AllScreens;
                        Screen cur = Screen.FromPoint(new Point(Left + sz.Width / 2, Top + sz.Height / 2));
                        int idx = Array.IndexOf(all, cur);
                        for (int i = 0; i < all.Length; i++) if (all[i].DeviceName == cur.DeviceName) idx = i;
                        Rectangle nw = all[(idx + 1) % all.Length].WorkingArea;
                        x = nw.Right - sz.Width - 24; y = nw.Bottom - sz.Height - 4;
                        break;
                    }
                default: x = wa.Right - sz.Width - 24; y = wa.Bottom - sz.Height - 4; break;
            }
            Location = new Point(x, y);
            settings.HasPosition = true; settings.X = x; settings.Y = y;
            settings.Save();
            RenderFrame();
        }

        public static readonly string[] PositionNames = { "Bottom right", "Bottom center", "Bottom left", "Top right", "Top center", "Top left", "Center" };

        Point StartLocation()
        {
            Size sz = PixelSize;
            if (settings.HasPosition)
            {
                Rectangle r = new Rectangle(settings.X, settings.Y, sz.Width, sz.Height);
                foreach (Screen sc in Screen.AllScreens)
                {
                    Rectangle i = Rectangle.Intersect(sc.WorkingArea, r);
                    if (i.Width > 60 && i.Height > 60) return r.Location;
                }
            }
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            return new Point(wa.Right - sz.Width - 24, wa.Bottom - sz.Height - 4);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST;
                if (settings != null && settings.ClickThrough) cp.ExStyle |= Native.WS_EX_TRANSPARENT;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (Native.IsWindows)
            {
                InstallHooks();
                Native.RegisterHotKey(Handle, HK_CLICKTHROUGH, Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, (uint)'L');
                Native.RegisterHotKey(Handle, HK_PRIVACY, Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, (uint)'P');
            }
            scene.Now = clock.Elapsed.TotalSeconds;
            scene.Poke();
            timer.Start();
            RenderFrame();
            if (settings.ClickThrough)
                tray.ShowBalloonTip(2500, "stemCAT", "Click-through lock is on. Press Ctrl+Alt+L to unlock.", ToolTipIcon.Info);
        }

        // ------------------------------------------------------------ global input (in-memory only)
        void InstallHooks()
        {
            kbProc = KeyboardHook;
            msProc = MouseHook;
            IntPtr mod = Native.GetModuleHandle(null);
            kbHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, kbProc, mod, 0);
            msHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, msProc, mod, 0);
        }

        void RemoveHooks()
        {
            if (kbHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(kbHook); kbHook = IntPtr.Zero; }
            if (msHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(msHook); msHook = IntPtr.Zero; }
        }

        IntPtr KeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    Native.KBDLLHOOKSTRUCT k = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                    int msg = wParam.ToInt32();
                    if (msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN) scene.KeyDown((int)k.vkCode);
                    else if (msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP) scene.KeyUp((int)k.vkCode);
                }
                catch { }
            }
            return Native.CallNextHookEx(kbHook, nCode, wParam, lParam);
        }

        IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    int msg = wParam.ToInt32();
                    switch (msg)
                    {
                        case Native.WM_MOUSEMOVE: scene.MouseMoved(); break;
                        case Native.WM_LBUTTONDOWN: scene.MouseButton(0, true); break;
                        case Native.WM_LBUTTONUP: scene.MouseButton(0, false); break;
                        case Native.WM_RBUTTONDOWN: scene.MouseButton(1, true); break;
                        case Native.WM_RBUTTONUP: scene.MouseButton(1, false); break;
                        case Native.WM_MBUTTONDOWN: scene.MouseButton(2, true); break;
                        case Native.WM_MBUTTONUP: scene.MouseButton(2, false); break;
                        case Native.WM_MOUSEWHEEL:
                        case Native.WM_MOUSEHWHEEL:
                            {
                                Native.MSLLHOOKSTRUCT m = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                                scene.Wheel((short)((m.mouseData >> 16) & 0xFFFF));
                                break;
                            }
                    }
                }
                catch { }
            }
            return Native.CallNextHookEx(msHook, nCode, wParam, lParam);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HK_CLICKTHROUGH) ToggleClickThrough();
                else if (id == HK_PRIVACY) TogglePrivacy();
            }
            base.WndProc(ref m);
        }

        // ------------------------------------------------------------ frame loop
        void OnTick(object sender, EventArgs e)
        {
            double t = clock.Elapsed.TotalSeconds;
            float sc = settings.Scale;
            Point c = Cursor.Position;
            scene.Cursor = new PointF((c.X - Left) / sc, (c.Y - Top) / sc);
            scene.PixelScale = sc;
            Rectangle sb = Screen.FromPoint(c).Bounds;
            scene.MouseNorm = new PointF(
                Math.Max(-1f, Math.Min(1f, (c.X - sb.Left) / (float)Math.Max(1, sb.Width) * 2f - 1f)),
                Math.Max(-1f, Math.Min(1f, (c.Y - sb.Top) / (float)Math.Max(1, sb.Height) * 2f - 1f)));
            if (petting) { scene.PetPoint = scene.Cursor; scene.Poke(); }

            // forget keys whose release we missed (e.g. after Ctrl+Alt+Del / lock screen)
            if (Native.IsWindows && t - lastKeyCheck > 0.5)
            {
                lastKeyCheck = t;
                foreach (int vk in scene.HeldKeys())
                    if ((Native.GetAsyncKeyState(vk) & 0x8000) == 0) scene.KeyUp(vk);
            }
            // stay above other windows (incl. borderless games)
            if (Native.IsWindows && t - lastTopmost > 3 && !dragging)
            {
                lastTopmost = t;
                Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            }

            scene.Update(t);
            RenderFrame();
        }

        void RenderFrame()
        {
            Size sz = PixelSize;
            if (frame == null || frame.Width != sz.Width || frame.Height != sz.Height)
            {
                if (frame != null) frame.Dispose();
                frame = new Bitmap(sz.Width, sz.Height, PixelFormat.Format32bppArgb);
            }
            using (Graphics g = Graphics.FromImage(frame))
            {
                g.Clear(Color.Transparent);
                g.ScaleTransform(settings.Scale, settings.Scale);
                scene.Draw(g);
            }
            scene.ApplyGlitch(frame);
            if (!Native.IsWindows || !IsHandleCreated) return;

            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            IntPtr hBmp = IntPtr.Zero, old = IntPtr.Zero;
            try
            {
                hBmp = frame.GetHbitmap(Color.FromArgb(0));
                old = Native.SelectObject(memDc, hBmp);
                Native.SIZE size = new Native.SIZE(frame.Width, frame.Height);
                Native.POINT src = new Native.POINT(0, 0);
                Native.POINT dst = new Native.POINT(Left, Top);
                Native.BLENDFUNCTION blend = new Native.BLENDFUNCTION();
                blend.BlendOp = Native.AC_SRC_OVER;
                blend.SourceConstantAlpha = (byte)Math.Max(38, Math.Min(255, settings.Opacity * 255 / 100));
                blend.AlphaFormat = Native.AC_SRC_ALPHA;
                Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, Native.ULW_ALPHA);
            }
            finally
            {
                Native.ReleaseDC(IntPtr.Zero, screenDc);
                if (hBmp != IntPtr.Zero) { Native.SelectObject(memDc, old); Native.DeleteObject(hBmp); }
                Native.DeleteDC(memDc);
            }
        }

        // ------------------------------------------------------------ interaction on the cat
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            PointF p = new PointF(e.X / settings.Scale, e.Y / settings.Scale);
            if (e.Button == MouseButtons.Right)
            {
                menu.Show(Cursor.Position);
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            pressPoint = Cursor.Position;
            dragOffset = new Point(Cursor.Position.X - Left, Cursor.Position.Y - Top);
            if (scene.HitHead(p))
            {
                petting = true; scene.Petting = true; scene.PetPoint = p;
            }
            else if (!settings.LockPosition)
            {
                dragging = true;
            }
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            // pulling the head further than a small "petting wiggle" turns the pet into a drag
            if (petting && !settings.LockPosition && (e.Button & MouseButtons.Left) != 0)
            {
                int mdx = Cursor.Position.X - pressPoint.X, mdy = Cursor.Position.Y - pressPoint.Y;
                if (mdx * mdx + mdy * mdy > 18 * 18) { petting = false; scene.Petting = false; dragging = true; }
            }
            if (dragging)
                Location = new Point(Cursor.Position.X - dragOffset.X, Cursor.Position.Y - dragOffset.Y);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragging)
            {
                dragging = false;
                settings.HasPosition = true; settings.X = Left; settings.Y = Top;
                settings.Save();
            }
            petting = false; scene.Petting = false;
            Capture = false;
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) { petting = false; scene.Petting = false; dragging = false; }
        }

        // ------------------------------------------------------------ menu
        void BuildMenu()
        {
            menu.Items.Clear();
            ToolStripMenuItem it;

            it = new ToolStripMenuItem("Settings\u2026"); it.Font = new Font(it.Font, FontStyle.Bold);
            it.Click += delegate { OpenSettings(); };
            menu.Items.Add(it);
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem chars = new ToolStripMenuItem("Character");
            foreach (Character ch in Character.All())
            {
                Character cc = ch;
                ToolStripMenuItem ci = new ToolStripMenuItem(cc.Name.Replace("&", "&&"));
                ci.Checked = cc.Id == settings.Coat.CharacterId;
                ci.Image = Swatch(cc.Palettes[0].Fur);
                ci.Click += delegate { settings.UseCharacter(cc); ApplyAll(); };
                chars.DropDownItems.Add(ci);
            }
            menu.Items.Add(chars);

            ToolStripMenuItem coats = new ToolStripMenuItem("Colours");
            List<Coat> all = new List<Coat>(settings.Character.Palettes);
            foreach (Coat c in settings.CustomCoats) if (c.CharacterId == settings.Coat.CharacterId) all.Add(c);
            foreach (Coat c in all)
            {
                Coat cc = c;
                ToolStripMenuItem ci = new ToolStripMenuItem(cc.Name.Replace("&", "&&"));
                ci.Checked = cc.Name == settings.Coat.Name;
                ci.Image = Swatch(cc.Fur);
                ci.Click += delegate { settings.Coat = cc.Clone(); ApplyAll(); };
                coats.DropDownItems.Add(ci);
            }
            menu.Items.Add(coats);

            ToolStripMenuItem sizes = new ToolStripMenuItem("Size");
            string[] sn = { "Mini", "Small", "Normal", "Large", "Huge" };
            float[] sv = { 0.6f, 0.8f, 1.0f, 1.3f, 1.7f };
            for (int i = 0; i < sn.Length; i++)
            {
                float v = sv[i];
                ToolStripMenuItem si = new ToolStripMenuItem(sn[i]);
                si.Checked = Math.Abs(settings.Scale - v) < 0.01f;
                si.Click += delegate { settings.Scale = v; ApplyAll(); };
                sizes.DropDownItems.Add(si);
            }
            menu.Items.Add(sizes);

            ToolStripMenuItem ops = new ToolStripMenuItem("Opacity");
            foreach (int o in new int[] { 100, 85, 70, 50, 35 })
            {
                int ov = o;
                ToolStripMenuItem oi = new ToolStripMenuItem(ov + "%");
                oi.Checked = settings.Opacity == ov;
                oi.Click += delegate { settings.Opacity = ov; ApplyAll(); };
                ops.DropDownItems.Add(oi);
            }
            menu.Items.Add(ops);
            menu.Items.Add(new ToolStripSeparator());

            it = new ToolStripMenuItem("Click-through lock"); it.ShortcutKeyDisplayString = "Ctrl+Alt+L";
            it.Checked = settings.ClickThrough; it.Click += delegate { ToggleClickThrough(); };
            menu.Items.Add(it);
            it = new ToolStripMenuItem("Privacy mode"); it.ShortcutKeyDisplayString = "Ctrl+Alt+P";
            it.Checked = settings.Privacy; it.Click += delegate { TogglePrivacy(); };
            menu.Items.Add(it);
            it = new ToolStripMenuItem("Key pop-ups");
            it.Checked = settings.KeyPops; it.Click += delegate { settings.KeyPops = !settings.KeyPops; ApplyAll(); };
            menu.Items.Add(it);
            it = new ToolStripMenuItem("Keyboard faces the character");
            it.Checked = settings.FlipKeyboard; it.Click += delegate { settings.FlipKeyboard = !settings.FlipKeyboard; ApplyAll(); };
            menu.Items.Add(it);
            it = new ToolStripMenuItem("Green screen (for streaming)");
            it.Checked = settings.GreenScreen; it.Click += delegate { settings.GreenScreen = !settings.GreenScreen; ApplyAll(); };
            menu.Items.Add(it);
            it = new ToolStripMenuItem("Start with Windows");
            it.Checked = StartupEnabled; it.Click += delegate { StartupEnabled = !StartupEnabled; };
            menu.Items.Add(it);
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem pos = new ToolStripMenuItem("Position");
            foreach (string pn in PositionNames)
            {
                string where = pn;
                ToolStripMenuItem pi = new ToolStripMenuItem(where);
                pi.Click += delegate { SnapTo(where); };
                pos.DropDownItems.Add(pi);
            }
            if (Screen.AllScreens.Length > 1)
            {
                ToolStripMenuItem nm = new ToolStripMenuItem("Next monitor");
                nm.Click += delegate { SnapTo("Next monitor"); };
                pos.DropDownItems.Add(nm);
            }
            pos.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem lk = new ToolStripMenuItem("Lock position (no dragging)");
            lk.Checked = settings.LockPosition;
            lk.Click += delegate { settings.LockPosition = !settings.LockPosition; ApplyAll(); };
            pos.DropDownItems.Add(lk);
            menu.Items.Add(pos);
            it = new ToolStripMenuItem("Quit");
            it.Click += delegate { Close(); };
            menu.Items.Add(it);
        }

        static Image Swatch(Color c)
        {
            Bitmap b = new Bitmap(14, 14);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush br = new SolidBrush(c)) g.FillEllipse(br, 1, 1, 12, 12);
                using (Pen p = new Pen(Color.FromArgb(120, 0, 0, 0))) g.DrawEllipse(p, 1, 1, 12, 12);
            }
            return b;
        }

        void OpenSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed)
            {
                settingsForm = new SettingsForm(settings, ApplyAll, delegate { return StartupEnabled; }, delegate (bool on) { StartupEnabled = on; }, SnapTo);
                settingsForm.Icon = appIcon;
            }
            settingsForm.Show();
            settingsForm.WindowState = FormWindowState.Normal;
            settingsForm.Activate();
        }

        // ------------------------------------------------------------ options
        void SyncScene()
        {
            scene.Coat = settings.Coat.Clone();
            scene.Char = settings.Character;
            scene.FlipKeyboard = settings.FlipKeyboard;
            scene.KeyPops = settings.KeyPops;
            scene.KeyboardColor = settings.KeyboardColor;
            scene.KeycapColor = settings.KeycapColor;
            scene.GlowColor = settings.GlowColor;
            scene.MouseColor = settings.MouseColor;
            scene.Privacy = settings.Privacy;
            scene.GreenScreen = settings.GreenScreen;
        }

        public void ApplyAll()
        {
            SyncScene();
            Size = PixelSize;
            ApplyClickThrough();
            if (appIcon != null)
            {
                Icon old = appIcon;
                appIcon = MakeIcon(); Icon = appIcon; tray.Icon = appIcon;
                if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.Icon = appIcon;
                old.Dispose();
            }
            settings.Save();
            RenderFrame();
        }

        void ApplyClickThrough()
        {
            if (!Native.IsWindows || !IsHandleCreated) return;
            int ex = Native.GetWindowLong(Handle, Native.GWL_EXSTYLE);
            if (settings.ClickThrough) ex |= Native.WS_EX_TRANSPARENT; else ex &= ~Native.WS_EX_TRANSPARENT;
            Native.SetWindowLong(Handle, Native.GWL_EXSTYLE, ex);
        }

        void ToggleClickThrough()
        {
            settings.ClickThrough = !settings.ClickThrough;
            ApplyAll();
            tray.ShowBalloonTip(1800, "stemCAT", settings.ClickThrough
                ? "Click-through ON: clicks pass through the cat. Ctrl+Alt+L to unlock."
                : "Click-through OFF: you can drag and pet the cat again.", ToolTipIcon.Info);
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.RefreshFromSettings();
        }

        void TogglePrivacy()
        {
            settings.Privacy = !settings.Privacy;
            ApplyAll();
            tray.ShowBalloonTip(1500, "stemCAT", settings.Privacy
                ? "Privacy mode ON: only left paw / right paw is shown."
                : "Privacy mode OFF.", ToolTipIcon.Info);
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.RefreshFromSettings();
        }

        bool StartupEnabled
        {
            get
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                        return k != null && (k.GetValue("stemCAT") != null || k.GetValue("DeskCat") != null);
                }
                catch { return false; }
            }
            set
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                    {
                        if (value) k.SetValue("stemCAT", "\"" + Application.ExecutablePath + "\"");
                        else k.DeleteValue("stemCAT", false);
                        k.DeleteValue("DeskCat", false);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Couldn't change the startup setting: " + ex.Message, "stemCAT");
                }
            }
        }

        Icon MakeIcon()
        {
            using (Bitmap b = scene.RenderIcon(64))
            {
                IntPtr h = b.GetHicon();
                Icon ic = (Icon)Icon.FromHandle(h).Clone();
                if (Native.IsWindows) Native.DestroyIcon(h);
                return ic;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timer.Stop();
            RemoveHooks();
            if (Native.IsWindows && IsHandleCreated)
            {
                Native.UnregisterHotKey(Handle, HK_CLICKTHROUGH);
                Native.UnregisterHotKey(Handle, HK_PRIVACY);
            }
            scene.ForgetKeys();
            tray.Visible = false;
            tray.Dispose();
            settings.Save();
            base.OnFormClosing(e);
        }
    }
}
