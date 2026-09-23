namespace ClockScreensaver;

public class SettingsForm : Form
{
    private readonly Settings _s;

    // Controls
    private ComboBox _cmbPosition = new();
    private ComboBox _cmbFont = new();
    private ComboBox _cmbWeight = new();
    private TrackBar _trkSize = new();
    private TrackBar _trkBrightness = new();
    private TrackBar _trkMargin = new();
    private CheckBox _chk24h = new();
    private CheckBox _chkSeconds = new();
    private CheckBox _chkDate = new();
    private CheckBox _chkRotate = new();

    private CheckBox _chkWeather = new();
    private ComboBox _cmbUnit = new();
    private CheckBox _chkCondName = new();
    private RadioButton _radAuto = new();
    private RadioButton _radCity = new();
    private TextBox _txtCity = new();
    private ComboBox _cmbPreview = new();

    private TextBox _txtPicPath = new();
    private Button _btnPicBrowse = new();
    private Button _btnPicClear = new();
    private ComboBox _cmbFit = new();
    private TrackBar _trkPicBright = new();

    private CheckBox _chkEffects = new();
    private ComboBox _cmbTod = new();
    private TrackBar _trkStrength = new();

    public SettingsForm(Settings s)
    {
        _s = s;
        Text = "Clock Screensaver Settings";
        Size = new Size(420, 740);
        MinimumSize = new Size(420, 600);
        MaximumSize = new Size(420, 900);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(22, 22, 22);
        ForeColor = Color.FromArgb(232, 232, 232);
        Font = new Font("Segoe UI", 9);
        AutoScroll = true;

        BuildUI();
        LoadValues();
    }

    private void BuildUI()
    {
        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(22, 22, 22) };
        Controls.Add(panel);

        int y = 12;
        int lw = 380;

        void Head(string text) { panel.Controls.Add(new Label { Text = text, Left = 12, Top = y, Width = lw, ForeColor = Color.FromArgb(232, 232, 232), Font = new Font("Segoe UI", 10, FontStyle.Bold) }); y += 26; }
        void Sep() { panel.Controls.Add(new Label { Left = 12, Top = y, Width = lw, Height = 1, BackColor = Color.FromArgb(44, 44, 44) }); y += 10; }
        void Lbl(string t) { panel.Controls.Add(new Label { Text = t, Left = 12, Top = y, Width = lw, ForeColor = Color.FromArgb(138, 138, 138) }); y += 18; }

        Control AddControl(Control c, int left = 12, int? width = null)
        {
            c.Left = left; c.Top = y; if (width.HasValue) c.Width = width.Value;
            c.BackColor = Color.FromArgb(12, 12, 12);
            c.ForeColor = Color.FromArgb(232, 232, 232);
            if (c is ComboBox cb) { cb.DropDownStyle = ComboBoxStyle.DropDownList; cb.FlatStyle = FlatStyle.Flat; }
            if (c is CheckBox chk) { chk.BackColor = Color.FromArgb(22, 22, 22); chk.AutoSize = false; chk.Width = lw; chk.Height = 22; }
            if (c is TrackBar trk) { trk.BackColor = Color.FromArgb(22, 22, 22); }
            panel.Controls.Add(c);
            return c;
        }

        void Gap(int px = 10) => y += px;

        // ---- Clock ----
        Head("Clock");

        Lbl("Position");
        AddControl(_cmbPosition, width: lw); _cmbPosition.Height = 28;
        _cmbPosition.Items.AddRange(new[] { "Bottom Right", "Bottom Left", "Top Left", "Top Right", "Center" });
        y += 32; Gap(6);

