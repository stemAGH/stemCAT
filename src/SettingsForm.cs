// stemCAT - settings window
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace stemCAT
{
    public class SettingsForm : Form
    {
        readonly Settings s;
        readonly Action apply;
        readonly Func<bool> getStartup;
        readonly Action<bool> setStartup;
        readonly Action<string> snap;
        List<Coat> presets { get { return s.Character.Palettes; } }
        List<Coat> Customs()
        {
            List<Coat> l = new List<Coat>();
            foreach (Coat c in s.CustomCoats) if (c.CharacterId == s.Coat.CharacterId) l.Add(c);
            return l;
        }

        ComboBox coatBox, sizeBox, charBox, posBox;
        TextBox nameBox;
        CheckBox chkPoints, chkStripes, chkPatches, chkWhitePaws, chkClick, chkPrivacy, chkGreen, chkStartup, chkPops, chkFlip, chkLock;
        TrackBar opacity;
        Label opLabel;
        readonly List<KeyValuePair<Button, Func<Color>>> swatches = new List<KeyValuePair<Button, Func<Color>>>();
        bool loading;

        static readonly string[] SizeNames = { "Mini", "Small", "Normal", "Large", "Huge" };
        static readonly float[] SizeValues = { 0.6f, 0.8f, 1.0f, 1.3f, 1.7f };

        public SettingsForm(Settings settings, Action apply, Func<bool> getStartup, Action<bool> setStartup, Action<string> snap)
        {
            s = settings; this.apply = apply; this.getStartup = getStartup; this.setStartup = setStartup; this.snap = snap;
            Text = "stemCAT Settings";
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Font;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = true;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            Build();
            RefreshFromSettings();
        }

        // ----------------------------------------------------------- layout helpers
        static TableLayoutPanel Grid(int cols)
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.ColumnCount = cols; t.AutoSize = true; t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            t.Padding = new Padding(2); t.Margin = new Padding(0);
            return t;
        }

        static GroupBox Group(string title, Control inner)
        {
            GroupBox g = new GroupBox();
            g.Text = title; g.AutoSize = true; g.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            g.Padding = new Padding(8, 6, 8, 8); g.Margin = new Padding(0, 0, 0, 10); g.MinimumSize = new Size(556, 0);
            inner.Location = new Point(10, 22);
            g.Controls.Add(inner);
            return g;
        }

        static Label L(string text)
        {
            Label l = new Label(); l.Text = text; l.AutoSize = true; l.Anchor = AnchorStyles.Left; l.Margin = new Padding(3, 7, 3, 3);
            return l;
        }

        Button ColorButton(Func<Color> get, Action<Color> set)
        {
            Button b = new Button();
            b.Width = 56; b.Height = 24; b.FlatStyle = FlatStyle.Flat; b.Margin = new Padding(3, 3, 14, 3);
            b.Click += delegate
            {
                using (ColorDialog cd = new ColorDialog())
                {
                    cd.FullOpen = true; cd.Color = get();
                    if (cd.ShowDialog(this) == DialogResult.OK)
                    {
                        set(cd.Color);
                        Changed(true);
                    }
                }
            };
            swatches.Add(new KeyValuePair<Button, Func<Color>>(b, get));
            return b;
        }

        void AddColor(TableLayoutPanel t, string label, Func<Color> get, Action<Color> set)
        {
            t.Controls.Add(L(label));
            t.Controls.Add(ColorButton(get, set));
        }

        CheckBox Check(string text, EventHandler onChange)
        {
            CheckBox c = new CheckBox(); c.Text = text; c.AutoSize = true; c.Margin = new Padding(3, 4, 12, 4);
            c.CheckedChanged += delegate (object o, EventArgs e) { if (!loading) onChange(o, e); };
            return c;
        }

        // ----------------------------------------------------------- build
        void Build()
        {
            FlowLayoutPanel root = new FlowLayoutPanel();
            root.FlowDirection = FlowDirection.TopDown; root.WrapContents = false;
            root.AutoSize = true; root.AutoSizeMode = AutoSizeMode.GrowAndShrink; root.Padding = new Padding(12);

            // coat ---------------------------------------------------
            TableLayoutPanel coat = Grid(1);
            FlowLayoutPanel presetRow = new FlowLayoutPanel(); presetRow.AutoSize = true; presetRow.WrapContents = false;
            presetRow.Controls.Add(L("Character"));
            charBox = new ComboBox(); charBox.DropDownStyle = ComboBoxStyle.DropDownList; charBox.Width = 130;
            foreach (Character ch in Character.All()) charBox.Items.Add(ch.Name);
            charBox.SelectedIndexChanged += delegate
            {
                if (loading || charBox.SelectedIndex < 0) return;
                s.UseCharacter(Character.All()[charBox.SelectedIndex]);
                apply();
                RefreshFromSettings();
            };
            presetRow.Controls.Add(charBox);
            Label cl = L("Colours"); cl.Margin = new Padding(14, 7, 3, 3);
            presetRow.Controls.Add(cl);
            coatBox = new ComboBox(); coatBox.DropDownStyle = ComboBoxStyle.DropDownList; coatBox.Width = 170;
            coatBox.SelectedIndexChanged += delegate { if (!loading) PickCoat(); };
            presetRow.Controls.Add(coatBox);
            coat.Controls.Add(presetRow);

            TableLayoutPanel colors = Grid(6);
            AddColor(colors, "Body", delegate { return s.Coat.Fur; }, delegate (Color c) { s.Coat.Fur = c; });
            AddColor(colors, "Belly / face", delegate { return s.Coat.Belly; }, delegate (Color c) { s.Coat.Belly = c; });
            AddColor(colors, "Eyes", delegate { return s.Coat.Eye; }, delegate (Color c) { s.Coat.Eye = c; });
            AddColor(colors, "Accent", delegate { return s.Coat.Points; }, delegate (Color c) { s.Coat.Points = c; });
            AddColor(colors, "Stripes", delegate { return s.Coat.Stripe; }, delegate (Color c) { s.Coat.Stripe = c; });
            AddColor(colors, "Nose / detail", delegate { return s.Coat.Nose; }, delegate (Color c) { s.Coat.Nose = c; });
            AddColor(colors, "Patch 1", delegate { return s.Coat.Patch1; }, delegate (Color c) { s.Coat.Patch1 = c; });
            AddColor(colors, "Patch 2", delegate { return s.Coat.Patch2; }, delegate (Color c) { s.Coat.Patch2 = c; });
            AddColor(colors, "Inner ears / lines", delegate { return s.Coat.EarInner; }, delegate (Color c) { s.Coat.EarInner = c; });
            coat.Controls.Add(colors);

            FlowLayoutPanel flags = new FlowLayoutPanel(); flags.AutoSize = true; flags.WrapContents = false;
            chkPoints = Check("Colour points", delegate { s.Coat.UsePoints = chkPoints.Checked; Changed(true); });
            chkStripes = Check("Stripes", delegate { s.Coat.UseStripes = chkStripes.Checked; Changed(true); });
            chkPatches = Check("Patches", delegate { s.Coat.UsePatches = chkPatches.Checked; Changed(true); });
            chkWhitePaws = Check("White paws", delegate { s.Coat.WhitePaws = chkWhitePaws.Checked; Changed(true); });
            flags.Controls.AddRange(new Control[] { chkPoints, chkStripes, chkPatches, chkWhitePaws });
            coat.Controls.Add(flags);

            FlowLayoutPanel saveRow = new FlowLayoutPanel(); saveRow.AutoSize = true; saveRow.WrapContents = false;
            saveRow.Controls.Add(L("Save colours as"));
            nameBox = new TextBox(); nameBox.Width = 150;
            Button save = new Button(); save.Text = "Save"; save.AutoSize = true;
            save.Click += delegate { SaveCustom(); };
            Button del = new Button(); del.Text = "Delete"; del.AutoSize = true;
            del.Click += delegate { DeleteCustom(); };
            saveRow.Controls.AddRange(new Control[] { nameBox, save, del });
            coat.Controls.Add(saveRow);
            root.Controls.Add(Group("Character", coat));

            // desk ---------------------------------------------------
            TableLayoutPanel desk = Grid(8);
            AddColor(desk, "Keyboard", delegate { return s.KeyboardColor; }, delegate (Color c) { s.KeyboardColor = c; });
            AddColor(desk, "Keys", delegate { return s.KeycapColor; }, delegate (Color c) { s.KeycapColor = c; });
            AddColor(desk, "Key light", delegate { return s.GlowColor; }, delegate (Color c) { s.GlowColor = c; });
            AddColor(desk, "Mouse", delegate { return s.MouseColor; }, delegate (Color c) { s.MouseColor = c; });
            root.Controls.Add(Group("Keyboard && mouse", desk));

            // window -------------------------------------------------
            TableLayoutPanel win = Grid(1);
            FlowLayoutPanel row1 = new FlowLayoutPanel(); row1.AutoSize = true; row1.WrapContents = false;
            row1.Controls.Add(L("Size"));
            sizeBox = new ComboBox(); sizeBox.DropDownStyle = ComboBoxStyle.DropDownList; sizeBox.Width = 90;
            sizeBox.Items.AddRange(SizeNames);
            sizeBox.SelectedIndexChanged += delegate
            {
                if (loading || sizeBox.SelectedIndex < 0) return;
                s.Scale = SizeValues[sizeBox.SelectedIndex]; Changed(false);
            };
            row1.Controls.Add(sizeBox);
            Label opl = L("Opacity"); opl.Margin = new Padding(20, 7, 3, 3);
            row1.Controls.Add(opl);
            opacity = new TrackBar(); opacity.Minimum = 15; opacity.Maximum = 100; opacity.TickFrequency = 10; opacity.Width = 170;
            opacity.ValueChanged += delegate
            {
                opLabel.Text = opacity.Value + "%";
                if (loading) return;
                s.Opacity = opacity.Value; Changed(false);
            };
            row1.Controls.Add(opacity);
            opLabel = L("100%");
            row1.Controls.Add(opLabel);
            win.Controls.Add(row1);

            FlowLayoutPanel row2 = new FlowLayoutPanel(); row2.AutoSize = true; row2.WrapContents = false;
            row2.Controls.Add(L("Move to"));
            posBox = new ComboBox(); posBox.DropDownStyle = ComboBoxStyle.DropDownList; posBox.Width = 130;
            posBox.Items.AddRange(CatForm.PositionNames);
            if (Screen.AllScreens.Length > 1) posBox.Items.Add("Next monitor");
            posBox.SelectedIndexChanged += delegate
            {
                if (loading || posBox.SelectedIndex < 0) return;
                snap((string)posBox.SelectedItem);
                loading = true; posBox.SelectedIndex = -1; loading = false;
            };
            row2.Controls.Add(posBox);
            chkLock = Check("Lock position (no dragging)", delegate { s.LockPosition = chkLock.Checked; Changed(false); });
            chkLock.Margin = new Padding(16, 6, 3, 3);
            row2.Controls.Add(chkLock);
            win.Controls.Add(row2);

            chkClick = Check("Click-through lock  (Ctrl+Alt+L)", delegate { s.ClickThrough = chkClick.Checked; Changed(false); });
            chkPrivacy = Check("Privacy mode: show only left / right paw  (Ctrl+Alt+P)", delegate { s.Privacy = chkPrivacy.Checked; Changed(false); });
            chkGreen = Check("Green screen background (for OBS / streaming chroma key)", delegate { s.GreenScreen = chkGreen.Checked; Changed(false); });
            chkStartup = Check("Start stemCAT when Windows starts", delegate { setStartup(chkStartup.Checked); });
            chkPops = Check("Key pop-ups: show a little keycap for each key you press", delegate { s.KeyPops = chkPops.Checked; Changed(false); });
            chkFlip = Check("Keyboard faces the character (seen from the front)", delegate { s.FlipKeyboard = chkFlip.Checked; Changed(false); });
            win.Controls.Add(chkPops); win.Controls.Add(chkFlip);
            win.Controls.Add(chkClick); win.Controls.Add(chkPrivacy); win.Controls.Add(chkGreen); win.Controls.Add(chkStartup);
            root.Controls.Add(Group("Desk && window", win));

            Label priv = new Label();
            priv.AutoSize = true; priv.MaximumSize = new Size(560, 0); priv.ForeColor = Color.DimGray; priv.Margin = new Padding(3, 0, 3, 10);
            priv.Text = "Privacy: stemCAT only remembers which keys are held down right now, in memory, so the cat can press them. " +
                        "Each key is forgotten the moment you release it. Nothing is logged, saved or sent anywhere, and stemCAT never uses the internet.\n" +
                        "Tips: drag the cat to move it, press and hold its head to pet it, right-click it (or the tray icon) for the menu.";
            root.Controls.Add(priv);

            Button close = new Button(); close.Text = "Close"; close.AutoSize = true; close.Anchor = AnchorStyles.Right;
            close.Click += delegate { Close(); };
            root.Controls.Add(close);
            CancelButton = close;

            Controls.Add(root);
        }

        // ----------------------------------------------------------- state
        public void RefreshFromSettings()
        {
            loading = true;
            List<Character> chars = Character.All();
            for (int i = 0; i < chars.Count; i++) if (chars[i].Id == s.Coat.CharacterId) charBox.SelectedIndex = i;
            List<Coat> customs = Customs();
            coatBox.Items.Clear();
            foreach (Coat c in presets) coatBox.Items.Add(c.Name);
            foreach (Coat c in customs) coatBox.Items.Add("\u2605 " + c.Name);
            int idx = -1;
            for (int i = 0; i < presets.Count; i++) if (presets[i].Name == s.Coat.Name) idx = i;
            for (int i = 0; i < customs.Count; i++) if (customs[i].Name == s.Coat.Name) idx = presets.Count + i;
            coatBox.SelectedIndex = idx;
            nameBox.Text = idx >= presets.Count ? s.Coat.Name : "";

            chkPoints.Checked = s.Coat.UsePoints; chkStripes.Checked = s.Coat.UseStripes;
            chkPatches.Checked = s.Coat.UsePatches; chkWhitePaws.Checked = s.Coat.WhitePaws;
            int si = 2;
            for (int i = 0; i < SizeValues.Length; i++) if (Math.Abs(SizeValues[i] - s.Scale) < 0.01f) si = i;
            sizeBox.SelectedIndex = si;
            opacity.Value = Math.Max(opacity.Minimum, Math.Min(opacity.Maximum, s.Opacity));
            opLabel.Text = opacity.Value + "%";
            chkClick.Checked = s.ClickThrough; chkPrivacy.Checked = s.Privacy; chkGreen.Checked = s.GreenScreen;
            chkStartup.Checked = getStartup();
            chkPops.Checked = s.KeyPops; chkFlip.Checked = s.FlipKeyboard; chkLock.Checked = s.LockPosition;
            foreach (KeyValuePair<Button, Func<Color>> kv in swatches) kv.Key.BackColor = kv.Value();
            loading = false;
        }

        void Changed(bool coatEdited)
        {
            if (coatEdited && coatBox.SelectedIndex >= 0 && coatBox.SelectedIndex < presets.Count)
            {
                // editing a preset makes it your own mix
                s.Coat.Name = "Custom";
            }
            apply();
            if (coatEdited)
            {
                foreach (KeyValuePair<Button, Func<Color>> kv in swatches) kv.Key.BackColor = kv.Value();
                if (s.Coat.Name == "Custom") { loading = true; coatBox.SelectedIndex = -1; loading = false; }
            }
        }

        void PickCoat()
        {
            int i = coatBox.SelectedIndex;
            if (i < 0) return;
            s.Coat = i < presets.Count ? presets[i].Clone() : Customs()[i - presets.Count].Clone();
            apply();
            RefreshFromSettings();
        }

        void SaveCustom()
        {
            string name = nameBox.Text.Trim().Replace("|", "/");
            if (name.Length == 0)
            {
                MessageBox.Show(this, "Type a name for your colours first.", "stemCAT");
                nameBox.Focus();
                return;
            }
            foreach (Coat p in presets)
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "That name is used by a built-in colour set. Pick another one.", "stemCAT");
                    return;
                }
            Coat c = s.Coat.Clone(); c.Name = name;
            int at = s.CustomCoats.FindIndex(delegate (Coat x) { return x.CharacterId == c.CharacterId && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase); });
            if (at >= 0) s.CustomCoats[at] = c; else s.CustomCoats.Add(c);
            s.Coat = c.Clone();
            apply();
            RefreshFromSettings();
        }

        void DeleteCustom()
        {
            int at = s.CustomCoats.FindIndex(delegate (Coat x) { return x.CharacterId == s.Coat.CharacterId && x.Name == s.Coat.Name; });
            if (at < 0)
            {
                MessageBox.Show(this, "Pick one of your saved colour sets (marked \u2605) to delete it.", "stemCAT");
                return;
            }
            s.CustomCoats.RemoveAt(at);
            s.Coat = s.Character.Palettes[0].Clone();
            apply();
            RefreshFromSettings();
        }
    }
}
