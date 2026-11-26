using System;
using System.Drawing;
using stemCAT;
class DarkChecks {
 static void Main() {
  foreach (string id in new string[] { "nightcat", "wraith", "cyberstalker" }) {
   CatScene s = new CatScene(Character.Get(id).Palettes[0]);
   if (!s.HitHead(new PointF(160, 60)) || s.HitHead(new PointF(160, 135))) throw new Exception("Head hit area: " + id);
   s.KeyDown(65); s.Update(0.1);
   Color cap = s.KeycapColor;
   using (Bitmap b = new Bitmap(320,240)) using (Graphics g = Graphics.FromImage(b)) {
    s.Draw(g);
    if (b.GetPixel(0,239).A != 0) throw new Exception("Lost transparency");
   }
   if (cap != s.KeycapColor || s.HeldKeys().Count != 1) throw new Exception("Drawing changed input or palette");
   s.KeyUp(65);
   if (s.HeldKeys().Count != 0) throw new Exception("Key release");
  }
  if (Character.All().Count != 12 || Character.Get("cat").Name != "Cat" || Character.Get("hacker").Name != "Hacker") throw new Exception("Original roster changed");
  foreach (Character ch in Character.All()) foreach (Coat c in ch.Palettes)
   if (c.CharacterId != ch.Id || Coat.Deserialize(c.Serialize()).CharacterId != ch.Id) throw new Exception("Palette identity: " + ch.Id);
  if (Object.ReferenceEquals(Character.Get("cat").Palettes[0], Character.Get("nightcat").Palettes[0])) throw new Exception("Shared variant palette");
  Console.WriteLine("Character rendering, hit areas, transparency and input checks passed.");
 }
}
