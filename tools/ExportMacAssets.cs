// Export the existing vector characters as transparent animation atlases for macOS.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Web.Script.Serialization;
using stemCAT;
class ExportMacAssets
{
    static int Main(string[] args)
    {
        if (args.Length != 1) return 2;
        string dir = args[0]; Directory.CreateDirectory(dir);
        var characters = new List<object>();
        foreach (Character ch in Character.All())
        {
            var palettes = new List<object>();
            for (int palette = 0; palette < ch.Palettes.Count; palette++)
            {
                string file = ch.Id + "-" + palette + ".png";
                using (Bitmap sheet = new Bitmap(2560, 1920, PixelFormat.Format32bppArgb))
                using (Graphics g = Graphics.FromImage(sheet))
                {
                    g.Clear(Color.Transparent);
                    for (int state = 0; state < 8; state++)
                    {
                        CatScene scene = new CatScene(ch.Palettes[palette].Clone());
                        scene.KeyboardColor = ch.Keyboard; scene.KeycapColor = ch.Keycap;
                        scene.GlowColor = ch.Glow; scene.MouseColor = ch.Mouse;
                        scene.KeyPops = false; scene.Cursor = new PointF(160, 70);
                        // Generic left/right activity, never actual key labels.
                        if (state == 1 || state == 3) scene.KeyDown(0x4C);
                        if (state == 2 || state == 3) scene.KeyDown(0x41);
                        if (state == 4) { scene.MouseMoved(); scene.MouseButton(0, true); }
                        if (state == 5) scene.SetIdleFor(20);
                        if (state == 6) scene.SetIdleFor(60);
                        if (state == 7) { scene.Petting = true; scene.PetPoint = new PointF(180, 60); }
                        double t = 0;
                        for (int frame = 0; frame < 8; frame++)
                        {
                            int steps = frame == 0 ? 60 : 8;
                            for (int n = 0; n < steps; n++) { t += 1.0 / 30; scene.Update(t); }
                            var save = g.Save(); g.TranslateTransform(frame * 320, state * 240);
                            scene.Draw(g); g.Restore(save);
                        }
                    }
                    sheet.Save(Path.Combine(dir, file), ImageFormat.Png);
                }
                palettes.Add(new { name = ch.Palettes[palette].Name, file = file });
            }
            characters.Add(new { id = ch.Id, name = ch.Name, palettes = palettes });
            Console.WriteLine("Exported " + ch.Name);
        }
        File.WriteAllText(Path.Combine(dir, "characters.json"), new JavaScriptSerializer().Serialize(characters));
        using (Bitmap icon = new CatScene(Character.All()[0].Palettes[0]).RenderIcon(512))
            icon.Save(Path.Combine(dir, "icon.png"), ImageFormat.Png);
        return 0;
    }
}
