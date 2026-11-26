// stemCAT - characters (the cat and friends) and their colour palettes
using System;
using System.Collections.Generic;
using System.Drawing;

namespace stemCAT
{
    public enum EarStyle { Cat, Fox, Bunny, Round, None, Antennae, Robot }
    public enum HeadStyle { Cat, Alien, Robot, Hood, Ghost }
    public enum EyeStyle { Cat, Alien, Led, Glow, Bead }
    public enum NoseStyle { Cat, Dots, None }
    public enum MouthStyle { Cat, Bunny, Small, Grille, O, None }
    public enum TailStyle { Cat, Bushy, Puff, Cable, Wisp, None }
    public enum BodyStyle { Fur, Hoodie, Robot }
    public enum TypeFx { None, Bits, Sparks }

    public class Character
    {
        public string Id, Name;
        public EarStyle Ears = EarStyle.Cat;
        public HeadStyle Head = HeadStyle.Cat;
        public EyeStyle Eyes = EyeStyle.Cat;
        public NoseStyle Nose = NoseStyle.Cat;
        public MouthStyle Mouth = MouthStyle.Cat;
        public TailStyle Tail = TailStyle.Cat;
        public BodyStyle Body = BodyStyle.Fur;
        public TypeFx Fx = TypeFx.None;
        public bool Whiskers = true, Neon, Glitch, Float, EyePatches, ArmsAccent, PawsAccent;
        public Color Keyboard, Keycap, Glow, Mouse;
        public List<Coat> Palettes = new List<Coat>();

        static Color H(string s) { return Coat.H(s); }

        Character Desk(string kb, string cap, string glow, string mouse)
        {
            Keyboard = H(kb); Keycap = H(cap); Glow = H(glow); Mouse = H(mouse);
            return this;
        }

        // name, body, belly/face, accent(points), eye, nose, stripe, inner ear
        Character Pal(string name, string fur, string belly, string points, string eye, string nose, string stripe, string ear)
        {
            Coat c = new Coat();
            c.Name = name; c.CharacterId = Id;
            c.Fur = H(fur); c.Belly = H(belly); c.Points = H(points); c.Eye = H(eye); c.Nose = H(nose);
            c.Stripe = H(stripe); c.EarInner = H(ear); c.Patch1 = c.Fur; c.Patch2 = c.Fur;
            Palettes.Add(c);
            return this;
        }

        Character Last(Action<Coat> tweak) { tweak(Palettes[Palettes.Count - 1]); return this; }

        Character Variant(string id, string name)
        {
            Character variant = (Character)MemberwiseClone();
            variant.Id = id; variant.Name = name;
            variant.Palettes = new List<Coat>();
            foreach (Coat palette in Palettes)
            {
                Coat copy = palette.Clone();
                copy.CharacterId = id;
                variant.Palettes.Add(copy);
            }
            return variant;
        }

        static List<Character> all;

