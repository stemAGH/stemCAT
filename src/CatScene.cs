// stemCAT - the character: state, animation and drawing (all vector, no image assets)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace stemCAT
{
    public enum CatMode { Awake, Daydream, Sleep }

    class KeyDef
    {
        public int Vk; public int Row; public float Start; public float Width; public bool Right;
    }

    enum PKind { Z, Heart, Pop, Bit, Spark }

    class Particle
    {
        public PKind Kind; public float X, Y, Vx, Vy, Age, Life, Size; public string Text;
    }

    public partial class CatScene
    {
        public const float W = 320f, H = 240f;
        const float KY = 186f, U = 10.4f, RH = 8f;

        static readonly Color DefaultOutline = Color.FromArgb(255, 59, 46, 46);
        static readonly Dictionary<int, KeyDef> Keys = BuildKeys();
        static readonly string[] BitGlyphs = { "0", "1", "0", "1", "{", "}", ";", "<", ">", "/", "#", "$", "_", "=" };

        // appearance / options
        public Coat Coat;
        public Character Char = Character.Get("cat");
        public Color KeyboardColor = ColorTranslator.FromHtml("#5E6A7D");
        public Color KeycapColor = ColorTranslator.FromHtml("#F4F1EA");
        public Color GlowColor = ColorTranslator.FromHtml("#FFC94D");
        public Color MouseColor = ColorTranslator.FromHtml("#F4F1EA");
        public bool Privacy;
        public bool GreenScreen;
        public bool FlipKeyboard = true;   // keyboard seen from the front, facing the character
        public bool KeyPops = true;        // little key-cap bubbles pop up as you type
        public float DaydreamAfter = 12f;
        public float SleepAfter = 45f;

        // inputs (key codes live only in 'held' while the key is physically down)
        public double Now;
        readonly Dictionary<int, double> held = new Dictionary<int, double>();
        readonly int[] lastVk = { -1, -1 };          // per paw: 0 = paw on the viewer's left, 1 = right
        readonly double[] lastTime = { -99, -99 };
        double lastMouseTime = -99, lastClickTime = -99, lastInput;
        bool mouseL, mouseR, mouseM;
        float wheelPhase, wheelVel;
        public PointF Cursor = new PointF(160, 20);
        public float PixelScale = 1f;
        public PointF MouseNorm;
        public bool Petting;
        public PointF PetPoint;

        // animation state
        readonly PointF[] paw = new PointF[2];
        readonly float[] squash = new float[2];
        PointF mousePos;
        float bodyDy, headDy, eyeOpen = 1f, tilt, dil = 1f, tailBlend, blush;
        PointF look;
        double blinkStart = -10, nextBlink = 2.5, shakeStart = -99, nextZ, nextHeart, nextGlitch = 4, glitchUntil = -1;
        CatMode mode = CatMode.Awake;
        readonly List<Particle> parts = new List<Particle>();
        readonly Random rnd = new Random();

        public CatMode Mode { get { return mode; } }
        public bool GlitchNow { get { return Char.Glitch && Now < glitchUntil; } }

        public CatScene(Coat coat)
        {
            Coat = coat;
            Char = Character.Get(coat.CharacterId);
            paw[0] = Rest(0); paw[1] = Rest(1);
            mousePos = new PointF(MouseBaseX, MouseBaseY);
        }

        // ---------------------------------------------------------------- keyboard layout
        static Dictionary<int, KeyDef> BuildKeys()
        {
            Dictionary<int, KeyDef> d = new Dictionary<int, KeyDef>();
            AddRow(d, 0, 6.0f, new int[] { 0xC0, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x30, 0xBD, 0xBB, 0x08 },
                new float[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2 });
            AddRow(d, 1, 6.5f, new int[] { 0x09, 0x51, 0x57, 0x45, 0x52, 0x54, 0x59, 0x55, 0x49, 0x4F, 0x50, 0xDB, 0xDD, 0xDC },
                new float[] { 1.5f, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1.5f });
            AddRow(d, 2, 6.75f, new int[] { 0x14, 0x41, 0x53, 0x44, 0x46, 0x47, 0x48, 0x4A, 0x4B, 0x4C, 0xBA, 0xDE, 0x0D },
                new float[] { 1.75f, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2.25f });
            AddRow(d, 3, 7.25f, new int[] { 0xA0, 0x5A, 0x58, 0x43, 0x56, 0x42, 0x4E, 0x4D, 0xBC, 0xBE, 0xBF, 0xA1 },
                new float[] { 2.25f, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2.75f });
            AddRow(d, 4, 9.0f, new int[] { 0xA2, 0x5B, 0xA4, 0x20, 0xA5, 0x5D, 0xA3 },
                new float[] { 1.5f, 1.25f, 1.25f, 6.5f, 1.25f, 1.25f, 2f });
            return d;
        }

        static void AddRow(Dictionary<int, KeyDef> d, int row, float split, int[] vks, float[] widths)
        {
            float x = 0;
            for (int i = 0; i < vks.Length; i++)
            {
                KeyDef k = new KeyDef();
                k.Vk = vks[i]; k.Row = row; k.Start = x; k.Width = widths[i];
                k.Right = (x + widths[i] / 2f) > split;
                d[k.Vk] = k;
                x += widths[i];
            }
        }

        public static int NormalizeVk(int vk)
        {
            if (vk == 0x10) return 0xA0;
            if (vk == 0x11) return 0xA2;
            if (vk == 0x12) return 0xA4;
            return vk;
        }

        // which of the user's hands normally types this key
        static bool UserRightHand(int vk)
        {
            KeyDef k;
            if (Keys.TryGetValue(vk, out k)) return k.Right;
            if (vk >= 0x70 && vk <= 0x75) return false;
            if (vk == 0x1B) return false;
            return true;
        }

        // which paw presses it: 0 = paw on the viewer's left, 1 = right.
        // When the keyboard faces the character, its left hand is on the viewer's right.
        int SideOf(int vk)
        {
            bool r = UserRightHand(vk);
            if (FlipKeyboard) r = !r;
            return r ? 1 : 0;
        }

        int MouseSide { get { return FlipKeyboard ? 0 : 1; } }
        float KX { get { return FlipKeyboard ? 94f : 70f; } }
        float MouseBaseX { get { return FlipKeyboard ? 58f : 262f; } }
        const float MouseBaseY = 204f;

        RectangleF KeyRect(KeyDef k)
        {
            float start = FlipKeyboard ? 15f - k.Start - k.Width : k.Start;
            int row = FlipKeyboard ? 4 - k.Row : k.Row;
            return new RectangleF(KX + start * U, KY + row * RH, k.Width * U, RH);
        }

        public static string KeyLabel(int vk)
        {
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
            if (vk >= 0x60 && vk <= 0x69) return ((char)('0' + vk - 0x60)).ToString();
            if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);
            switch (vk)
            {
                case 0xBA: return ";"; case 0xBB: return "="; case 0xBC: return ","; case 0xBD: return "-";
                case 0xBE: return "."; case 0xBF: return "/"; case 0xC0: return "`"; case 0xDB: return "[";
                case 0xDC: return "\\"; case 0xDD: return "]"; case 0xDE: return "'";
                case 0x08: return "Bksp"; case 0x09: return "Tab"; case 0x0D: return "Enter"; case 0x1B: return "Esc";
                case 0x20: return "Space"; case 0x14: return "Caps";
                case 0xA0: case 0xA1: return "Shift";
                case 0xA2: case 0xA3: return "Ctrl";
                case 0xA4: case 0xA5: return "Alt";
                case 0x5B: case 0x5C: return "Win";
                case 0x5D: return "Menu"; case 0x2E: return "Del"; case 0x2D: return "Ins";
                case 0x24: return "Home"; case 0x23: return "End"; case 0x21: return "PgUp"; case 0x22: return "PgDn";
                case 0x25: return "\u2190"; case 0x26: return "\u2191"; case 0x27: return "\u2192"; case 0x28: return "\u2193";
                case 0x2C: return "PrtSc"; case 0x6A: return "*"; case 0x6B: return "+"; case 0x6D: return "-";
                case 0x6E: return "."; case 0x6F: return "/"; case 0x90: return "Num";
            }
            return null;
        }

        // ---------------------------------------------------------------- input
        public void KeyDown(int vk)
        {
            vk = NormalizeVk(vk);
            if (held.ContainsKey(vk)) return;   // auto-repeat
            held[vk] = Now;
            int s = SideOf(vk);
            lastVk[s] = vk; lastTime[s] = Now;
            if (mode == CatMode.Sleep) shakeStart = Now;
            lastInput = Now;

            PointF kc = KeyCenter(vk, s);
            if (KeyPops && !Privacy)
            {
                string label = KeyLabel(vk);
                if (label != null)
                {
                    int pops = 0;
                    foreach (Particle q in parts) if (q.Kind == PKind.Pop) pops++;
                    if (pops < 8)
                    {
                        Particle p = new Particle();
                        p.Kind = PKind.Pop; p.Text = label;
                        p.X = Math.Max(24f, Math.Min(W - 24f, kc.X)); p.Y = KY - 22f;
                        p.Vx = 0; p.Vy = -26f; p.Life = 0.85f;
                        parts.Add(p);
                    }
                }
            }
            if (Char.Fx == TypeFx.Bits)
            {
                for (int i = 0; i < 2; i++)
                {
                    Particle b = new Particle();
                    b.Kind = PKind.Bit; b.Text = BitGlyphs[rnd.Next(BitGlyphs.Length)];
                    b.X = kc.X + (float)(rnd.NextDouble() * 16 - 8); b.Y = kc.Y - 10;
                    b.Vx = (float)(rnd.NextDouble() * 16 - 8); b.Vy = -30f - (float)rnd.NextDouble() * 20f;
                    b.Life = 0.9f + (float)rnd.NextDouble() * 0.4f; b.Size = 7f + (float)rnd.NextDouble() * 3f;
                    parts.Add(b);
                }
            }
            else if (Char.Fx == TypeFx.Sparks)
            {
                for (int i = 0; i < 5; i++)
                {
                    double a = rnd.NextDouble() * Math.PI * 2;
                    Particle b = new Particle();
                    b.Kind = PKind.Spark; b.X = kc.X; b.Y = kc.Y - 6;
                    b.Vx = (float)Math.Cos(a) * 55f; b.Vy = (float)Math.Sin(a) * 40f - 20f;
                    b.Life = 0.35f; b.Size = 2.2f;
                    parts.Add(b);
                }
            }
            while (parts.Count > 120) parts.RemoveAt(0);
        }

        public void KeyUp(int vk) { held.Remove(NormalizeVk(vk)); }
        public List<int> HeldKeys() { return new List<int>(held.Keys); }
        public void ForgetKeys() { held.Clear(); }
        public void MouseMoved() { lastMouseTime = Now; lastInput = Now; }

        public void MouseButton(int button, bool down)
        {
            if (button == 0) mouseL = down; else if (button == 1) mouseR = down; else mouseM = down;
            lastMouseTime = Now; lastInput = Now;
            if (down) lastClickTime = Now;
        }

        public void Wheel(int delta)
        {
            wheelVel += (delta > 0 ? -1 : 1) * 40f;
            lastMouseTime = Now; lastInput = Now;
        }

        public void Poke() { lastInput = Now; }
        public void SetIdleFor(double seconds) { lastInput = Now - seconds; }

        public bool HitHead(PointF p)
        {
            if (DarkCharacter)
            {
                double a = -(tilt + ShakeAngle()) * Math.PI / 180.0;
                float px = p.X - 160, py = p.Y - 140 - headDy;
                float x = (float)(px * Math.Cos(a) - py * Math.Sin(a));
                float y = (float)(px * Math.Sin(a) + py * Math.Cos(a)) + 140;
                return x * x / (48f * 48f) + (y - 57) * (y - 57) / (52f * 52f) <= 1;
            }
            float dx = (p.X - 160f) / 68f, dy = (p.Y - (88f + headDy)) / 56f;
            return dx * dx + dy * dy <= 1f;
        }

        // ---------------------------------------------------------------- positions
        static PointF P(float x, float y) { return new PointF(x, y); }
        static PointF Off(PointF p, float dx, float dy) { return new PointF(p.X + dx, p.Y + dy); }

        PointF Rest(int side)
        {
            // hover above the home row: F for the user's left hand, J for the right
            bool userRight = (side == 1) != FlipKeyboard;
            RectangleF r = KeyRect(Keys[userRight ? 0x4A : 0x46]);
            return P(r.X + r.Width / 2 + (side == 1 ? 4f : -2f), KY + 1.0f * RH);
        }

        PointF DeskRest(int side) { return P(side == 0 ? 130f : 190f, 172f + bodyDy * 0.5f); }
        PointF Shoulder(int side) { return P(side == 0 ? 113f : 207f, 152f + bodyDy); }

        PointF KeyCenter(int vk, int side)
        {
            if (Privacy)
                return side == 1 ? P(KX + 11.25f * U, KY + 2.5f * RH) : P(KX + 3.75f * U, KY + 2.5f * RH);
            KeyDef k;
            if (Keys.TryGetValue(vk, out k))
            {
                RectangleF r = KeyRect(k);
                return P(r.X + r.Width / 2f, r.Y + r.Height / 2f);
            }
            return side == 1 ? P(KX + 14.3f * U, KY + 0.5f * RH) : P(KX + 0.7f * U, KY + 0.5f * RH);
        }

        bool NewestHeld(int side, out int vk)
        {
            vk = -1; double best = -1e9;
            foreach (KeyValuePair<int, double> kv in held)
                if (SideOf(kv.Key) == side && kv.Value > best) { best = kv.Value; vk = kv.Key; }
            return vk >= 0;
        }

        bool SideActive(int side)
        {
            int dummy;
            return NewestHeld(side, out dummy) || Now - lastTime[side] < 0.09;
        }

        static float Approach(float cur, float target, float speed, float dt)
        {
            return cur + (target - cur) * (1f - (float)Math.Exp(-speed * dt));
        }

        static PointF Approach(PointF cur, PointF target, float speed, float dt)
        {
            return P(Approach(cur.X, target.X, speed, dt), Approach(cur.Y, target.Y, speed, dt));
        }

        // ---------------------------------------------------------------- update
        public void Update(double now)
        {
            float dt = (float)Math.Max(0, Math.Min(0.1, now - Now));
            Now = now;

            double idle = Now - lastInput;
            CatMode target = Petting ? CatMode.Awake
                : idle > SleepAfter ? CatMode.Sleep
                : idle > DaydreamAfter ? CatMode.Daydream : CatMode.Awake;
            if (target != mode)
            {
                if (target == CatMode.Sleep) nextZ = Now + 0.6;
                mode = target;
            }
            bool awake = mode == CatMode.Awake;

            PointF mt = P(MouseBaseX + MouseNorm.X * 6f, MouseBaseY + MouseNorm.Y * 4f);
            mousePos = Approach(mousePos, mt, 18f, dt);
            wheelPhase += wheelVel * dt;
            wheelVel *= (float)Math.Exp(-5 * dt);

            for (int s = 0; s < 2; s++)
            {
                int vk;
                bool pressing = NewestHeld(s, out vk);
                int kv = pressing ? vk : lastVk[s];
                if (!pressing && Now - lastTime[s] < 0.09) pressing = true;
                bool onMouse = s == MouseSide && !pressing && lastMouseTime >= lastTime[s] && Now - lastMouseTime < 8;
                PointF t; float sq = 0;
                if (!awake) t = DeskRest(s);
                else if (pressing) { t = Off(KeyCenter(kv, s), 0, -10); sq = 1; }
                else if (onMouse)
                {
                    t = Off(mousePos, s == 1 ? 3 : -3, -3);
                    if (mouseL || mouseR || mouseM || Now - lastClickTime < 0.08) sq = 1;
                }
                else if (Now - lastTime[s] < 0.6) t = Off(KeyCenter(lastVk[s], s), 0, -12);
                else t = Rest(s);
                float speed = !awake ? 6f : onMouse ? 60f : 38f;
                paw[s] = Approach(paw[s], t, speed, dt);
                squash[s] = Approach(squash[s], sq, 45f, dt);
            }

            float breathe = (float)Math.Sin(Now * (mode == CatMode.Sleep ? 1.7 : 2.3));
            float bob = Char.Float ? (float)Math.Sin(Now * 2.2) * 3f : 0f;
            float bTarget = (mode == CatMode.Daydream ? -5f : mode == CatMode.Sleep ? 5f + breathe * 1.3f : breathe * 0.6f) + bob;
            float hTarget = (mode == CatMode.Daydream ? -7f : mode == CatMode.Sleep ? 9f + breathe : (Petting ? -3f : 0f)) + bob;
            bodyDy = Approach(bodyDy, bTarget, 5f, dt);
            headDy = Approach(headDy, hTarget, 6f, dt);
            tailBlend = Approach(tailBlend, mode == CatMode.Sleep ? 1f : 0f, 2.5f, dt);
            blush = Approach(blush, Petting ? 1f : 0f, 8f, dt);

            float openTarget = 1f;
            if (mode != CatMode.Sleep)
            {
                if (Now > nextBlink)
                {
                    blinkStart = Now;
                    nextBlink = Now + (mode == CatMode.Daydream ? 4.0 : 2.2) + rnd.NextDouble() * 4.0;
                }
                double blinkLen = mode == CatMode.Daydream ? 0.45 : 0.16;
                double bt = (Now - blinkStart) / blinkLen;
                if (bt >= 0 && bt < 1) openTarget = (float)Math.Abs(1 - 2 * bt);
                if (mode == CatMode.Daydream) openTarget = Math.Min(openTarget, 0.82f);
            }
            eyeOpen = (mode == CatMode.Sleep) ? Approach(eyeOpen, 0f, 4f, dt) : openTarget;

            PointF lk; float dl;
            if (mode == CatMode.Daydream) { lk = P(0, -1.8f); dl = 1.35f; }
            else if (mode == CatMode.Sleep) { lk = P(0, 0); dl = 1f; }
            else
            {
                float vx = Cursor.X - 160f, vy = Cursor.Y - (EyeY + headDy);
                float d = (float)Math.Sqrt(vx * vx + vy * vy) + 0.001f;
                float k = Math.Min(d / 70f, 1f);
                lk = P(vx / d * k * 4.8f, vy / d * k * 3.6f);
                dl = (d * PixelScale < 140f) ? 1.65f : 1.0f;
            }
            look = Approach(look, lk, 14f, dt);
            dil = Approach(dil, dl, 7f, dt);

            float tt = 0;
            if (Petting)
            {
                float side = Math.Max(-1f, Math.Min(1f, (PetPoint.X - 160f) / 45f));
                tt = side * 9f + (float)Math.Sin(Now * 7) * 2.5f;
            }
            tilt = Approach(tilt, tt, 10f, dt);

            if (Char.Glitch && Now > nextGlitch)
            {
                glitchUntil = Now + 0.12 + rnd.NextDouble() * 0.1;
                nextGlitch = Now + 3 + rnd.NextDouble() * 7;
            }

            if (mode == CatMode.Sleep && Now > nextZ)
            {
                Particle z = new Particle();
                z.Kind = PKind.Z; z.X = 214f + (float)rnd.NextDouble() * 6f; z.Y = 52f + headDy; z.Vx = 9f; z.Vy = -13f; z.Life = 2.8f;
                parts.Add(z); nextZ = Now + 1.15;
            }
            if (Petting && Now > nextHeart)
            {
                Particle h = new Particle();
                h.Kind = DarkCharacter ? PKind.Spark : PKind.Heart; h.X = PetPoint.X + (float)(rnd.NextDouble() * 24 - 12); h.Y = 34f + headDy;
                h.Vx = (float)(rnd.NextDouble() * 12 - 6); h.Vy = -24f; h.Life = 1.4f; h.Size = 9f + (float)rnd.NextDouble() * 4f;
                parts.Add(h); nextHeart = Now + 0.42;
            }
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                Particle p = parts[i];
                p.Age += dt; p.X += p.Vx * dt; p.Y += p.Vy * dt;
                if (p.Kind == PKind.Spark) p.Vy += 160f * dt;
                if (p.Age >= p.Life) parts.RemoveAt(i);
            }
        }

        float ShakeAngle()
        {
            double t = Now - shakeStart;
            if (t < 0 || t > 0.75) return 0;
            return (float)(Math.Sin(t * 34) * 11 * (1 - t / 0.75));
        }

        // ---------------------------------------------------------------- drawing helpers
        static GraphicsPath Ellipse(float x, float y, float w, float h)
        {
            GraphicsPath p = new GraphicsPath(); p.AddEllipse(x, y, w, h); return p;
        }

        static GraphicsPath RoundRect(float x, float y, float w, float h, float r)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(r * 2, Math.Min(w, h));
            if (d <= 0.01f) { p.AddRectangle(new RectangleF(x, y, w, h)); return p; }
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static GraphicsPath Poly(params float[] xy)
        {
            PointF[] pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(xy[i * 2], xy[i * 2 + 1]);
            GraphicsPath p = new GraphicsPath(); p.AddPolygon(pts); return p;
        }

        static Pen RoundPen(Color c, float w)
        {
            Pen p = new Pen(c, w);
            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
            return p;
        }

        static Color A(Color c, int a) { return Color.FromArgb(Math.Max(0, Math.Min(255, a)), c.R, c.G, c.B); }

        Color OL { get { return Char.Neon ? Coat.Points : DefaultOutline; } }

        Color FeatureColor
        {
            get
            {
                if (Char.Neon) return Coat.Points;
                return Coat.Luma(Coat.Fur) < 0.3f ? Coat.Shade(Coat.Fur, 0.5f) : DefaultOutline;
            }
        }

        Color PawColor
        {
            get
            {
                if (Char.PawsAccent || Coat.UsePoints) return Coat.Points;
                if (Coat.WhitePaws) return Coat.Belly;
                return Coat.Fur;
            }
        }

        Color ArmColor { get { return Char.ArmsAccent ? Coat.Points : Coat.Fur; } }

        // A consistent upper-left studio light gives the vector parts sculpted volume.
        void Sculpt(Graphics g, Color color, GraphicsPath shape)
        {
            RectangleF r = shape.GetBounds();
            using (LinearGradientBrush baseLight = new LinearGradientBrush(r,
                Coat.Shade(color, 0.25f), Coat.Shade(color, -0.38f), 55f))
                g.FillPath(baseLight, shape);
            using (PathGradientBrush light = new PathGradientBrush(shape))
            {
                light.CenterPoint = P(r.X + r.Width * 0.36f, r.Y + r.Height * 0.30f);
                light.CenterColor = Coat.Shade(color, 0.38f);
                light.SurroundColors = new Color[] { A(color, 0) };
                light.FocusScales = P(0.20f, 0.18f);
                g.FillPath(light, shape);
            }
        }

        void SoftShadow(Graphics g, float x, float y, float w, float h, int alpha)
        {
            using (GraphicsPath path = Ellipse(x, y, w, h))
            using (PathGradientBrush shadow = new PathGradientBrush(path))
            {
                shadow.CenterColor = Color.FromArgb(alpha, 30, 24, 42);
                shadow.SurroundColors = new Color[] { Color.FromArgb(0, 30, 24, 42) };
                g.FillPath(shadow, path);
            }
        }

        void FillOutlined(Graphics g, Color fill, float ow, params GraphicsPath[] shapes)
        {
            if (Char.Neon)
                using (Pen gp = RoundPen(A(OL, 60), ow + 6f))
                    foreach (GraphicsPath s in shapes) g.DrawPath(gp, s);
            foreach (GraphicsPath shape in shapes)
            {
                using (Pen edge = RoundPen(Char.Neon ? A(OL, 210) : Coat.Shade(fill, -0.48f), 1.5f))
                    g.DrawPath(edge, shape);
                Sculpt(g, fill, shape);
            }
        }

        void StrokeOutlined(Graphics g, Color fill, float w, PointF[] pts)
        {
            if (Char.Neon) using (Pen gp = RoundPen(A(OL, 60), w + 11f)) g.DrawLines(gp, pts);
            using (Pen edge = RoundPen(Coat.Shade(fill, -0.40f), w + 1f)) g.DrawLines(edge, pts);
            // Shade across the limb; the same brush follows moving paws without a hard highlight stripe.
            PointF mid = pts[pts.Length / 2];
            float dx = pts[pts.Length - 1].X - pts[0].X;
            float dy = pts[pts.Length - 1].Y - pts[0].Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            float nx = len > 0.01f ? -dy / len : 1f, ny = len > 0.01f ? dx / len : 0f;
            if (nx + ny > 0) { nx = -nx; ny = -ny; }
            using (LinearGradientBrush light = new LinearGradientBrush(
                P(mid.X + nx * w / 2, mid.Y + ny * w / 2),
                P(mid.X - nx * w / 2, mid.Y - ny * w / 2), fill, fill))
            {
                ColorBlend blend = new ColorBlend();
                blend.Positions = new float[] { 0, 0.28f, 0.60f, 1 };
                blend.Colors = new Color[] { Coat.Shade(fill, -0.06f), Coat.Shade(fill, 0.32f), fill, Coat.Shade(fill, -0.38f) };
                light.InterpolationColors = blend;
                light.WrapMode = WrapMode.TileFlipXY;
                using (Pen surface = new Pen(light, w))
                {
                    surface.StartCap = surface.EndCap = LineCap.Round;
                    surface.LineJoin = LineJoin.Round;
                    g.DrawLines(surface, pts);
                }
            }
        }

        static PointF Bez(PointF a, PointF b, PointF c, PointF d, float t)
        {
            float u = 1 - t;
            return P(u * u * u * a.X + 3 * u * u * t * b.X + 3 * u * t * t * c.X + t * t * t * d.X,
                     u * u * u * a.Y + 3 * u * u * t * b.Y + 3 * u * t * t * c.Y + t * t * t * d.Y);
        }

        static PointF Lerp(PointF a, PointF b, float t) { return P(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t); }

        void Glow(Graphics g, Color c, float cx, float cy, float r, int alpha)
        {
            using (GraphicsPath gp = Ellipse(cx - r, cy - r, r * 2, r * 2))
            using (PathGradientBrush pb = new PathGradientBrush(gp))
            {
                pb.CenterColor = A(c, alpha);
                pb.SurroundColors = new Color[] { A(c, 0) };
                g.FillPath(pb, gp);
            }
        }

        // ---------------------------------------------------------------- drawing
        public void Draw(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            if (GreenScreen)
                using (SolidBrush gb = new SolidBrush(Color.FromArgb(255, 0, 255, 0)))
                    g.FillRectangle(gb, -2, -2, W + 4, H + 4);

            if (DarkCharacter) { DrawDarkCharacter(g); return; }

            bool tailOnDesk = tailBlend > 0.5f && Char.Tail != TailStyle.Puff;
            if (!tailOnDesk) DrawTail(g);
            DrawBody(g);
            DrawHead(g, true);
            DrawDesk(g);
            DrawKeyboard(g);
            DrawMouse(g);
            if (tailOnDesk) DrawTail(g);
            DrawArms(g);
            DrawParticles(g);
        }

        // ---------------------------------------------------------------- tail
        void DrawTail(Graphics g)
        {
            if (Char.Tail == TailStyle.None) return;
            float bd = bodyDy;
            if (Char.Tail == TailStyle.Puff)
            {
                float wig = (float)Math.Sin(Now * 3) * 1.5f;
                using (GraphicsPath puff = Ellipse(219 + wig, 150 + bd, 28, 26)) FillOutlined(g, Coat.Fur, 5f, puff);
                return;
            }
            float sway = (float)Math.Sin(Now * (mode == CatMode.Daydream ? 0.9 : 1.7)) * (mode == CatMode.Daydream ? 6f : 11f);
            PointF[] up = { P(221, 166 + bd), P(262, 168 + bd), P(276 + sway * 0.3f, 126 + bd), P(258 + sway, 96 + bd) };
            PointF[] down = { P(222, 167), P(248, 177), P(282, 178), P(298, 164) };
            if (Char.Tail == TailStyle.Bushy)
            {
                up = new PointF[] { P(221, 168 + bd), P(268, 172 + bd), P(286 + sway * 0.3f, 130 + bd), P(266 + sway, 100 + bd) };
            }
            PointF[] c = new PointF[4];
            for (int i = 0; i < 4; i++) c[i] = Lerp(up[i], down[i], Math.Min(1f, tailBlend));
            int n = 24;
            PointF[] pts = new PointF[n + 1];
            for (int i = 0; i <= n; i++) pts[i] = Bez(c[0], c[1], c[2], c[3], i / (float)n);

            switch (Char.Tail)
            {
                case TailStyle.Cable:
                    {
                        using (Pen op = RoundPen(OL, 8f)) g.DrawLines(op, pts);
                        using (Pen fp = RoundPen(Color.FromArgb(255, 58, 63, 71), 4.5f)) g.DrawLines(fp, pts);
                        PointF e = pts[n];
                        using (GraphicsPath plug = RoundRect(e.X - 7, e.Y - 6, 14, 11, 3)) FillOutlined(g, Coat.Stripe, 2.5f, plug);
                        using (Pen pr = RoundPen(Coat.Shade(Coat.Fur, 0.3f), 2.2f))
                        {
                            g.DrawLine(pr, e.X - 3, e.Y - 6, e.X - 3, e.Y - 11);
                            g.DrawLine(pr, e.X + 3, e.Y - 6, e.X + 3, e.Y - 11);
                        }
                        return;
                    }
                case TailStyle.Wisp:
                    {
                        Color fur = Coat.Fur;
                        for (int pass = 0; pass < 2; pass++)
                            for (int i = 0; i < n; i++)
                            {
                                float w = 20f * (1f - i / (float)n * 0.85f);
                                using (Pen pn = RoundPen(pass == 0 ? OL : fur, pass == 0 ? w + 5f : w))
                                    g.DrawLine(pn, pts[i], pts[i + 1]);
                            }
                        return;
                    }
            }

            float width = Char.Tail == TailStyle.Bushy ? 26f : 16f;
            StrokeOutlined(g, Coat.Fur, width, pts);
            if (Char.Tail == TailStyle.Bushy || Coat.UsePoints || Coat.UsePatches)
            {
                int from = (int)(n * (Char.Tail == TailStyle.Bushy ? 0.68f : 0.55f));
                PointF[] tip = new PointF[n - from + 1];
                Array.Copy(pts, from, tip, 0, tip.Length);
                Color tc = Char.Tail == TailStyle.Bushy ? Coat.Belly : Coat.UsePoints ? Coat.Points : Coat.Patch2;
                using (Pen tp = RoundPen(tc, width)) g.DrawLines(tp, tip);
            }
            if (Coat.UseStripes && Char.Tail == TailStyle.Cat)
            {
                using (Pen sp = RoundPen(Coat.Stripe, 3.2f))
                    foreach (float t in new float[] { 0.42f, 0.6f, 0.78f })
                    {
                        PointF a = Bez(c[0], c[1], c[2], c[3], t);
                        PointF b = Bez(c[0], c[1], c[2], c[3], t + 0.02f);
                        float dx = b.X - a.X, dy = b.Y - a.Y, l = (float)Math.Sqrt(dx * dx + dy * dy) + 0.001f;
                        float nx = -dy / l * 6.5f, ny = dx / l * 6.5f;
                        g.DrawLine(sp, a.X - nx, a.Y - ny, a.X + nx, a.Y + ny);
                    }
            }
        }

        // ---------------------------------------------------------------- body
        void DrawBody(Graphics g)
        {
            float bd = bodyDy;
            using (GraphicsPath body = Ellipse(86, 106 + bd, 148, 126))
            {
                FillOutlined(g, Coat.Fur, 5f, body);
                GraphicsState st = g.Save();
                g.SetClip(body, CombineMode.Intersect);
                switch (Char.Body)
                {
                    case BodyStyle.Fur:
                        if (Coat.UsePatches)
                        {
                            using (SolidBrush b1 = new SolidBrush(Coat.Patch1)) g.FillEllipse(b1, 80, 132 + bd, 52, 46);
                            using (SolidBrush b2 = new SolidBrush(Coat.Patch2)) g.FillEllipse(b2, 182, 110 + bd, 56, 44);
                        }
                        if (Coat.UseStripes)
                            using (Pen sp = RoundPen(A(Coat.Stripe, 180), 3.2f))
                            {
                                g.DrawLine(sp, 86, 132 + bd, 102, 136 + bd); g.DrawLine(sp, 84, 145 + bd, 100, 147 + bd);
                                g.DrawLine(sp, 234, 132 + bd, 218, 136 + bd); g.DrawLine(sp, 236, 145 + bd, 220, 147 + bd);
                            }
                        if (Char.ArmsAccent)
                            using (SolidBrush sb = new SolidBrush(Coat.Points)) g.FillEllipse(sb, 70, 112 + bd, 180, 44);
                        using (GraphicsPath belly = Ellipse(124, 124 + bd + (Char.ArmsAccent ? 14 : 0), 72, 66))
                            Sculpt(g, Coat.Belly, belly);
                        break;
                    case BodyStyle.Hoodie:
                        using (SolidBrush pk = new SolidBrush(Coat.Shade(Coat.Fur, -0.18f)))
                        using (GraphicsPath pocket = RoundRect(124, 152 + bd, 72, 30, 10))
                            g.FillPath(pk, pocket);
                        using (Pen pl = RoundPen(Coat.Shade(Coat.Fur, 0.12f), 1.6f)) g.DrawArc(pl, 124, 152 + bd, 72, 14, 200, 140);
                        using (Font f = new Font(FontFamily.GenericMonospace, 8f, FontStyle.Bold, GraphicsUnit.Pixel))
                        using (SolidBrush eb = new SolidBrush(A(Coat.Eye, 220)))
                            g.DrawString("</>", f, eb, 101, 128 + bd);
                        break;
                    case BodyStyle.Robot:
                        using (GraphicsPath panel = RoundRect(132, 126 + bd, 56, 30, 6))
                        {
                            using (SolidBrush pb = new SolidBrush(Coat.Belly)) g.FillPath(pb, panel);
                            using (Pen pp = new Pen(Coat.Stripe, 2f)) g.DrawPath(pp, panel);
                        }
                        Color[] leds = { Coat.Points, Coat.Eye, ColorTranslator.FromHtml("#FFD24D") };
                        for (int i = 0; i < 3; i++)
                        {
                            bool on = Math.Sin(Now * 3 + i * 2.1) > -0.2;
                            float lx = 146 + i * 14, ly = 141 + bd;
                            if (on) Glow(g, leds[i], lx, ly, 8, 120);
                            using (SolidBrush lb = new SolidBrush(on ? leds[i] : A(leds[i], 70))) g.FillEllipse(lb, lx - 3.5f, ly - 3.5f, 7, 7);
                        }
                        using (SolidBrush rv = new SolidBrush(Coat.Stripe))
                        {
                            g.FillEllipse(rv, 104, 128 + bd, 5, 5); g.FillEllipse(rv, 211, 128 + bd, 5, 5);
                        }
                        break;
                }
                SoftShadow(g, 99, 112 + headDy, 126, 46, 95);
                g.Restore(st);
            }
        }

        // ---------------------------------------------------------------- head
        float EyeY
        {
            get
            {
                switch (Char.Head)
                {
                    case HeadStyle.Alien: return 84f;
                    case HeadStyle.Robot: return 84f;
                    case HeadStyle.Hood: return 94f;
                    default: return 81f;
                }
            }
        }

        float EyeDX { get { return Char.Head == HeadStyle.Hood ? 19f : Char.Head == HeadStyle.Robot ? 24f : 26f; } }

        public void DrawHead(Graphics g, bool full)
        {
            GraphicsState st = g.Save();
            float hd = full ? headDy : 0f;
            float ang = full ? tilt + ShakeAngle() : 0f;
            g.TranslateTransform(160, 140 + hd);
            g.RotateTransform(ang);
            g.TranslateTransform(-160, -140);

            if (DarkCharacter)
            {
                DrawDarkHead(g, full);
                g.Restore(st);
                return;
            }
            DrawEars(g);
            switch (Char.Head)
            {
                case HeadStyle.Alien: DrawAlienHead(g); break;
                case HeadStyle.Robot: DrawRobotHead(g); break;
                case HeadStyle.Hood: DrawHoodHead(g); break;
                case HeadStyle.Ghost: DrawSimpleHead(g, false); break;
                default: DrawSimpleHead(g, true); break;
            }

            float ey = EyeY, dx = EyeDX;
            DrawEye(g, 160 - dx, ey, full, -1);
            DrawEye(g, 160 + dx, ey, full, 1);
            DrawFace(g);
            if (Char.Head == HeadStyle.Hood) DrawDrawstrings(g);
            g.Restore(st);
        }

        void DrawEars(Graphics g)
        {
            switch (Char.Ears)
            {
                case EarStyle.Cat:
                case EarStyle.Fox:
                    {
                        bool fox = Char.Ears == EarStyle.Fox;
                        GraphicsPath earL = fox ? Poly(98, 70, 98, 2, 152, 42) : Poly(100, 66, 106, 10, 150, 42);
                        GraphicsPath earR = fox ? Poly(222, 70, 222, 2, 168, 42) : Poly(220, 66, 214, 10, 170, 42);
                        Color earLc = Coat.UsePoints ? Coat.Points : Coat.UsePatches ? Coat.Patch1 : Coat.Fur;
                        Color earRc = Coat.UsePoints ? Coat.Points : Coat.UsePatches ? Coat.Patch2 : Coat.Fur;
                        if (Char.Neon) using (Pen gp = RoundPen(A(OL, 60), 11f)) { g.DrawPath(gp, earL); g.DrawPath(gp, earR); }
                        FillOutlined(g, earLc, 5f, earL);
                        FillOutlined(g, earRc, 5f, earR);
                        using (Pen ip = RoundPen(Coat.EarInner, 3f))
                        using (SolidBrush ib = new SolidBrush(Coat.EarInner))
                        {
                            GraphicsPath inL = fox ? Poly(108, 58, 105, 16, 138, 44) : Poly(111, 55, 112, 25, 137, 44);
                            GraphicsPath inR = fox ? Poly(212, 58, 215, 16, 182, 44) : Poly(209, 55, 208, 25, 183, 44);
                            Sculpt(g, Coat.EarInner, inL); Sculpt(g, Coat.EarInner, inR);
                            inL.Dispose(); inR.Dispose();
                        }
                        if (fox)
                            using (SolidBrush tb = new SolidBrush(Coat.Points))
                            {
                                GraphicsState s2 = g.Save(); g.SetClip(earL, CombineMode.Intersect); g.FillRectangle(tb, 80, -10, 40, 26); g.Restore(s2);
                                s2 = g.Save(); g.SetClip(earR, CombineMode.Intersect); g.FillRectangle(tb, 200, -10, 40, 26); g.Restore(s2);
                            }
                        earL.Dispose(); earR.Dispose();
                        break;
                    }
                case EarStyle.Bunny:
                    {
                        float wig = (float)Math.Sin(Now * 2.4) * 4f;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            GraphicsState s2 = g.Save();
                            g.TranslateTransform(160 + side * 30, 52);
                            g.RotateTransform(side * (12 + wig * (side < 0 ? 1 : -0.6f)));
                            using (GraphicsPath ear = Ellipse(-13, -52, 26, 60)) FillOutlined(g, Coat.Fur, 5f, ear);
                            using (SolidBrush ib = new SolidBrush(Coat.EarInner)) g.FillEllipse(ib, -6.5f, -44, 13, 44);
                            g.Restore(s2);
                        }
                        break;
                    }
                case EarStyle.Round:
                    using (GraphicsPath l = Ellipse(96, 28, 38, 38))
                    using (GraphicsPath r = Ellipse(186, 28, 38, 38))
                        FillOutlined(g, Coat.Points, 5f, l, r);
                    using (SolidBrush ib = new SolidBrush(Coat.Shade(Coat.Points, 0.18f)))
                    {
                        g.FillEllipse(ib, 105, 37, 20, 20); g.FillEllipse(ib, 195, 37, 20, 20);
                    }
                    break;
                case EarStyle.Antennae:
                    {
                        float w1 = (float)Math.Sin(Now * 2.6) * 3f, w2 = (float)Math.Sin(Now * 2.6 + 1.3) * 3f;
                        PointF[] a1 = { P(142, 34), P(132, 18), P(122 + w1, 4) };
                        PointF[] a2 = { P(178, 34), P(188, 18), P(198 + w2, 4) };
                        StrokeOutlined(g, Coat.Fur, 3.5f, a1);
                        StrokeOutlined(g, Coat.Fur, 3.5f, a2);
                        float pulse = 0.65f + 0.35f * (float)Math.Sin(Now * 4);
                        foreach (PointF tip in new PointF[] { a1[2], a2[2] })
                        {
                            Glow(g, Coat.Points, tip.X, tip.Y, 13, (int)(150 * pulse));
                            using (GraphicsPath bulb = Ellipse(tip.X - 6, tip.Y - 6, 12, 12)) FillOutlined(g, Coat.Points, 3f, bulb);
                            using (SolidBrush hb = new SolidBrush(A(Color.White, 200))) g.FillEllipse(hb, tip.X - 3.5f, tip.Y - 3.5f, 4, 4);
                        }
                        break;
                    }
                case EarStyle.Robot:
                    {
                        using (Pen op = RoundPen(OL, 6f)) g.DrawLine(op, 160, 36, 160, 12);
                        using (Pen fp = RoundPen(Coat.Stripe, 3f)) g.DrawLine(fp, 160, 36, 160, 12);
                        bool on = Math.Sin(Now * 3.5) > 0;
                        if (on) Glow(g, Coat.Points, 160, 8, 14, 170);
                        using (GraphicsPath ball = Ellipse(153.5f, 1.5f, 13, 13)) FillOutlined(g, on ? Coat.Points : Coat.Shade(Coat.Points, -0.45f), 3f, ball);
                        using (GraphicsPath l = RoundRect(84, 68, 16, 38, 6))
                        using (GraphicsPath r = RoundRect(220, 68, 16, 38, 6))
                            FillOutlined(g, Coat.Stripe, 4f, l, r);
                        break;
                    }
            }
        }

        void DrawSimpleHead(Graphics g, bool cheeks)
        {
            GraphicsPath face = new GraphicsPath();
            if (cheeks)
            {
                face.AddBezier(160, 34, 116, 32, 94, 54, 94, 88);
                face.AddBezier(94, 88, 72, 119, 109, 140, 160, 142);
                face.AddBezier(160, 142, 211, 140, 248, 119, 226, 88);
                face.AddBezier(226, 88, 226, 54, 204, 32, 160, 34);
                face.CloseFigure();
            }
            else face.AddEllipse(94, 34, 132, 108);
            FillOutlined(g, Coat.Fur, 5f, face);

            GraphicsState fs = g.Save();
            g.SetClip(face, CombineMode.Intersect);
            if (Coat.UsePatches)
            {
                using (SolidBrush b1 = new SolidBrush(Coat.Patch1)) g.FillEllipse(b1, 84, 26, 66, 56);
                using (SolidBrush b2 = new SolidBrush(Coat.Patch2)) g.FillEllipse(b2, 180, 50, 60, 50);
            }
            if (Coat.UseStripes)
                using (Pen sp = RoundPen(A(Coat.Stripe, 180), 3.2f))
                {
                    if (Char.Neon) using (Pen gsp = RoundPen(A(Coat.Stripe, 70), 9f)) DrawStripes(g, gsp);
                    DrawStripes(g, sp);
                }
            if (Char.Ears == EarStyle.Fox)
                using (SolidBrush wb = new SolidBrush(Coat.Belly))
                {
                    g.FillEllipse(wb, 84, 92, 52, 46); g.FillEllipse(wb, 184, 92, 52, 46); g.FillEllipse(wb, 124, 96, 72, 50);
                }
            if (cheeks && Char.Head != HeadStyle.Ghost)
                using (SolidBrush mb = new SolidBrush(Coat.UsePoints ? Coat.Shade(Coat.Points, 0.25f) : Coat.Belly))
                {
                    g.FillEllipse(mb, 137, 97, 25, 19); g.FillEllipse(mb, 158, 97, 25, 19); g.FillEllipse(mb, 149, 108, 22, 14);
                }
            if (Coat.UsePoints)
                using (GraphicsPath mask = Ellipse(118, 78, 84, 64))
                using (PathGradientBrush pb = new PathGradientBrush(mask))
                {
                    pb.CenterPoint = P(160, 106);
                    pb.CenterColor = A(Coat.Points, 235);
                    pb.SurroundColors = new Color[] { A(Coat.Points, 0) };
                    pb.FocusScales = new PointF(0.55f, 0.5f);
                    g.FillPath(pb, mask);
                }
            if (Char.EyePatches)
                using (SolidBrush pb = new SolidBrush(Coat.Points))
                    for (int side = -1; side <= 1; side += 2)
                    {
                        GraphicsState s2 = g.Save();
                        g.TranslateTransform(160 + side * EyeDX, EyeY + 2);
                        g.RotateTransform(-side * 28);
                        g.FillEllipse(pb, -16, -20, 32, 42);
                        g.Restore(s2);
                    }
            DrawBlush(g, 104, 192);
            g.Restore(fs);
            face.Dispose();
        }

        static void DrawStripes(Graphics g, Pen sp)
        {
            g.DrawLine(sp, 160, 39, 160, 54); g.DrawLine(sp, 147, 41, 149, 52); g.DrawLine(sp, 173, 41, 171, 52);
            g.DrawLine(sp, 90, 104, 103, 106); g.DrawLine(sp, 91, 113, 103, 113);
            g.DrawLine(sp, 230, 104, 217, 106); g.DrawLine(sp, 229, 113, 217, 113);
        }

        void DrawBlush(Graphics g, float lx, float rx)
        {
            if (blush > 0.02f || mode == CatMode.Sleep)
            {
                int ba = (int)(150 * Math.Max(blush, mode == CatMode.Sleep ? 0.45f : 0f));
                using (SolidBrush bl = new SolidBrush(A(ColorTranslator.FromHtml("#FF8FA3"), ba)))
                {
                    g.FillEllipse(bl, lx, 99, 24, 12); g.FillEllipse(bl, rx, 99, 24, 12);
                }
            }
        }

        void DrawAlienHead(Graphics g)
        {
            using (GraphicsPath head = new GraphicsPath())
            {
                head.AddBezier(160, 22, 65, 18, 72, 97, 124, 122);
                head.AddBezier(124, 122, 143, 151, 177, 151, 196, 122);
                head.AddBezier(196, 122, 248, 97, 255, 18, 160, 22);
                head.CloseFigure();
                FillOutlined(g, Coat.Fur, 5f, head);
            }
            using (SolidBrush sp = new SolidBrush(A(Coat.Stripe, 150)))
            {
                g.FillEllipse(sp, 150, 34, 9, 9); g.FillEllipse(sp, 166, 40, 6, 6); g.FillEllipse(sp, 139, 45, 5, 5);
                g.FillEllipse(sp, 194, 50, 7, 7);
            }
            using (SolidBrush cb = new SolidBrush(A(Coat.Belly, 160))) g.FillEllipse(cb, 136, 104, 48, 30);
            DrawBlush(g, 100, 196);
        }

        void DrawRobotHead(Graphics g)
        {
            using (GraphicsPath head = RoundRect(96, 32, 128, 104, 26)) FillOutlined(g, Coat.Fur, 5f, head);
            using (GraphicsPath shine = RoundRect(104, 38, 112, 18, 9))
            using (SolidBrush sb = new SolidBrush(A(Color.White, 70))) g.FillPath(sb, shine);
            using (GraphicsPath visor = RoundRect(108, 58, 104, 52, 18))
            {
                using (SolidBrush vb = new SolidBrush(Coat.Belly)) g.FillPath(vb, visor);
                using (Pen vp = new Pen(Coat.Stripe, 2.5f)) g.DrawPath(vp, visor);
                GraphicsState s2 = g.Save();
                g.SetClip(visor, CombineMode.Intersect);
                using (SolidBrush ref1 = new SolidBrush(A(Color.White, 25))) g.FillEllipse(ref1, 100, 40, 80, 34);
                g.Restore(s2);
            }
            using (SolidBrush rv = new SolidBrush(Coat.Stripe))
            {
                g.FillEllipse(rv, 104, 120, 5, 5); g.FillEllipse(rv, 211, 120, 5, 5);
            }
            DrawBlush(g, 112, 184);
        }

        void DrawHoodHead(Graphics g)
        {
            using (GraphicsPath hood = Ellipse(84, 20, 152, 136)) FillOutlined(g, Coat.Fur, 5f, hood);
            using (Pen fold = RoundPen(A(Coat.Shade(Coat.Fur, 0.25f), 150), 2f))
            {
                g.DrawArc(fold, 92, 34, 40, 90, 140, 80);
                g.DrawArc(fold, 188, 34, 40, 90, 320, 80);
                g.DrawArc(fold, 140, 24, 40, 30, 200, 140);
            }
            using (GraphicsPath open = Ellipse(110, 52, 100, 86))
            {
                using (Pen rim = RoundPen(Coat.Shade(Coat.Fur, 0.18f), 7f)) g.DrawPath(rim, open);
                using (PathGradientBrush pb = new PathGradientBrush(open))
                {
                    pb.CenterPoint = P(160, 104);
                    pb.CenterColor = Coat.Shade(Coat.Belly, 0.08f);
                    pb.SurroundColors = new Color[] { Coat.Belly };
                    g.FillPath(pb, open);
                }
                using (Pen ol = new Pen(A(OL, 200), 1.6f)) g.DrawPath(ol, open);
            }
            if (blush > 0.02f)
                using (SolidBrush bl = new SolidBrush(A(Coat.Eye, (int)(70 * blush))))
                {
                    g.FillEllipse(bl, 124, 104, 20, 9); g.FillEllipse(bl, 176, 104, 20, 9);
                }
        }

        void DrawDrawstrings(Graphics g)
        {
            using (Pen sp = RoundPen(Coat.Shade(Coat.Fur, 0.45f), 2.2f))
            {
                g.DrawBezier(sp, 136, 134, 132, 146, 135, 158, 132, 170);
                g.DrawBezier(sp, 184, 134, 188, 146, 185, 158, 188, 170);
            }
            using (GraphicsPath a1 = RoundRect(129, 168, 6, 9, 2))
            using (GraphicsPath a2 = RoundRect(185, 168, 6, 9, 2))
                FillOutlined(g, ColorTranslator.FromHtml("#B8BEC6"), 2f, a1, a2);
        }

        void DrawFace(Graphics g)
        {
            Color fc = FeatureColor;
            switch (Char.Nose)
            {
                case NoseStyle.Cat:
                    using (GraphicsPath nose = Poly(154, 97, 166, 97, 160, 103.5f))
                    using (SolidBrush nb = new SolidBrush(Coat.Nose))
                    using (Pen np = RoundPen(Coat.Nose, 3f))
                    {
                        g.DrawPath(np, nose); Sculpt(g, Coat.Nose, nose);
                        using (Pen shine = RoundPen(A(Color.White, 160), 1.3f)) g.DrawLine(shine, 156, 97.5f, 160, 97.5f);
                    }
                    break;
                case NoseStyle.Dots:
                    using (SolidBrush nb = new SolidBrush(Coat.Nose))
                    {
                        g.FillEllipse(nb, 154.5f, 100, 3.5f, 3.5f); g.FillEllipse(nb, 162, 100, 3.5f, 3.5f);
                    }
                    break;
            }
            switch (Char.Mouth)
            {
                case MouthStyle.Cat:
                case MouthStyle.Bunny:
                    if (Char.Mouth == MouthStyle.Bunny)
                        using (SolidBrush tb = new SolidBrush(Color.White))
                        using (Pen tp = new Pen(A(DefaultOutline, 180), 1.2f))
                        {
                            g.FillRectangle(tb, 155.5f, 106, 4.5f, 6); g.DrawRectangle(tp, 155.5f, 106, 4.5f, 6);
                            g.FillRectangle(tb, 160, 106, 4.5f, 6); g.DrawRectangle(tp, 160, 106, 4.5f, 6);
                        }
                    using (Pen mp = RoundPen(fc, 2f))
                    {
                        g.DrawLine(mp, 160, 103.5f, 160, 106.5f);
                        g.DrawArc(mp, 152, 102.5f, 8, 8, 0, 180);
                        g.DrawArc(mp, 160, 102.5f, 8, 8, 0, 180);
                    }
                    break;
                case MouthStyle.Small:
                    using (Pen mp = RoundPen(Coat.Shade(Coat.Fur, -0.55f), 2.2f)) g.DrawArc(mp, 150, 104, 20, 10, 25, 130);
                    break;
                case MouthStyle.O:
                    using (SolidBrush mb = new SolidBrush(Coat.Eye)) g.FillEllipse(mb, 156, 104, 8, Petting ? 6 : 9);
                    break;
                case MouthStyle.Grille:
                    using (GraphicsPath gr = RoundRect(140, 116, 40, 11, 4))
                    {
                        using (SolidBrush gb = new SolidBrush(Coat.Belly)) g.FillPath(gb, gr);
                        using (Pen gp = new Pen(Coat.Stripe, 1.6f)) g.DrawPath(gp, gr);
                    }
                    bool talking = Now - Math.Max(lastTime[0], lastTime[1]) < 0.3;
                    using (Pen bp = RoundPen(A(Coat.Eye, talking ? 255 : 150), 2f))
                        for (int i = 0; i < 5; i++)
                        {
                            float x = 146 + i * 7;
                            float h = talking ? 2 + (float)Math.Abs(Math.Sin(Now * 18 + i)) * 3 : 2;
                            g.DrawLine(bp, x, 121.5f - h, x, 121.5f + h);
                        }
                    break;
            }
            if (Char.Whiskers)
                using (Pen wp = RoundPen(A(fc, Char.Neon ? 230 : 115), 1.1f))
                {
                    g.DrawLine(wp, 122, 104, 84, 98); g.DrawLine(wp, 122, 108, 82, 109); g.DrawLine(wp, 122, 112, 86, 120);
                    g.DrawLine(wp, 198, 104, 236, 98); g.DrawLine(wp, 198, 108, 238, 109); g.DrawLine(wp, 198, 112, 234, 120);
                }
        }

        // ---------------------------------------------------------------- eyes
        void DrawEye(Graphics g, float cx, float cy, bool full, int side)
        {
            CatMode m = full ? mode : CatMode.Awake;
            float open = full ? eyeOpen : 1f;
            PointF lk = full ? look : P(0, 0);
            float dl = full ? dil : 1.2f;
            bool glowy = Char.Eyes == EyeStyle.Led || Char.Eyes == EyeStyle.Glow;
            Color lidColor = glowy ? Coat.Eye : (Char.EyePatches ? Color.White : DefaultOutline);
            if (Char.Neon) lidColor = Coat.Points;

            using (Pen lid = RoundPen(lidColor, 3.2f))
            {
                if (full && Petting)
                {
                    if (glowy) Glow(g, Coat.Eye, cx, cy, 14, 90);
                    g.DrawLines(lid, new PointF[] { P(cx - 10, cy + 4), P(cx, cy - 5), P(cx + 10, cy + 4) });
                    return;
                }
                if (m == CatMode.Sleep && open < 0.5f)
                {
                    if (glowy) { using (Pen dim = RoundPen(A(Coat.Eye, 120), 2.6f)) g.DrawLine(dim, cx - 9, cy + 1, cx + 9, cy + 1); }
                    else g.DrawArc(lid, cx - 11, cy - 4, 22, 10, 10, 160);
                    return;
                }
                if (open < 0.18f)
                {
                    g.DrawLine(lid, cx - 11, cy + 1, cx + 11, cy + 1);
                    return;
                }
            }

            switch (Char.Eyes)
            {
                case EyeStyle.Cat: DrawCatEye(g, cx, cy, open, lk, dl); break;
                case EyeStyle.Bead:
                    {
                        float w = 13f * (0.9f + dl * 0.1f), h = 18f * open;
                        float x = cx + lk.X * 0.6f, y = cy + lk.Y * 0.6f;
                        using (SolidBrush b = new SolidBrush(Coat.Eye)) g.FillEllipse(b, x - w / 2, y - h / 2, w, h);
                        using (SolidBrush hb = new SolidBrush(Color.White)) g.FillEllipse(hb, x - 4, y - h / 2 + 3, 5, 5 * Math.Max(open, 0.5f));
                        break;
                    }
                case EyeStyle.Alien:
                    {
                        GraphicsState s2 = g.Save();
                        g.TranslateTransform(cx, cy);
                        g.RotateTransform(-side * 22);
                        g.ScaleTransform(1, open);
                        using (GraphicsPath eye = Ellipse(-19, -12, 38, 24))
                        {
                            using (SolidBrush b = new SolidBrush(Coat.Eye)) g.FillPath(b, eye);
                            GraphicsState s3 = g.Save();
                            g.SetClip(eye, CombineMode.Intersect);
                            using (SolidBrush sh = new SolidBrush(A(Color.White, 28))) g.FillEllipse(sh, -22, -16, 40, 16);
                            g.Restore(s3);
                            using (Pen op = new Pen(OL, 2.2f)) g.DrawPath(op, eye);
                        }
                        g.Restore(s2);
                        float hs = 4.5f + (dl - 1f) * 4f;
                        using (SolidBrush hb = new SolidBrush(Color.White))
                        {
                            g.FillEllipse(hb, cx - 8 + lk.X * 1.4f - hs / 2, cy - 4 * open + lk.Y - hs / 2, hs * 1.4f, hs);
                            g.FillEllipse(hb, cx + 4 + lk.X * 1.4f, cy + 2 * open + lk.Y, 3, 3);
                        }
                        break;
                    }
                case EyeStyle.Led:
                    {
                        float x = cx + lk.X * 1.3f, y = cy + lk.Y * 1.2f;
                        float r = 7.5f * (0.85f + dl * 0.15f);
                        Glow(g, Coat.Eye, x, y, r * 2.2f, 130);
                        using (GraphicsPath led = RoundRect(x - r, y - r * open, r * 2, r * 2 * open, r * open * 0.9f))
                        using (SolidBrush lb = new SolidBrush(Coat.Eye)) g.FillPath(lb, led);
                        using (SolidBrush cb = new SolidBrush(A(Color.White, 210))) g.FillEllipse(cb, x - r * 0.45f, y - r * 0.55f * open, r * 0.7f, r * 0.7f * open);
                        break;
                    }
                case EyeStyle.Glow:
                    {
                        float x = cx + lk.X * 0.9f, y = cy + lk.Y * 0.8f;
                        Glow(g, Coat.Eye, x, y, 16, 120);
                        GraphicsState s2 = g.Save();
                        g.TranslateTransform(x, y);
                        g.RotateTransform(side * 12);
                        float w = 18f * (0.9f + 0.1f * dl), h = 8f * open * (0.9f + 0.15f * dl);
                        using (SolidBrush b = new SolidBrush(Coat.Eye)) g.FillEllipse(b, -w / 2, -h / 2, w, h);
                        using (SolidBrush c = new SolidBrush(A(Color.White, 190))) g.FillEllipse(c, -w / 4, -h / 4, w / 2, h / 2);
                        g.Restore(s2);
                        break;
                    }
            }
        }

        void DrawCatEye(Graphics g, float cx, float cy, float o, PointF lk, float dl)
        {
            float h = 28f * o;
            using (GraphicsPath iris = Ellipse(cx - 12, cy - h / 2, 24, h))
            {
                if (Char.Neon) Glow(g, Coat.Eye, cx, cy, 20, 90);
                SoftShadow(g, cx - 16, cy - h / 2 - 1, 32, h + 7, 105);
                using (LinearGradientBrush irisLight = new LinearGradientBrush(iris.GetBounds(),
                    Coat.Shade(Coat.Eye, -0.60f), Coat.Shade(Coat.Eye, 0.35f), 90f))
                    g.FillPath(irisLight, iris);
                GraphicsState st = g.Save();
                g.SetClip(iris, CombineMode.Intersect);
                using (SolidBrush top = new SolidBrush(A(Coat.Shade(Coat.Eye, -0.35f), 150)))
                    g.FillEllipse(top, cx - 16, cy - h / 2 - 10, 32, h * 0.55f + 4);
                float pw = Math.Min(18f, 11f * dl), ph = 23f * o;
                using (SolidBrush pb = new SolidBrush(Color.FromArgb(255, 27, 26, 34)))
                    g.FillEllipse(pb, cx + lk.X - pw / 2, cy + lk.Y * o - ph / 2, pw, ph);
                using (SolidBrush hb = new SolidBrush(Color.White))
                    g.FillEllipse(hb, cx - 8, cy - 9f * o, 7, 7 * Math.Max(o, 0.5f));
                using (SolidBrush hb2 = new SolidBrush(A(Color.White, 220)))
                    g.FillEllipse(hb2, cx + 3, cy + 3f * o, 3.5f, 3.5f);
                g.Restore(st);
                using (Pen op = new Pen(Char.EyePatches ? A(Color.White, 60) : Coat.Shade(Coat.Eye, -0.65f), 1.5f)) g.DrawPath(op, iris);
                using (Pen rim = RoundPen(A(Color.White, 150), 1f))
                    g.DrawArc(rim, cx - 10, cy - h / 2 + 1, 20, h - 2, 35, 100);
            }
        }

        // ---------------------------------------------------------------- desk, keyboard, mouse
        void DrawDesk(Graphics g)
        {
            using (GraphicsPath desk = RoundRect(16, 170, 288, 66, 16))
            {
                using (Pen op = RoundPen(ColorTranslator.FromHtml("#B5A28C"), 1.5f)) g.DrawPath(op, desk);
                using (LinearGradientBrush db = new LinearGradientBrush(desk.GetBounds(),
                    ColorTranslator.FromHtml("#FFF5E6"), ColorTranslator.FromHtml("#DCC9AD"), 90f)) g.FillPath(db, desk);
                GraphicsState st = g.Save();
                g.SetClip(desk, CombineMode.Intersect);
                using (SolidBrush fb = new SolidBrush(ColorTranslator.FromHtml("#D9C4A6"))) g.FillRectangle(fb, 10, 226, 300, 14);
                using (Pen hl = new Pen(A(Color.White, 120), 2f)) g.DrawLine(hl, 28, 173.5f, 292, 173.5f);
                g.Restore(st);
            }
        }

        void DrawKeyboard(Graphics g)
        {
            float kx = KX;
            using (SolidBrush sh = new SolidBrush(A(DefaultOutline, 40))) using (GraphicsPath s = RoundRect(kx - 6, KY - 2, 15 * U + 12, 5 * RH + 10, 6))
                g.FillPath(sh, s);
            using (GraphicsPath basePath = RoundRect(kx - 6, KY - 5, 15 * U + 12, 5 * RH + 10, 6))
            {
                using (Pen op = RoundPen(DefaultOutline, 3f)) g.DrawPath(op, basePath);
                Sculpt(g, KeyboardColor, basePath);
            }
            bool leftActive = SideActive(0), rightActive = SideActive(1);
            Color capEdge = Coat.Shade(KeycapColor, Coat.Luma(KeycapColor) < 0.3f ? 0.15f : -0.22f);
            foreach (KeyDef k in Keys.Values)
            {
                RectangleF kr = KeyRect(k);
                int side = SideOf(k.Vk);
                bool lit = Privacy ? (side == 1 ? rightActive : leftActive) : held.ContainsKey(k.Vk);
                float x = kr.X + 0.8f, y = kr.Y + 0.8f, w = kr.Width - 1.6f, h = RH - 1.6f;
                if (lit)
                {
                    using (SolidBrush gl = new SolidBrush(A(GlowColor, 110))) using (GraphicsPath gp = RoundRect(x - 2.5f, y - 2.5f, w + 5, h + 5, 4))
                        g.FillPath(gl, gp);
                    y += 0.7f;
                }
                else
                {
                    using (SolidBrush eb = new SolidBrush(capEdge)) using (GraphicsPath ep = RoundRect(x, y + 0.9f, w, h, 1.6f))
                        g.FillPath(eb, ep);
                }
                using (SolidBrush cb = new SolidBrush(lit ? GlowColor : KeycapColor)) using (GraphicsPath cp = RoundRect(x, y, w, h - (lit ? 0 : 0.6f), 1.6f))
                    g.FillPath(cb, cp);
            }
        }

        void DrawMouse(Graphics g)
        {
            float mx = mousePos.X, my = mousePos.Y;
            using (SolidBrush sh = new SolidBrush(A(DefaultOutline, 40))) g.FillEllipse(sh, mx - 14, my - 17, 28, 40);
            using (GraphicsPath body = Ellipse(mx - 14, my - 20, 28, 40))
            {
                using (Pen op = RoundPen(DefaultOutline, 3f)) g.DrawPath(op, body);
                Sculpt(g, MouseColor, body);
                GraphicsState st = g.Save();
                g.SetClip(body, CombineMode.Intersect);
                using (SolidBrush gb = new SolidBrush(A(GlowColor, 230)))
                {
                    if (mouseL) g.FillRectangle(gb, mx - 14, my - 20, 14, 16);
                    if (mouseR) g.FillRectangle(gb, mx, my - 20, 14, 16);
                }
                using (Pen lp = new Pen(A(Coat.Luma(MouseColor) < 0.3f ? Color.White : DefaultOutline, 140), 1.4f))
                {
                    g.DrawLine(lp, mx, my - 20, mx, my - 4);
                    g.DrawArc(lp, mx - 18, my - 10, 36, 12, 20, 140);
                }
                g.Restore(st);
            }
            using (GraphicsPath wheel = RoundRect(mx - 2.6f, my - 16, 5.2f, 9, 2.4f))
            using (SolidBrush wb = new SolidBrush(mouseM ? GlowColor : Color.FromArgb(255, 80, 80, 92)))
                g.FillPath(wb, wheel);
            using (Pen rib = new Pen(A(Color.White, 170), 1f))
                for (int i = 0; i < 3; i++)
                {
                    float ph = ((wheelPhase + i * 3f) % 9f + 9f) % 9f;
                    float y = my - 16 + ph;
                    if (y > my - 15.5f && y < my - 7.5f) g.DrawLine(rib, mx - 1.7f, y, mx + 1.7f, y);
                }
        }

        // ---------------------------------------------------------------- arms & paws
        void DrawArms(Graphics g)
        {
            PointF sl = Shoulder(0), sr = Shoulder(1);
            Color arm = ArmColor;
            SoftShadow(g, paw[0].X - 21, paw[0].Y - 1, 46, 22, 95);
            SoftShadow(g, paw[1].X - 21, paw[1].Y - 1, 46, 22, 95);
            StrokeOutlined(g, arm, 22f, new PointF[] { sl, paw[0] });
            StrokeOutlined(g, arm, 22f, new PointF[] { sr, paw[1] });

            if (Char.Body == BodyStyle.Hoodie)
                using (Pen cuff = new Pen(Coat.Shade(Coat.Fur, -0.25f), 20f))
                {
                    g.DrawLine(cuff, Lerp(sl, paw[0], 0.76f), Lerp(sl, paw[0], 0.86f));
                    g.DrawLine(cuff, Lerp(sr, paw[1], 0.76f), Lerp(sr, paw[1], 0.86f));
                }
            else if (Char.Body == BodyStyle.Robot)
                using (Pen ring = new Pen(Coat.Stripe, 2.2f))
                    foreach (int s in new int[] { 0, 1 })
                    {
                        PointF a = s == 0 ? sl : sr, b = paw[s];
                        float dx = b.X - a.X, dy = b.Y - a.Y, l = (float)Math.Sqrt(dx * dx + dy * dy) + 0.01f;
                        float nx = -dy / l * 9.5f, ny = dx / l * 9.5f;
                        foreach (float t in new float[] { 0.45f, 0.7f })
                        {
                            PointF c = Lerp(a, b, t);
                            g.DrawLine(ring, c.X - nx, c.Y - ny, c.X + nx, c.Y + ny);
                        }
                    }
            else if ((Coat.UsePoints || Coat.WhitePaws || Char.PawsAccent) && !Char.ArmsAccent)
                using (Pen pp = RoundPen(PawColor, 20f))
                {
                    g.DrawLine(pp, Lerp(sl, paw[0], 0.6f), paw[0]); g.DrawLine(pp, Lerp(sr, paw[1], 0.6f), paw[1]);
                }
            DrawPaw(g, paw[0], squash[0]);
            DrawPaw(g, paw[1], squash[1]);
        }

        void DrawPaw(Graphics g, PointF p, float sq)
        {
            float w = 26f + 5f * sq, h = 19f - 4f * sq;
            float y = p.Y + 2.5f * sq;
            using (GraphicsPath pw = Ellipse(p.X - w / 2, y - h / 2, w, h))
                FillOutlined(g, PawColor, 4f, pw);
            float by = y + h / 2 - 1.5f;
            if (Char.Body == BodyStyle.Robot)
            {
                using (Pen tp = RoundPen(A(DefaultOutline, 170), 1.8f)) g.DrawLine(tp, p.X, by - 6f, p.X, by);
                return;
            }
            if (Char.Head == HeadStyle.Ghost) return;
            using (Pen tp = RoundPen(A(Char.Neon ? Coat.Points : DefaultOutline, 150), 1.5f))
            {
                g.DrawLine(tp, p.X - 4.5f, by - 4f, p.X - 4.5f, by);
                g.DrawLine(tp, p.X + 4.5f, by - 4f, p.X + 4.5f, by);
            }
        }

        // ---------------------------------------------------------------- particles
        void DrawParticles(Graphics g)
        {
            foreach (Particle p in parts)
            {
                float t = p.Age / p.Life;
                int a = (int)(255 * (t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f));
                switch (p.Kind)
                {
                    case PKind.Heart:
                        {
                            float s = p.Size, x = p.X, y = p.Y;
                            using (GraphicsPath hp = new GraphicsPath())
                            {
                                hp.AddBezier(x, y + s, x - s, y + s * 0.45f, x - s * 0.55f, y - s * 0.4f, x, y + s * 0.12f);
                                hp.AddBezier(x, y + s * 0.12f, x + s * 0.55f, y - s * 0.4f, x + s, y + s * 0.45f, x, y + s);
                                hp.CloseFigure();
                                using (SolidBrush hb = new SolidBrush(A(ColorTranslator.FromHtml("#FF6F91"), a))) g.FillPath(hb, hp);
                                using (Pen hpn = new Pen(A(DefaultOutline, a * 2 / 3), 1.2f)) g.DrawPath(hpn, hp);
                            }
                            break;
                        }
                    case PKind.Z:
                        {
                            float size = 11f + t * 9f;
                            using (GraphicsPath zp = new GraphicsPath())
                            {
                                zp.AddString(t < 0.45f ? "z" : "Z", FontFamily.GenericSansSerif, (int)FontStyle.Bold, size, new PointF(p.X, p.Y), StringFormat.GenericDefault);
                                using (Pen zo = RoundPen(A(DefaultOutline, a), 2.6f)) g.DrawPath(zo, zp);
                                using (SolidBrush zb = new SolidBrush(A(Color.White, a))) g.FillPath(zb, zp);
                            }
                            break;
                        }
                    case PKind.Pop: DrawPop(g, p, t); break;
                    case PKind.Bit:
                        using (Font f = new Font(FontFamily.GenericMonospace, p.Size, FontStyle.Bold, GraphicsUnit.Pixel))
                        using (SolidBrush b = new SolidBrush(A(GlowColor, a)))
                            g.DrawString(p.Text, f, b, p.X - p.Size / 3, p.Y);
                        break;
                    case PKind.Spark:
                        using (SolidBrush b = new SolidBrush(A(GlowColor, Math.Min(255, a + 60))))
                            g.FillEllipse(b, p.X - p.Size / 2, p.Y - p.Size / 2, p.Size, p.Size);
                        break;
                }
            }
        }

        void DrawPop(Graphics g, Particle p, float t)
        {
            // springy "pop": overshoot then settle, fade out at the end
            float s;
            if (t < 0.18f) { float u = t / 0.18f; s = 0.3f + 0.95f * u; }
            else if (t < 0.3f) { float u = (t - 0.18f) / 0.12f; s = 1.25f - 0.25f * u; }
            else s = 1f;
            int a = t < 0.65f ? 255 : (int)(255 * (1 - (t - 0.65f) / 0.35f));
            using (Font f = new Font(FontFamily.GenericSansSerif, 10.5f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                SizeF ts = g.MeasureString(p.Text, f, PointF.Empty, StringFormat.GenericTypographic);
                float w = Math.Max(17f, ts.Width + 10f), h = 17f;
                GraphicsState st = g.Save();
                g.TranslateTransform(p.X, p.Y);
                g.ScaleTransform(s, s);
                using (GraphicsPath shadow = RoundRect(-w / 2, -h / 2 + 2.2f, w, h, 4))
                using (SolidBrush sb = new SolidBrush(A(Coat.Shade(KeycapColor, Coat.Luma(KeycapColor) < 0.3f ? 0.25f : -0.3f), a)))
                    g.FillPath(sb, shadow);
                using (GraphicsPath cap = RoundRect(-w / 2, -h / 2, w, h, 4))
                {
                    using (SolidBrush cb = new SolidBrush(A(KeycapColor, a))) g.FillPath(cb, cap);
                    using (Pen op = new Pen(A(GlowColor, a), 1.6f)) g.DrawPath(op, cap);
                }
                Color tc = Coat.Luma(KeycapColor) < 0.4f ? GlowColor : DefaultOutline;
                using (SolidBrush tb = new SolidBrush(A(tc, a)))
                    g.DrawString(p.Text, f, tb, -ts.Width / 2, -ts.Height / 2 - 0.5f, StringFormat.GenericTypographic);
                g.Restore(st);
            }
        }

        // ---------------------------------------------------------------- post effects & icon
        public void ApplyGlitch(Bitmap frame)
        {
            if (!GlitchNow) return;
            using (Bitmap copy = (Bitmap)frame.Clone())
            using (Graphics g = Graphics.FromImage(frame))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                int w = frame.Width, h = frame.Height;
                for (int i = 0; i < 4; i++)
                {
                    int y = rnd.Next(h), sh = 2 + rnd.Next(Math.Max(3, h / 14));
                    int dx = (rnd.Next(2) == 0 ? -1 : 1) * (3 + rnd.Next(Math.Max(4, w / 40)));
                    Rectangle src = new Rectangle(0, y, w, Math.Min(sh, h - y));
                    if (src.Height <= 0) continue;
                    g.DrawImage(copy, new Rectangle(dx, y, w, src.Height), src, GraphicsUnit.Pixel);
                }
            }
        }

        public Bitmap RenderIcon(int size)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float s = size / 156f;
                g.ScaleTransform(s, s);
                g.TranslateTransform(-82, 0);
                DrawHead(g, false);
            }
            return bmp;
        }
    }
}