        var pairPanel = new Panel { Left = 12, Top = y, Width = lw, Height = 60, BackColor = Color.FromArgb(22, 22, 22) };
        panel.Controls.Add(pairPanel);
        var lblFont = new Label { Text = "Font", Left = 0, Top = 0, Width = 180, ForeColor = Color.FromArgb(138, 138, 138) };
        var lblWeight = new Label { Text = "Weight", Left = 196, Top = 0, Width = 180, ForeColor = Color.FromArgb(138, 138, 138) };
        _cmbFont.Left = 0; _cmbFont.Top = 18; _cmbFont.Width = 182; _cmbFont.DropDownStyle = ComboBoxStyle.DropDownList; _cmbFont.FlatStyle = FlatStyle.Flat;
        _cmbFont.BackColor = Color.FromArgb(12, 12, 12); _cmbFont.ForeColor = Color.FromArgb(232, 232, 232);
        _cmbFont.Items.AddRange(new[] { "Plex Mono", "Space Grotesk", "Fraunces", "Major Mono Display", "Syne", "System" });
        _cmbWeight.Left = 196; _cmbWeight.Top = 18; _cmbWeight.Width = 180; _cmbWeight.DropDownStyle = ComboBoxStyle.DropDownList; _cmbWeight.FlatStyle = FlatStyle.Flat;
        _cmbWeight.BackColor = Color.FromArgb(12, 12, 12); _cmbWeight.ForeColor = Color.FromArgb(232, 232, 232);
        _cmbWeight.Items.AddRange(new[] { "Light", "Bold" });
        pairPanel.Controls.AddRange(new Control[] { lblFont, lblWeight, _cmbFont, _cmbWeight });
        y += 68;

        Lbl("Size");
        _trkSize.Left = 12; _trkSize.Top = y; _trkSize.Width = lw; _trkSize.Height = 32;
        _trkSize.Minimum = 16; _trkSize.Maximum = 240; _trkSize.TickFrequency = 8;
        _trkSize.BackColor = Color.FromArgb(22, 22, 22); panel.Controls.Add(_trkSize); y += 36;

        Lbl("Brightness");
        _trkBrightness.Left = 12; _trkBrightness.Top = y; _trkBrightness.Width = lw; _trkBrightness.Height = 32;
        _trkBrightness.Minimum = 10; _trkBrightness.Maximum = 100; _trkBrightness.TickFrequency = 5;
        _trkBrightness.BackColor = Color.FromArgb(22, 22, 22); panel.Controls.Add(_trkBrightness); y += 36;

        Lbl("Distance from edge");
        _trkMargin.Left = 12; _trkMargin.Top = y; _trkMargin.Width = lw; _trkMargin.Height = 32;
        _trkMargin.Minimum = 8; _trkMargin.Maximum = 200; _trkMargin.TickFrequency = 8;
        _trkMargin.BackColor = Color.FromArgb(22, 22, 22); panel.Controls.Add(_trkMargin); y += 36;

        AddControl(_chk24h, width: lw); _chk24h.Text = "24-hour time"; y += 24;
        AddControl(_chkSeconds, width: lw); _chkSeconds.Text = "Show seconds"; y += 24;
        AddControl(_chkDate, width: lw); _chkDate.Text = "Show date"; y += 24;
        AddControl(_chkRotate, width: lw); _chkRotate.Text = "Move to next corner every 10 minutes"; y += 30;

        // ---- Weather ----
        Sep(); Head("Weather");
        AddControl(_chkWeather, width: lw); _chkWeather.Text = "Show weather"; y += 28;

