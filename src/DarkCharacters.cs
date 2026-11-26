// Angular, elongated silhouettes for the night cat and masked operator.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace stemCAT
{
    public partial class CatScene
    {
        bool DarkCharacter { get { return Char.Id == "nightcat" || Char.Id == "cyberstalker" || Char.Id == "wraith"; } }
        Color Ink { get { return Color.FromArgb(9, 13, 20); } }
        Color Shell { get { return Coat.Shade(Coat.Fur, -0.65f); } }

        void Facet(Graphics g, Color c, params float[] points)
        {
            using (GraphicsPath path = Poly(points))
            using (SolidBrush brush = new SolidBrush(c)) g.FillPath(brush, path);
        }

        void DrawDarkCharacter(Graphics g)
        {
            bool hacker = Char.Head == HeadStyle.Hood;
            Color shell = Shell, edge = Coat.Shade(shell, 0.15f);
            if (!hacker)
            {
                float sway = (float)Math.Sin(Now * 1.5) * 10;
                using (Pen tail = RoundPen(Ink, 10))
                    g.DrawBezier(tail, 196, 168, 265, 179, 273 + sway, 107, 255 + sway, 87);
                using (Pen rim = RoundPen(edge, 2))
                    g.DrawBezier(rim, 195, 165, 261, 177, 269 + sway, 108, 254 + sway, 88);
            }
            GraphicsState st = g.Save();
            g.TranslateTransform(0, bodyDy);
            Facet(g, shell, 143, 80, 177, 80, 204, 113, 213, 180, 108, 180, 116, 114);
            Facet(g, Ink, 160, 91, 179, 105, 192, 180, 162, 180);
            Facet(g, edge, 143, 85, 123, 116, 132, 167, 141, 116);
            if (hacker)
            {
                Facet(g, Coat.Shade(shell, 0.09f), 136, 96, 153, 112, 142, 142, 122, 115);
                Facet(g, Coat.Shade(shell, -0.20f), 182, 96, 166, 113, 178, 142, 200, 114);
                using (Pen seam = new Pen(Coat.Shade(shell, 0.23f), 0.8f))
                {
                    g.DrawLine(seam, 160, 113, 160, 177);
                    g.DrawLine(seam, 124, 149, 145, 142);
                    g.DrawLine(seam, 176, 142, 198, 149);
                }
                using (Pen accent = new Pen(A(Coat.Eye, 170), 1.4f)) g.DrawLine(accent, 126, 126, 139, 122);
            }
            else
            {
                Facet(g, Coat.Shade(shell, 0.12f), 143, 91, 160, 106, 177, 91, 168, 134, 160, 157, 149, 131);
                using (Pen collar = new Pen(Ink, 5)) g.DrawLine(collar, 137, 98, 182, 98);
                Facet(g, Coat.Eye, 158, 98, 163, 98, 162, 105, 159, 105);
            }
            g.Restore(st);
            DrawHead(g, true);

            using (GraphicsPath desk = RoundRect(16, 174, 288, 62, 5))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(19, 25, 34))) g.FillPath(b, desk);
            using (Pen rim = new Pen(Color.FromArgb(51, 65, 79), 1)) g.DrawLine(rim, 23, 175, 297, 175);
            using (Pen rim = new Pen(A(Coat.Eye, 100), 1)) g.DrawLine(rim, 28, 232, 292, 232);
            // Keep the existing key layout and input feedback on the darker desk.
            Color kb = KeyboardColor, cap = KeycapColor, mouse = MouseColor;
            try
            {
                KeyboardColor = Color.FromArgb(12, 17, 24);
                KeycapColor = Color.FromArgb(45, 55, 67);
                MouseColor = Color.FromArgb(35, 44, 55);
                DrawKeyboard(g); DrawMouse(g);
            }
            finally { KeyboardColor = kb; KeycapColor = cap; MouseColor = mouse; }
            for (int side = 0; side < 2; side++)
            {
                PointF shoulder = P(side == 0 ? 124 : 196, 116 + bodyDy);
                PointF hand = paw[side];
                PointF elbow = P(side == 0 ? 110 : 210, 156 + bodyDy);
                using (Pen sleeve = RoundPen(Ink, hacker ? 20 : 14)) g.DrawLines(sleeve, new PointF[] { shoulder, elbow, hand });
                using (Pen sleeve = RoundPen(shell, hacker ? 16 : 10)) g.DrawLines(sleeve, new PointF[] { shoulder, elbow, hand });
                using (Pen stitch = new Pen(edge, 1)) g.DrawLine(stitch, shoulder.X - 4, shoulder.Y, elbow.X - 4, elbow.Y);
                SoftShadow(g, hand.X - 15, hand.Y + 1, 32, 14, 100);
                using (GraphicsPath glove = RoundRect(hand.X - 10, hand.Y - 6, 20, 13 - squash[side] * 2, 3))
                using (SolidBrush b = new SolidBrush(hacker ? Ink : shell)) g.FillPath(b, glove);
                using (Pen fingers = RoundPen(hacker ? edge : Coat.Shade(Coat.Belly, -0.25f), 1))
                    for (int i = -1; i <= 1; i++) g.DrawLine(fingers, hand.X + i * 4, hand.Y + 1, hand.X + i * 4 - 1, hand.Y + 6);
            }
            DrawParticles(g);
        }

        void DrawDarkHead(Graphics g, bool full)
        {
            bool hacker = Char.Head == HeadStyle.Hood;
            Color shell = Shell, edge = Coat.Shade(shell, 0.18f);
            if (hacker)
            {
                Facet(g, Ink, 160, 7, 189, 23, 205, 62, 204, 93, 182, 109, 139, 107, 116, 92, 118, 58, 132, 23);
                Facet(g, shell, 160, 9, 135, 28, 121, 65, 124, 91, 144, 100, 131, 66, 143, 34);
                Facet(g, Coat.Shade(shell, 0.08f), 160, 9, 185, 27, 200, 66, 197, 91, 180, 102, 189, 63, 176, 34);
                Facet(g, Color.FromArgb(3, 6, 10), 160, 29, 181, 45, 189, 72, 179, 94, 160, 103, 140, 92, 132, 71, 141, 45);
                using (Pen seam = new Pen(edge, 1))
                {
                    g.DrawLines(seam, new PointF[] { P(127, 82), P(136, 49), P(160, 23), P(185, 49), P(194, 82) });
                    g.DrawLine(seam, 160, 10, 160, 22);
                }
                // A faceted respirator replaces the floating cute face.
                Facet(g, Color.FromArgb(31, 39, 47), 140, 72, 160, 79, 181, 72, 177, 92, 160, 101, 144, 91);
                Facet(g, Color.FromArgb(15, 21, 29), 160, 79, 181, 72, 177, 92, 160, 101);
                using (Pen vents = new Pen(Color.FromArgb(64, 77, 87), 1))
                    for (int i = 0; i < 4; i++) { g.DrawLine(vents, 145, 79 + i * 3, 154, 82 + i * 3); g.DrawLine(vents, 166, 82 + i * 3, 175, 79 + i * 3); }
                DrawNarrowEye(g, 146, 63, -1, full, true);
                DrawNarrowEye(g, 175, 63, 1, full, true);
            }
            else
            {
                Facet(g, shell, 114, 5, 146, 33, 174, 33, 207, 5, 200, 59, 210, 75, 198, 73, 201, 85, 181, 88, 172, 99, 148, 99, 136, 88, 118, 85, 121, 73, 111, 75, 120, 57);
                Facet(g, Ink, 120, 14, 137, 34, 123, 45);
                Facet(g, Ink, 200, 14, 182, 34, 197, 45);
                Facet(g, edge, 125, 49, 145, 36, 159, 40, 147, 53, 132, 57);
                Facet(g, Coat.Shade(shell, -0.22f), 160, 40, 178, 35, 197, 49, 188, 71, 176, 84, 160, 92);
                Facet(g, Coat.Shade(shell, 0.12f), 132, 74, 151, 72, 160, 81, 169, 72, 187, 74, 173, 88, 160, 95, 146, 87);
                Facet(g, Ink, 154, 77, 166, 77, 160, 83);
                using (Pen mouth = new Pen(Ink, 1.2f))
                { g.DrawLine(mouth, 160, 82, 160, 88); g.DrawLine(mouth, 151, 88, 170, 87); }
                using (Pen fur = new Pen(A(edge, 170), 0.8f))
                    for (int i = 0; i < 3; i++)
                    { g.DrawLine(fur, 136, 80 + i * 3, 106, 77 + i * 6); g.DrawLine(fur, 185, 80 + i * 3, 214, 77 + i * 6); }
                using (Pen scar = new Pen(Coat.Shade(shell, 0.30f), 1))
                { g.DrawLine(scar, 182, 44, 176, 70); g.DrawLine(scar, 186, 46, 181, 70); }
                DrawNarrowEye(g, 140, 62, -1, full, false);
                DrawNarrowEye(g, 180, 62, 1, full, false);
            }
        }

        void DrawNarrowEye(Graphics g, float x, float y, int side, bool full, bool hacker)
        {
            float open = full ? eyeOpen : 1;
            if (full && Petting) open = 0.40f;
            if (full && mode == CatMode.Sleep) open = 0.08f;
            GraphicsState st = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(-side * 13);
            if (open < 0.18f)
            {
                using (Pen lid = new Pen(A(Coat.Eye, 130), 1)) g.DrawLine(lid, -9, 0, 9, 0);
            }
            else
            {
                float h = (hacker ? 2.6f : 4.4f) * open;
                Glow(g, Coat.Eye, 0, 0, 9, hacker ? 35 : 25);
                Facet(g, Coat.Eye, -11, -h, 10, -h * 0.1f, 5, h, -7, h);
                if (!hacker)
                {
                    float px = full ? look.X * 0.45f : 0;
                    Facet(g, Ink, px - 1, -h, px + 1, -h, px + 1.5f, h, px - 1.5f, h);
                }
                using (Pen brow = new Pen(Ink, 2)) g.DrawLine(brow, -12, -h - 1, 11, -h * 0.1f - 1);
            }
            g.Restore(st);
        }
    }
}
