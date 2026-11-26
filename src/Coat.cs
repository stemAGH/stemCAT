// stemCAT - coat colours and presets
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

namespace stemCAT
{
    public class Coat
    {
        public string Name = "Custom";
        public string CharacterId = "cat";
        public Color Fur, Belly, Points, Stripe, Patch1, Patch2, Eye, Nose, EarInner;
        public bool UsePoints, UseStripes, UsePatches, WhitePaws;

        public Coat Clone()
        {
            return (Coat)MemberwiseClone();
        }

        public static Color H(string hex)
        {
            return ColorTranslator.FromHtml(hex);
        }

        static string Hex(Color c)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }

        static Coat Make(string name, string fur, string belly, string eye, string nose)
        {
            Coat c = new Coat();
            c.Name = name;
            c.Fur = H(fur); c.Belly = H(belly); c.Eye = H(eye); c.Nose = H(nose);
            c.Points = c.Fur; c.Stripe = Shade(c.Fur, -0.25f);
            c.Patch1 = c.Fur; c.Patch2 = c.Fur;
            c.EarInner = H("#F2A9B3");
            return c;
        }

        public static Color Shade(Color c, float amt)
        {
            // amt < 0 darkens, > 0 lightens
            Func<int, int> f = delegate (int v)
            {
                double r = amt < 0 ? v * (1 + amt) : v + (255 - v) * amt;
                return Math.Max(0, Math.Min(255, (int)Math.Round(r)));
            };
            return Color.FromArgb(c.A, f(c.R), f(c.G), f(c.B));
        }

        public static float Luma(Color c)
        {
            return (0.299f * c.R + 0.587f * c.G + 0.114f * c.B) / 255f;
        }

        public static List<Coat> Presets()
        {
            List<Coat> l = new List<Coat>();
            Coat c;

            c = Make("Orange Tabby", "#F2A65A", "#FCE6CB", "#86B95B", "#E58C8F");
            c.UseStripes = true; c.Stripe = H("#D7742A"); l.Add(c);

            c = Make("Midnight Black", "#2D2C33", "#3C3A44", "#F2C744", "#5B4650");
            c.EarInner = H("#6B4E5C"); l.Add(c);

            c = Make("Snow White", "#FAFAF6", "#FFFFFF", "#6DB6EA", "#F4A6B0");
            c.EarInner = H("#F8C5CC"); l.Add(c);

            c = Make("Ash Gray", "#9B9FA7", "#D9DBDF", "#E2B13F", "#D98E99"); l.Add(c);

            c = Make("Siamese", "#F3E6D1", "#FBF5EA", "#4A90D9", "#6B4A3A");
            c.UsePoints = true; c.Points = H("#5B3F2F"); c.EarInner = H("#7A5644"); l.Add(c);

            c = Make("Calico", "#FBF8F2", "#FFFFFF", "#C9A640", "#F0A0A8");
            c.UsePatches = true; c.Patch1 = H("#E8913F"); c.Patch2 = H("#3B3333"); l.Add(c);

            c = Make("Tuxedo", "#2A2A2F", "#FFFFFF", "#9CCB5B", "#F0A0A8");
            c.WhitePaws = true; c.EarInner = H("#6B4E5C"); l.Add(c);

            c = Make("Tortoiseshell", "#3B2A24", "#5A4034", "#E3A332", "#5E4038");
            c.UsePatches = true; c.Patch1 = H("#C8692E"); c.Patch2 = H("#8E4E2A"); c.EarInner = H("#7A5048"); l.Add(c);

            c = Make("Russian Blue", "#7D8B9B", "#99A6B4", "#6CC06C", "#8C6E7A"); l.Add(c);

            c = Make("Cream", "#F6DCB2", "#FFF3DF", "#D6993A", "#EE9AA0");
            c.UseStripes = true; c.Stripe = H("#E6BE86"); l.Add(c);

            c = Make("Brown Tabby", "#9C7552", "#DCC7A9", "#B5C24A", "#B5766E");
            c.UseStripes = true; c.Stripe = H("#4E3826"); l.Add(c);

            c = Make("Silver Tabby", "#C9CDD2", "#EFF1F3", "#78B860", "#D99AA2");
            c.UseStripes = true; c.Stripe = H("#4C5258"); l.Add(c);

            c = Make("Ginger & White", "#EE9B4E", "#FFFFFF", "#D9A833", "#EE9298");
            c.UseStripes = true; c.Stripe = H("#D27A30"); c.WhitePaws = true; l.Add(c);

            c = Make("Chocolate", "#6B4733", "#8C644A", "#E3A13A", "#9A6A5E");
            c.EarInner = H("#A07060"); l.Add(c);

            c = Make("Lilac", "#CBBAC0", "#E6DBDE", "#D7A85A", "#D99AA8"); l.Add(c);

            return l;
        }

        public static Coat FindPreset(string name)
        {
            foreach (Coat c in Presets())
                if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        // Serialise: name|fur|belly|points|stripe|patch1|patch2|eye|nose|earInner|flags
        public string Serialize()
        {
            int flags = (UsePoints ? 1 : 0) | (UseStripes ? 2 : 0) | (UsePatches ? 4 : 0) | (WhitePaws ? 8 : 0);
            return string.Join("|", new string[] {
                Name.Replace("|", "/"), Hex(Fur), Hex(Belly), Hex(Points), Hex(Stripe), Hex(Patch1), Hex(Patch2),
                Hex(Eye), Hex(Nose), Hex(EarInner), flags.ToString(CultureInfo.InvariantCulture), CharacterId });
        }

        public static Coat Deserialize(string s)
        {
            try
            {
                string[] p = s.Split('|');
                if (p.Length < 11) return null;
                Coat c = new Coat();
                c.Name = p[0];
                c.Fur = H(p[1]); c.Belly = H(p[2]); c.Points = H(p[3]); c.Stripe = H(p[4]);
                c.Patch1 = H(p[5]); c.Patch2 = H(p[6]); c.Eye = H(p[7]); c.Nose = H(p[8]); c.EarInner = H(p[9]);
                int f = int.Parse(p[10], CultureInfo.InvariantCulture);
                c.UsePoints = (f & 1) != 0; c.UseStripes = (f & 2) != 0; c.UsePatches = (f & 4) != 0; c.WhitePaws = (f & 8) != 0;
                c.CharacterId = p.Length > 11 && p[11].Length > 0 ? p[11] : "cat";
                return c;
            }
            catch
            {
                return null;
            }
        }
    }
}