        var wxPair = new Panel { Left = 12, Top = y, Width = lw, Height = 60, BackColor = Color.FromArgb(22, 22, 22) };
        panel.Controls.Add(wxPair);
        var lblUnit = new Label { Text = "Units", Left = 0, Top = 0, Width = 180, ForeColor = Color.FromArgb(138, 138, 138) };
        var lblPrev = new Label { Text = "Preview condition", Left = 196, Top = 0, Width = 180, ForeColor = Color.FromArgb(138, 138, 138) };
        _cmbUnit.Left = 0; _cmbUnit.Top = 18; _cmbUnit.Width = 182; _cmbUnit.DropDownStyle = ComboBoxStyle.DropDownList; _cmbUnit.FlatStyle = FlatStyle.Flat;
        _cmbUnit.BackColor = Color.FromArgb(12, 12, 12); _cmbUnit.ForeColor = Color.FromArgb(232, 232, 232);
        _cmbUnit.Items.AddRange(new[] { "°C", "°F" });
        _cmbPreview.Left = 196; _cmbPreview.Top = 18; _cmbPreview.Width = 180; _cmbPreview.DropDownStyle = ComboBoxStyle.DropDownList; _cmbPreview.FlatStyle = FlatStyle.Flat;
        _cmbPreview.BackColor = Color.FromArgb(12, 12, 12); _cmbPreview.ForeColor = Color.FromArgb(232, 232, 232);
        _cmbPreview.Items.AddRange(new[] { "Automatic", "Clear", "Partly cloudy", "Cloudy", "Rain", "Snow", "Thunderstorm" });
        wxPair.Controls.AddRange(new Control[] { lblUnit, lblPrev, _cmbUnit, _cmbPreview });
        y += 68;

        AddControl(_chkCondName, width: lw); _chkCondName.Text = "Show condition name"; y += 28;

        Lbl("Location");
        _radAuto.Left = 12; _radAuto.Top = y; _radAuto.Width = 182; _radAuto.Text = "Automatic (IP-based)";
        _radAuto.BackColor = Color.FromArgb(22, 22, 22); _radAuto.ForeColor = Color.FromArgb(232, 232, 232); panel.Controls.Add(_radAuto);
        _radCity.Left = 200; _radCity.Top = y; _radCity.Width = 180; _radCity.Text = "City";
        _radCity.BackColor = Color.FromArgb(22, 22, 22); _radCity.ForeColor = Color.FromArgb(232, 232, 232); panel.Controls.Add(_radCity);
        y += 26;
        _txtCity.Left = 12; _txtCity.Top = y; _txtCity.Width = lw;
        _txtCity.BackColor = Color.FromArgb(12, 12, 12); _txtCity.ForeColor = Color.FromArgb(232, 232, 232); _txtCity.BorderStyle = BorderStyle.FixedSingle;
        panel.Controls.Add(_txtCity); y += 32;

        // ---- Picture ----
        Sep(); Head("Picture");
        Lbl("Image file");
        var picRow = new Panel { Left = 12, Top = y, Width = lw, Height = 28, BackColor = Color.FromArgb(22, 22, 22) };
        panel.Controls.Add(picRow);
        _txtPicPath.Dock = DockStyle.None; _txtPicPath.Left = 0; _txtPicPath.Top = 2; _txtPicPath.Width = 250; _txtPicPath.ReadOnly = true;
        _txtPicPath.BackColor = Color.FromArgb(12, 12, 12); _txtPicPath.ForeColor = Color.FromArgb(138, 138, 138); _txtPicPath.BorderStyle = BorderStyle.FixedSingle;
        _btnPicBrowse.Left = 256; _btnPicBrowse.Top = 0; _btnPicBrowse.Width = 60; _btnPicBrowse.Height = 26; _btnPicBrowse.Text = "Browse";
        _btnPicBrowse.BackColor = Color.FromArgb(12, 12, 12); _btnPicBrowse.ForeColor = Color.FromArgb(232, 232, 232); _btnPicBrowse.FlatStyle = FlatStyle.Flat;
        _btnPicClear.Left = 320; _btnPicClear.Top = 0; _btnPicClear.Width = 60; _btnPicClear.Height = 26; _btnPicClear.Text = "Clear";
        _btnPicClear.BackColor = Color.FromArgb(12, 12, 12); _btnPicClear.ForeColor = Color.FromArgb(232, 232, 232); _btnPicClear.FlatStyle = FlatStyle.Flat;
        picRow.Controls.AddRange(new Control[] { _txtPicPath, _btnPicBrowse, _btnPicClear });
        y += 36;

        Lbl("Fit");
        AddControl(_cmbFit, width: lw); _cmbFit.Height = 28;
        _cmbFit.Items.AddRange(new[] { "Fill screen (cover)", "Show whole picture (contain)" });
        y += 32; Gap(4);

