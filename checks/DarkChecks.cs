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
  string[] original = { "cat", "fox", "bunny", "panda", "alien", "robot", "hacker", "cybercat", "ghost", "nightcat", "wraith", "cyberstalker" };
  string[] cozy = { "cozyginger", "cozytuxedo", "cozycalico", "cozycloud", "cozyblue", "cozycocoa" };
  if (Character.All().Count != 18) throw new Exception("Expected eighteen characters");
  foreach (string id in original)
   if (Character.Get(id).Id != id || Character.Get(id).Cozy) throw new Exception("Original roster changed: " + id);
  foreach (string id in cozy) {
   Character ch = Character.Get(id);
   if (ch.Id != id || !ch.Cozy) throw new Exception("Missing cozy character: " + id);
   CatScene s = new CatScene(ch.Palettes[0].Clone());
   if (!s.HitHead(new PointF(160,60)) || s.HitHead(new PointF(160,200))) throw new Exception("Cozy hit area: " + id);
   s.KeyDown(65); s.Update(0.1);
   if (s.HeldKeys().Count != 1) throw new Exception("Cozy input: " + id);
   s.KeyUp(65);
   if (s.HeldKeys().Count != 0) throw new Exception("Cozy key release: " + id);
   s.SetIdleFor(60); s.Update(0.2);
   if (s.Mode != CatMode.Sleep) throw new Exception("Cozy sleep: " + id);
   s.KeyDown(65); s.Update(0.3); s.KeyUp(65);
   if (s.Mode != CatMode.Awake) throw new Exception("Cozy wake: " + id);
   using (Bitmap b = new Bitmap(320,240)) using (Graphics g = Graphics.FromImage(b)) {
    s.Draw(g);
    if (b.GetPixel(0,239).A != 0 || b.GetPixel(160,65).A == 0) throw new Exception("Cozy render/alpha: " + id);
   }
   s.Petting = true; s.Update(0.4);
   using (Bitmap icon = s.RenderIcon(64)) if (icon.Width != 64) throw new Exception("Cozy icon: " + id);
  }
  foreach (Character ch in Character.All()) foreach (Coat c in ch.Palettes)
   if (c.CharacterId != ch.Id || Coat.Deserialize(c.Serialize()).CharacterId != ch.Id) throw new Exception("Palette identity: " + ch.Id);
  if (Object.ReferenceEquals(Character.Get("cat").Palettes[0], Character.Get("nightcat").Palettes[0])) throw new Exception("Shared variant palette");
  Console.WriteLine("Character rendering, hit areas, transparency and input checks passed.");
 }
}