        public static List<Character> All()
        {
            if (all != null) return all;
            List<Character> l = new List<Character>();
            Character c;

            c = new Character(); c.Id = "cat"; c.Name = "Cat";
            c.Desk("#5E6A7D", "#F4F1EA", "#FFC94D", "#F4F1EA");
            foreach (Coat k in Coat.Presets()) { k.CharacterId = "cat"; c.Palettes.Add(k); }
            l.Add(c);

            c = new Character(); c.Id = "fox"; c.Name = "Fox";
            c.Ears = EarStyle.Fox; c.Tail = TailStyle.Bushy; c.PawsAccent = true;
            c.Desk("#4E5B4E", "#F6F1E7", "#FFB347", "#F6F1E7");
            c.Pal("Red Fox", "#E8762D", "#FFF6EC", "#3B2B27", "#E9A23B", "#2E2626", "#C85E1E", "#F7C9B5")
             .Pal("Arctic Fox", "#F2F4F7", "#FFFFFF", "#9AA6B2", "#6FA8DC", "#3A3A44", "#D5DAE0", "#F3C6CF")
             .Pal("Silver Fox", "#5D6168", "#E8EAED", "#1E2024", "#E3B341", "#1E1E22", "#44484E", "#B98A92");
            l.Add(c);

            c = new Character(); c.Id = "bunny"; c.Name = "Bunny";
            c.Ears = EarStyle.Bunny; c.Mouth = MouthStyle.Bunny; c.Tail = TailStyle.Puff;
            c.Desk("#F2B8C6", "#FFFFFF", "#FF8FB1", "#FFFFFF");
            c.Pal("Snowball", "#FBFBF8", "#FFFFFF", "#FBFBF8", "#7A4B33", "#F39AB0", "#E6E6E0", "#F6B9C6")
             .Pal("Cocoa", "#A57A57", "#F1E3D3", "#A57A57", "#3A2A20", "#E58F9A", "#8A6244", "#E9B7B5")
             .Pal("Dusty Gray", "#A3A7AE", "#E6E8EB", "#A3A7AE", "#2E2A2A", "#E39AA6", "#8A8E95", "#EEC0C8");
            l.Add(c);

            c = new Character(); c.Id = "panda"; c.Name = "Panda";
            c.Ears = EarStyle.Round; c.Whiskers = false; c.Tail = TailStyle.None; c.EyePatches = true;
            c.ArmsAccent = true; c.PawsAccent = true;
            c.Desk("#3D6B47", "#F3F7EE", "#9BE37A", "#F3F7EE");
            c.Pal("Giant Panda", "#FAFAF7", "#FFFFFF", "#2A2A2E", "#3A2B22", "#2A2A2E", "#E5E5E0", "#2A2A2E")
             .Pal("Brown Panda", "#F3EADF", "#FFF9F2", "#6B4B39", "#3A2B22", "#4A3428", "#E3D6C6", "#6B4B39");
            l.Add(c);

            c = new Character(); c.Id = "alien"; c.Name = "Alien";
            c.Head = HeadStyle.Alien; c.Ears = EarStyle.Antennae; c.Eyes = EyeStyle.Alien; c.Nose = NoseStyle.Dots;
            c.Mouth = MouthStyle.Small; c.Tail = TailStyle.None; c.Whiskers = false; c.Fx = TypeFx.Sparks;
            c.Desk("#2B2F45", "#E8ECFF", "#7CFF6B", "#E8ECFF");
            c.Pal("Little Green", "#8BD46B", "#C3EDAE", "#D8FF5C", "#0F1418", "#4E8A3A", "#6DB552", "#6DB552")
             .Pal("Grey Visitor", "#B9C0C8", "#DCE1E6", "#8FF0FF", "#0B0D10", "#6E7680", "#9AA3AD", "#9AA3AD")
             .Pal("Nebula Purple", "#A98BE6", "#D6C8F6", "#FF7AE6", "#120E1C", "#6A4FB0", "#8A6BD0", "#8A6BD0")
             .Pal("Ice Blue", "#7FD3F0", "#C8EEF9", "#FFFFFF", "#0A1620", "#3E93B3", "#5DB6D6", "#5DB6D6");
            l.Add(c);

            c = new Character(); c.Id = "robot"; c.Name = "Robot";
            c.Head = HeadStyle.Robot; c.Ears = EarStyle.Robot; c.Eyes = EyeStyle.Led; c.Nose = NoseStyle.None;
            c.Mouth = MouthStyle.Grille; c.Tail = TailStyle.Cable; c.Body = BodyStyle.Robot; c.Whiskers = false;
            c.Fx = TypeFx.Sparks;
            c.Desk("#2E3440", "#D8DEE9", "#4DF0FF", "#D8DEE9");
            c.Pal("Steel", "#B8C2CC", "#26303C", "#FF6B5B", "#4DF0FF", "#26303C", "#7D8894", "#7D8894")
             .Pal("Gold", "#E8C36A", "#2B2620", "#4DF0FF", "#FFE066", "#2B2620", "#B8923E", "#B8923E")
             .Pal("Snow Bot", "#F1F3F6", "#1E2733", "#FF5FA2", "#7CFFB2", "#1E2733", "#B7BFCA", "#B7BFCA")
             .Pal("Rusty", "#B8754A", "#2A1F1A", "#FFD24D", "#FF9E3D", "#2A1F1A", "#8A5232", "#8A5232");
            l.Add(c);

            c = new Character(); c.Id = "hacker"; c.Name = "Hacker";
            c.Head = HeadStyle.Hood; c.Ears = EarStyle.None; c.Eyes = EyeStyle.Glow; c.Nose = NoseStyle.None;
            c.Mouth = MouthStyle.None; c.Tail = TailStyle.None; c.Body = BodyStyle.Hoodie; c.Whiskers = false;
            c.Fx = TypeFx.Bits; c.Glitch = true; c.PawsAccent = true;
            c.Desk("#111417", "#1E242B", "#39FF7A", "#1E242B");
            c.Pal("Matrix", "#2A2E34", "#07090B", "#3D434B", "#39FF7A", "#07090B", "#1C1F23", "#1C1F23")
             .Pal("Red Team", "#2E2426", "#0B0809", "#4A3A3D", "#FF3B4E", "#0B0809", "#1F1819", "#1F1819")
             .Pal("Blue Team", "#222A38", "#070A10", "#384660", "#3BC8FF", "#070A10", "#161C26", "#161C26")
             .Pal("Phantom", "#2D2642", "#0A0812", "#453A66", "#C77DFF", "#0A0812", "#1E192D", "#1E192D");
            l.Add(c);

            c = new Character(); c.Id = "cybercat"; c.Name = "Cyber Cat";
            c.Neon = true; c.Glitch = true; c.Fx = TypeFx.Bits;
            c.Desk("#0D0F12", "#1A1E24", "#00FFC6", "#1A1E24");
            c.Pal("Neon Mint", "#111418", "#1A1F25", "#00FFC6", "#00FFC6", "#FF2BD6", "#FF2BD6", "#FF2BD6")
             .Last(delegate (Coat k) { k.UseStripes = true; })
             .Pal("Synthwave", "#160F24", "#231A3A", "#FF4FD8", "#FFE45C", "#2DE2FF", "#2DE2FF", "#2DE2FF")
             .Last(delegate (Coat k) { k.UseStripes = true; })
             .Pal("Cyber Yellow", "#121212", "#1D1D1D", "#FFE600", "#FFE600", "#00E5FF", "#00E5FF", "#00E5FF")
             .Last(delegate (Coat k) { k.UseStripes = true; });
            l.Add(c);

            c = new Character(); c.Id = "ghost"; c.Name = "Ghost";
            c.Head = HeadStyle.Ghost; c.Ears = EarStyle.None; c.Eyes = EyeStyle.Bead; c.Nose = NoseStyle.None;
            c.Mouth = MouthStyle.O; c.Tail = TailStyle.Wisp; c.Whiskers = false; c.Float = true;
            c.Desk("#3B3355", "#EDEAF7", "#B69CFF", "#EDEAF7");
            c.Pal("Boo", "#F6F7FF", "#F6F7FF", "#DADCF0", "#23222C", "#23222C", "#DADCF0", "#DADCF0")
             .Pal("Spooky Purple", "#CDB8FF", "#CDB8FF", "#A98EEA", "#1C1530", "#1C1530", "#A98EEA", "#A98EEA")
             .Pal("Mint Spirit", "#C8F7E6", "#C8F7E6", "#97E3C8", "#14302A", "#14302A", "#97E3C8", "#97E3C8");
            l.Add(c);

            l.Add(l.Find(delegate(Character ch) { return ch.Id == "cat"; }).Variant("nightcat", "Night Cat"));
            l.Add(l.Find(delegate(Character ch) { return ch.Id == "hacker"; }).Variant("wraith", "Wraith Hacker"));
            l.Add(l.Find(delegate(Character ch) { return ch.Id == "cybercat"; }).Variant("cyberstalker", "Cyber Stalker"));

            all = l;
            return all;
        }

        public static Character Get(string id)
        {
            foreach (Character c in All())
                if (string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)) return c;
            return All()[0];
        }
    }
}