        Lbl("Picture brightness");
        _trkPicBright.Left = 12; _trkPicBright.Top = y; _trkPicBright.Width = lw; _trkPicBright.Height = 32;
        _trkPicBright.Minimum = 5; _trkPicBright.Maximum = 100; _trkPicBright.TickFrequency = 5;
        _trkPicBright.BackColor = Color.FromArgb(22, 22, 22); panel.Controls.Add(_trkPicBright); y += 36;

        // ---- Effects ----
        Sep(); Head("Effects");
        AddControl(_chkEffects, width: lw); _chkEffects.Text = "Weather and night effects"; y += 28;

        Lbl("Time of day");
        AddControl(_cmbTod, width: lw); _cmbTod.Height = 28;
        _cmbTod.Items.AddRange(new[] { "Automatic (use real sunrise/sunset)", "Always day", "Always night" });
        y += 32; Gap(4);

        Lbl("Effect strength");
        _trkStrength.Left = 12; _trkStrength.Top = y; _trkStrength.Width = lw; _trkStrength.Height = 32;
        _trkStrength.Minimum = 10; _trkStrength.Maximum = 100; _trkStrength.TickFrequency = 5;
        _trkStrength.BackColor = Color.FromArgb(22, 22, 22); panel.Controls.Add(_trkStrength); y += 36;

        // ---- Buttons ----
        Sep(); Gap(4);
        var btnOK = new Button { Left = 12, Top = y, Width = 90, Height = 30, Text = "OK", FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Left = 108, Top = y, Width = 90, Height = 30, Text = "Cancel", FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };
        var btnReset = new Button { Left = 204, Top = y, Width = 90, Height = 30, Text = "Reset", FlatStyle = FlatStyle.Flat };
        foreach (var b in new[] { btnOK, btnCancel, btnReset })
        {
            b.BackColor = Color.FromArgb(12, 12, 12); b.ForeColor = Color.FromArgb(232, 232, 232);
            panel.Controls.Add(b);
        }
        y += 40;
        AcceptButton = btnOK; CancelButton = btnCancel;

        // Events
        btnOK.Click += (_, _) => { SaveValues(); _s.Save(); Close(); };
        btnReset.Click += (_, _) => { ResetDefaults(); LoadValues(); };
        _btnPicBrowse.Click += BrowsePicture;
        _btnPicClear.Click += (_, _) => { _s.PicturePath = ""; _txtPicPath.Text = ""; };
        _radCity.CheckedChanged += (_, _) => _txtCity.Enabled = _radCity.Checked;

        panel.Height = y + 20;
    }

    private void LoadValues()
    {
        _cmbPosition.SelectedIndex = _s.Position switch
        {
            ClockPosition.BottomRight => 0, ClockPosition.BottomLeft => 1,
            ClockPosition.TopLeft => 2, ClockPosition.TopRight => 3,
            ClockPosition.Center => 4, _ => 0
        };
        _cmbFont.SelectedIndex = (int)_s.Font;
        _cmbWeight.SelectedIndex = _s.Weight == ClockWeight.Light ? 0 : 1;
        _trkSize.Value = Math.Clamp(_s.Size, 16, 240);
        _trkBrightness.Value = Math.Clamp(_s.Brightness, 10, 100);
        _trkMargin.Value = Math.Clamp(_s.Margin, 8, 200);
        _chk24h.Checked = _s.Hours24;
        _chkSeconds.Checked = _s.ShowSeconds;
        _chkDate.Checked = _s.ShowDate;
        _chkRotate.Checked = _s.Rotate;

        _chkWeather.Checked = _s.WeatherOn;
        _cmbUnit.SelectedIndex = _s.Unit == WeatherUnit.C ? 0 : 1;
        _chkCondName.Checked = _s.ShowConditionName;
        _radAuto.Checked = _s.LocationAuto;
        _radCity.Checked = !_s.LocationAuto;
        _txtCity.Text = _s.CityName;
        _txtCity.Enabled = !_s.LocationAuto;
        _cmbPreview.SelectedIndex = (int)_s.PreviewCondition;

        _txtPicPath.Text = _s.PicturePath;
        _cmbFit.SelectedIndex = _s.PictureFit == PictureFit.Cover ? 0 : 1;
        _trkPicBright.Value = Math.Clamp(_s.PictureBrightness, 5, 100);

        _chkEffects.Checked = _s.EffectsOn;
        _cmbTod.SelectedIndex = (int)_s.TimeOfDay;
        _trkStrength.Value = Math.Clamp(_s.EffectsStrength, 10, 100);
    }

    private void SaveValues()
    {
        _s.Position = _cmbPosition.SelectedIndex switch
        {
            0 => ClockPosition.BottomRight, 1 => ClockPosition.BottomLeft,
            2 => ClockPosition.TopLeft, 3 => ClockPosition.TopRight,
            4 => ClockPosition.Center, _ => ClockPosition.BottomRight
        };
        _s.Font = (ClockFont)_cmbFont.SelectedIndex;
        _s.Weight = _cmbWeight.SelectedIndex == 0 ? ClockWeight.Light : ClockWeight.Bold;
        _s.Size = _trkSize.Value;
        _s.Brightness = _trkBrightness.Value;
        _s.Margin = _trkMargin.Value;
        _s.Hours24 = _chk24h.Checked;
        _s.ShowSeconds = _chkSeconds.Checked;
        _s.ShowDate = _chkDate.Checked;
        _s.Rotate = _chkRotate.Checked;

        _s.WeatherOn = _chkWeather.Checked;
        _s.Unit = _cmbUnit.SelectedIndex == 0 ? WeatherUnit.C : WeatherUnit.F;
        _s.ShowConditionName = _chkCondName.Checked;
        _s.LocationAuto = _radAuto.Checked;
        _s.CityName = _txtCity.Text.Trim();
        _s.PreviewCondition = (PreviewCondition)_cmbPreview.SelectedIndex;

        _s.PicturePath = _txtPicPath.Text;
        _s.PictureFit = _cmbFit.SelectedIndex == 0 ? PictureFit.Cover : PictureFit.Contain;
        _s.PictureBrightness = _trkPicBright.Value;

        _s.EffectsOn = _chkEffects.Checked;
        _s.TimeOfDay = (TimeOfDay)_cmbTod.SelectedIndex;
        _s.EffectsStrength = _trkStrength.Value;
    }

    private void ResetDefaults()
    {
        var d = new Settings();
        _s.Position = d.Position; _s.Font = d.Font; _s.Weight = d.Weight;
        _s.Size = d.Size; _s.Brightness = d.Brightness; _s.Margin = d.Margin;
        _s.Hours24 = d.Hours24; _s.ShowSeconds = d.ShowSeconds; _s.ShowDate = d.ShowDate; _s.Rotate = d.Rotate;
        _s.WeatherOn = d.WeatherOn; _s.Unit = d.Unit; _s.ShowConditionName = d.ShowConditionName;
        _s.LocationAuto = d.LocationAuto; _s.CityName = d.CityName; _s.PreviewCondition = d.PreviewCondition;
        _s.PicturePath = d.PicturePath; _s.PictureFit = d.PictureFit; _s.PictureBrightness = d.PictureBrightness;
        _s.EffectsOn = d.EffectsOn; _s.EffectsStrength = d.EffectsStrength; _s.TimeOfDay = d.TimeOfDay;
    }

    private void BrowsePicture(object? s, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Choose a picture",
            Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.tiff|All files|*.*",
            CheckFileExists = true,
        };
        if (!string.IsNullOrEmpty(_s.PicturePath) && File.Exists(_s.PicturePath))
            dlg.InitialDirectory = Path.GetDirectoryName(_s.PicturePath);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _s.PicturePath = dlg.FileName;
            _txtPicPath.Text = dlg.FileName;
        }
    }
}
