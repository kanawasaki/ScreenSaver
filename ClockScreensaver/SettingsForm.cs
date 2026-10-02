using System.Drawing.Drawing2D;

namespace ClockScreensaver;

public class SettingsForm : Form
{
    // ── Colors ──────────────────────────────────────────────────────────────
    private static readonly Color BG       = Color.FromArgb(18, 18, 18);
    private static readonly Color BG2      = Color.FromArgb(26, 26, 26);
    private static readonly Color BG3      = Color.FromArgb(36, 36, 36);
    private static readonly Color FGDim    = Color.FromArgb(110, 110, 118);
    private static readonly Color FGMid    = Color.FromArgb(160, 160, 168);
    private static readonly Color FGBright = Color.FromArgb(220, 220, 228);
    private static readonly Color Accent   = Color.FromArgb(120, 160, 255);
    private static readonly Color Divider  = Color.FromArgb(42, 42, 48);

    // ── Layout ──────────────────────────────────────────────────────────────
    private const int SidebarW  = 128;
    private const int ContentW  = 382;
    private const int PreviewW  = 250;
    private const int BottomH   = 52;
    private const int FormW     = SidebarW + ContentW + PreviewW;  // 760
    private const int FormH     = 520;
    private const int ContentH  = FormH - BottomH;                 // 468

    // ── State ────────────────────────────────────────────────────────────────
    private readonly Settings _original;
    private Settings _work;

    // Tab panels
    private Panel _clockPanel  = new();
    private Panel _wxPanel     = new();
    private Panel _picPanel    = new();
    private Panel _fxPanel     = new();
    private Panel? _activeTab;

    // Sidebar tab buttons
    private readonly Button[] _tabBtns = new Button[4];

    // Clock tab controls
    private PositionPicker _posPicker   = new();
    private ComboBox _cmbFont           = new();
    private ComboBox _cmbWeight         = new();
    private ComboBox _cmbDateFmt        = new();
    private DarkSlider _trkSize       = new();
    private Label      _lblSize       = new();
    private DarkSlider _trkBright     = new();
    private Label      _lblBright     = new();
    private DarkSlider _trkMargin     = new();
    private Label      _lblMargin     = new();
    private CheckBox _chk24h        = new();
    private CheckBox _chkSec        = new();
    private CheckBox _chkDate       = new();
    private CheckBox _chkRotate     = new();

    // Weather tab controls
    private CheckBox _chkWeather    = new();
    private Panel    _wxGroup       = new();
    private ComboBox _cmbUnit       = new();
    private CheckBox _chkCondName   = new();
    private RadioButton _radAuto    = new();
    private RadioButton _radCity    = new();
    private TextBox  _txtCity       = new();
    private Button   _btnTest       = new();
    private Label    _lblTestResult = new();
    private ComboBox _cmbPreview    = new();

    // Picture tab controls
    private RadioButton _radMyPicture   = new();
    private RadioButton _radLandscape   = new();
    private Panel    _myPicGroup    = new();
    private TextBox  _txtPicPath    = new();
    private Button   _btnBrowse     = new();
    private Button   _btnClear      = new();
    private Panel    _landscapeGroup     = new();
    private TextBox  _txtLandscapeFolder = new();
    private Button   _btnBrowseFolder    = new();
    private Button   _btnOpenFolder      = new();
    private Label    _lblLandscapeSummary = new();
    private readonly LandscapeLibrary _summaryLib = new();
    private Panel    _picGroup      = new();
    private ComboBox _cmbFit        = new();
    private DarkSlider _trkPicBright  = new();
    private Label      _lblPicBright  = new();

    // Effects tab controls
    private CheckBox   _chkFx         = new();
    private Panel      _fxGroup       = new();
    private ComboBox   _cmbTod        = new();
    private DarkSlider _trkStrength   = new();
    private Label      _lblStrength   = new();

    // Preview
    private ScreenPreviewPanel _preview = new();
    private System.Windows.Forms.Timer _previewTimer = new();
    private WeatherService? _testWeather;

    // ────────────────────────────────────────────────────────────────────────

    public SettingsForm(Settings s)
    {
        _original = s;
        _work     = Copy(s);

        SuspendLayout();
        Text            = $"Clock Screensaver — Settings  (v{BuildInfo.Version} · {BuildInfo.GitHash})";
        ClientSize      = new Size(FormW, FormH);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = BG;
        ForeColor       = FGBright;
        Font            = new Font("Segoe UI", 9f);
        AutoSize        = false;
        MinimumSize     = new Size(FormW + 16, FormH + 39); // client + chrome
        AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        AutoScaleMode   = AutoScaleMode.Dpi;

        BuildUI();
        LoadValues();
        SwitchTab(0);
        _previewTimer.Interval = 33;
        _previewTimer.Tick += (_, _) => _preview.Tick();
        _previewTimer.Start();

        ResumeLayout();
    }

    // ── Build ────────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        BuildSidebar();

        // Content host panel
        var content = new Panel { Left = SidebarW, Top = 0, Width = ContentW, Height = ContentH, BackColor = BG2 };
        Controls.Add(content);

        // Vertical divider
        var div = new Label { Left = SidebarW + ContentW, Top = 0, Width = 1, Height = ContentH, BackColor = Divider };
        Controls.Add(div);

        // Preview area
        var previewHost = new Panel { Left = SidebarW + ContentW + 1, Top = 0, Width = PreviewW - 1, Height = ContentH, BackColor = BG };
        Controls.Add(previewHost);
        BuildPreviewArea(previewHost);

        // Tab panels (all parented to content)
        BuildClockTab(content);
        BuildWeatherTab(content);
        BuildPictureTab(content);
        BuildEffectsTab(content);

        // Bottom bar
        BuildBottomBar();
    }

    private void BuildSidebar()
    {
        var sidebar = new Panel { Left = 0, Top = 0, Width = SidebarW, Height = ContentH, BackColor = BG };
        Controls.Add(sidebar);

        // App title
        var title = new Label
        {
            Text = "CLOCK\nSCREENSAVER",
            Left = 0, Top = 16, Width = SidebarW,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = FGDim,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            AutoSize = false, Height = 36,
        };
        sidebar.Controls.Add(title);

        string[] tabNames = { "Clock", "Weather", "Picture", "Effects" };
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            var btn = new Button
            {
                Text = tabNames[i],
                Left = 0, Top = 68 + i * 44, Width = SidebarW, Height = 40,
                FlatStyle = FlatStyle.Flat,
                BackColor = BG,
                ForeColor = FGMid,
                Font = new Font("Segoe UI", 9.5f),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = BG3;
            btn.Click += (_, _) => SwitchTab(idx);
            _tabBtns[i] = btn;
            sidebar.Controls.Add(btn);
        }

        // Version/commit footer — lets us tell which build is actually installed
        var verLbl = new Label
        {
            Text = $"v{BuildInfo.Version} · {BuildInfo.GitHash}",
            Left = 0, Top = ContentH - 44, Width = SidebarW,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = FGDim,
            Font = new Font("Segoe UI", 7f),
            AutoSize = false, Height = 16,
        };
        sidebar.Controls.Add(verLbl);

        // Log-file shortcut at bottom
        var logLbl = new Label
        {
            Text = "Open log",
            Left = 0, Top = ContentH - 28, Width = SidebarW,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = FGDim,
            Font = new Font("Segoe UI", 7.5f),
            Cursor = Cursors.Hand,
            AutoSize = false,
        };
        logLbl.Click += (_, _) =>
        {
            try { System.Diagnostics.Process.Start("notepad.exe", Logger.LogFile); } catch { }
        };
        sidebar.Controls.Add(logLbl);

        // Sidebar right border
        var sideDiv = new Label { Left = SidebarW - 1, Top = 0, Width = 1, Height = ContentH, BackColor = Divider };
        Controls.Add(sideDiv);
    }

    private void BuildClockTab(Panel host)
    {
        _clockPanel = MakeTabPanel(host);

        int y = 18;

        // ─ Position ─
        SectionHead(_clockPanel, "Position", ref y);

        _posPicker.Left = 16; _posPicker.Top = y; _posPicker.Width = 120; _posPicker.Height = 80;
        _posPicker.SelectionChanged += pos => { _work.Position = pos; MarkDirty(); };
        _clockPanel.Controls.Add(_posPicker);
        y += 92;

        // ─ Font ─
        SectionHead(_clockPanel, "Font", ref y);

        var fontRow = new Panel { Left = 16, Top = y, Width = 350, Height = 28, BackColor = Color.Transparent };
        _clockPanel.Controls.Add(fontRow);

        DarkCombo(_cmbFont, 0, 0, 200);
        _cmbFont.Items.AddRange(new[] { "Plex Mono", "Space Grotesk", "Fraunces", "Major Mono", "Syne", "System" });
        _cmbFont.SelectedIndexChanged += (_, _) => { _work.Font = (ClockFont)_cmbFont.SelectedIndex; MarkDirty(); };
        fontRow.Controls.Add(_cmbFont);

        DarkCombo(_cmbWeight, 210, 0, 130);
        _cmbWeight.Items.AddRange(new[] { "Light", "Bold" });
        _cmbWeight.SelectedIndexChanged += (_, _) => { _work.Weight = _cmbWeight.SelectedIndex == 0 ? ClockWeight.Light : ClockWeight.Bold; MarkDirty(); };
        fontRow.Controls.Add(_cmbWeight);
        y += 36;

        // ─ Sliders ─
        AddSlider(_clockPanel, ref y, "Size", _trkSize, _lblSize, 16, 240, _work.Size, "px",
            v => { _work.Size = v; MarkDirty(); });
        AddSlider(_clockPanel, ref y, "Brightness", _trkBright, _lblBright, 10, 100, _work.Brightness, "%",
            v => { _work.Brightness = v; MarkDirty(); });
        AddSlider(_clockPanel, ref y, "Edge margin", _trkMargin, _lblMargin, 8, 200, _work.Margin, "px",
            v => { _work.Margin = v; MarkDirty(); });

        // ─ Checkboxes ─
        y += 4;
        AddCheck(_clockPanel, _chk24h,  ref y, "24-hour time",    () => { _work.Hours24     = _chk24h.Checked; MarkDirty(); });
        AddCheck(_clockPanel, _chkSec,  ref y, "Show seconds",     () => { _work.ShowSeconds = _chkSec.Checked; MarkDirty(); });
        AddCheck(_clockPanel, _chkDate, ref y, "Show date",        () => { _work.ShowDate    = _chkDate.Checked; _cmbDateFmt.Visible = _chkDate.Checked; MarkDirty(); });

        // Date format — sub-row (only visible when Show date is checked)
        DarkCombo(_cmbDateFmt, 32, y, 220);
        _cmbDateFmt.Items.AddRange(new[] { "Wed, Sep 23", "Wednesday, September 23", "23 September", "23.09.2026" });
        _cmbDateFmt.Visible = _work.ShowDate;
        _cmbDateFmt.SelectedIndexChanged += (_, _) => { _work.DateFmt = (DateFormat)_cmbDateFmt.SelectedIndex; MarkDirty(); };
        _clockPanel.Controls.Add(_cmbDateFmt);
        y += 32; // always reserve space so layout doesn't shift

        AddCheck(_clockPanel, _chkRotate, ref y, "Move to next corner every 10 min", () => { _work.Rotate = _chkRotate.Checked; });
    }

    private void BuildWeatherTab(Panel host)
    {
        _wxPanel = MakeTabPanel(host);

        int y = 18;
        SectionHead(_wxPanel, "Weather display", ref y);

        AddCheck(_wxPanel, _chkWeather, ref y, "Show weather below the clock", () =>
        {
            _work.WeatherOn = _chkWeather.Checked;
            _wxGroup.Visible = _chkWeather.Checked;
            MarkDirty();
        });
        y += 4;

        // Collapsible group
        _wxGroup = new Panel { Left = 0, Top = y, Width = ContentW, BackColor = Color.Transparent };
        _wxPanel.Controls.Add(_wxGroup);

        int gy = 0;
        SectionHead(_wxGroup, "Units & display", ref gy);

        var unitRow = new Panel { Left = 16, Top = gy, Width = 350, Height = 28, BackColor = Color.Transparent };
        _wxGroup.Controls.Add(unitRow);

        DarkCombo(_cmbUnit, 0, 0, 90);
        _cmbUnit.Items.AddRange(new[] { "°C", "°F" });
        _cmbUnit.SelectedIndexChanged += (_, _) => { _work.Unit = _cmbUnit.SelectedIndex == 0 ? WeatherUnit.C : WeatherUnit.F; MarkDirty(); };
        unitRow.Controls.Add(_cmbUnit);
        gy += 36;

        AddCheck(_wxGroup, _chkCondName, ref gy, "Show condition name",
            () => { _work.ShowConditionName = _chkCondName.Checked; MarkDirty(); });
        gy += 12;

        SectionHead(_wxGroup, "Location", ref gy);

        _radAuto.Text = "Automatic (IP-based)"; StyleRadio(_radAuto);
        _radAuto.Left = 16; _radAuto.Top = gy; _radAuto.Width = 200;
        _radCity.Text = "City"; StyleRadio(_radCity);
        _radCity.Left = 222; _radCity.Top = gy; _radCity.Width = 100;
        _radAuto.CheckedChanged += (_, _) =>
        {
            _work.LocationAuto = _radAuto.Checked;
            _txtCity.Enabled   = _radCity.Checked;
            _btnTest.Enabled   = _radCity.Checked && _txtCity.Text.Trim().Length > 0;
        };
        _radCity.CheckedChanged += (_, _) =>
        {
            _txtCity.Enabled = _radCity.Checked;
            _btnTest.Enabled = _radCity.Checked && _txtCity.Text.Trim().Length > 0;
        };
        _wxGroup.Controls.Add(_radAuto);
        _wxGroup.Controls.Add(_radCity);
        gy += 28;

        // City textbox + Test button
        _txtCity.Left = 16; _txtCity.Top = gy; _txtCity.Width = 232; _txtCity.Height = 26;
        _txtCity.BackColor = BG3; _txtCity.ForeColor = FGBright;
        _txtCity.BorderStyle = BorderStyle.FixedSingle;
        _txtCity.PlaceholderText = "e.g. Berlin";
        _txtCity.TextChanged += (_, _) =>
        {
            _work.CityName   = _txtCity.Text.Trim();
            _btnTest.Enabled = _radCity.Checked && _txtCity.Text.Trim().Length > 0;
            _lblTestResult.Text = "";
        };
        _wxGroup.Controls.Add(_txtCity);

        _btnTest.Left = 256; _btnTest.Top = gy; _btnTest.Width = 80; _btnTest.Height = 26;
        _btnTest.Text = "Test"; StyleButton(_btnTest);
        _btnTest.Click += TestLocation_Click;
        _wxGroup.Controls.Add(_btnTest);
        gy += 32;

        _lblTestResult.Left = 16; _lblTestResult.Top = gy; _lblTestResult.Width = 330;
        _lblTestResult.AutoSize = false; _lblTestResult.Height = 20;
        _lblTestResult.ForeColor = FGMid;
        _wxGroup.Controls.Add(_lblTestResult);
        gy += 28;

        SectionHead(_wxGroup, "Preview condition (overrides live weather)", ref gy);
        DarkCombo(_cmbPreview, 16, gy, 320);
        _cmbPreview.Items.AddRange(new[] { "Automatic (use live weather)", "Clear", "Partly cloudy", "Cloudy", "Rain", "Snow", "Thunderstorm" });
        _cmbPreview.SelectedIndexChanged += (_, _) =>
        {
            _work.PreviewCondition = (PreviewCondition)_cmbPreview.SelectedIndex;
            MarkDirty();
        };
        _wxGroup.Controls.Add(_cmbPreview);
        gy += 36;
        _wxGroup.Height = gy;
    }

    private void BuildPictureTab(Panel host)
    {
        _picPanel = MakeTabPanel(host);

        int y = 18;
        SectionHead(_picPanel, "Background picture", ref y);

        // Source selector
        _radMyPicture.Text = "My picture"; StyleRadio(_radMyPicture);
        _radMyPicture.Left = 16; _radMyPicture.Top = y; _radMyPicture.Width = 130;
        _radLandscape.Text = "Matching landscape from folder"; StyleRadio(_radLandscape);
        _radLandscape.Left = 146; _radLandscape.Top = y; _radLandscape.Width = 220;
        _radMyPicture.CheckedChanged += (_, _) =>
        {
            _work.PictureSource = _radMyPicture.Checked ? PictureSource.MyPicture : PictureSource.MatchingLandscape;
            _myPicGroup.Visible = _radMyPicture.Checked;
            _landscapeGroup.Visible = !_radMyPicture.Checked;
            UpdatePicGroupVisibility();
            MarkDirty();
        };
        _picPanel.Controls.Add(_radMyPicture);
        _picPanel.Controls.Add(_radLandscape);
        y += 28;

        // ── "My picture" sub-group ──
        _myPicGroup = new Panel { Left = 0, Top = y, Width = ContentW, Height = 36, BackColor = Color.Transparent };
        _picPanel.Controls.Add(_myPicGroup);

        var row = new Panel { Left = 16, Top = 0, Width = 350, Height = 28, BackColor = Color.Transparent };
        _myPicGroup.Controls.Add(row);

        _txtPicPath.Left = 0; _txtPicPath.Top = 1; _txtPicPath.Width = 216; _txtPicPath.Height = 26;
        _txtPicPath.BackColor = BG3; _txtPicPath.ForeColor = FGDim;
        _txtPicPath.ReadOnly = true; _txtPicPath.BorderStyle = BorderStyle.FixedSingle;
        _txtPicPath.PlaceholderText = "No picture";
        row.Controls.Add(_txtPicPath);

        _btnBrowse.Left = 224; _btnBrowse.Top = 1; _btnBrowse.Width = 60; _btnBrowse.Height = 26;
        _btnBrowse.Text = "Browse"; StyleButton(_btnBrowse);
        _btnBrowse.Click += BrowsePicture;
        row.Controls.Add(_btnBrowse);

        _btnClear.Left = 288; _btnClear.Top = 1; _btnClear.Width = 60; _btnClear.Height = 26;
        _btnClear.Text = "Clear"; StyleButton(_btnClear);
        _btnClear.Click += (_, _) =>
        {
            _work.PicturePath = ""; _txtPicPath.Text = "";
            UpdatePicGroupVisibility(); MarkDirty();
        };
        row.Controls.Add(_btnClear);

        // ── "Matching landscape" sub-group ──
        _landscapeGroup = new Panel { Left = 0, Top = y, Width = ContentW, Height = 124, BackColor = Color.Transparent };
        _picPanel.Controls.Add(_landscapeGroup);

        var frow = new Panel { Left = 16, Top = 0, Width = 350, Height = 28, BackColor = Color.Transparent };
        _landscapeGroup.Controls.Add(frow);

        _txtLandscapeFolder.Left = 0; _txtLandscapeFolder.Top = 1; _txtLandscapeFolder.Width = 216; _txtLandscapeFolder.Height = 26;
        _txtLandscapeFolder.BackColor = BG3; _txtLandscapeFolder.ForeColor = FGDim;
        _txtLandscapeFolder.ReadOnly = true; _txtLandscapeFolder.BorderStyle = BorderStyle.FixedSingle;
        frow.Controls.Add(_txtLandscapeFolder);

        _btnBrowseFolder.Left = 224; _btnBrowseFolder.Top = 1; _btnBrowseFolder.Width = 60; _btnBrowseFolder.Height = 26;
        _btnBrowseFolder.Text = "Browse"; StyleButton(_btnBrowseFolder);
        _btnBrowseFolder.Click += BrowseLandscapeFolder;
        frow.Controls.Add(_btnBrowseFolder);

        _btnOpenFolder.Left = 16; _btnOpenFolder.Top = 34; _btnOpenFolder.Width = 110; _btnOpenFolder.Height = 26;
        _btnOpenFolder.Text = "Open folder"; StyleButton(_btnOpenFolder);
        _btnOpenFolder.Click += (_, _) => OpenLandscapeFolder();
        _landscapeGroup.Controls.Add(_btnOpenFolder);

        _lblLandscapeSummary.Left = 16; _lblLandscapeSummary.Top = 68; _lblLandscapeSummary.Width = ContentW - 32;
        _lblLandscapeSummary.AutoSize = false; _lblLandscapeSummary.Height = 56;
        _lblLandscapeSummary.ForeColor = FGMid;
        _landscapeGroup.Controls.Add(_lblLandscapeSummary);

        y += Math.Max(_myPicGroup.Height, _landscapeGroup.Height) + 8;

        // Collapsible options (shared between both sources)
        _picGroup = new Panel { Left = 0, Top = y, Width = ContentW, BackColor = Color.Transparent };
        _picPanel.Controls.Add(_picGroup);

        int gy = 0;
        SectionHead(_picGroup, "Image options", ref gy);

        DarkCombo(_cmbFit, 16, gy, 200);
        _cmbFit.Items.AddRange(new[] { "Fill screen (cover)", "Show whole picture (contain)" });
        _cmbFit.SelectedIndexChanged += (_, _) =>
        {
            _work.PictureFit = _cmbFit.SelectedIndex == 0 ? PictureFit.Cover : PictureFit.Contain;
            MarkDirty();
        };
        _picGroup.Controls.Add(_cmbFit);
        gy += 36;

        AddSlider(_picGroup, ref gy, "Picture brightness", _trkPicBright, _lblPicBright, 5, 100, _work.PictureBrightness, "%",
            v => { _work.PictureBrightness = v; MarkDirty(); });
        _picGroup.Height = gy;
    }

    private void UpdatePicGroupVisibility()
    {
        _picGroup.Visible = _work.PictureSource == PictureSource.MatchingLandscape
            || !string.IsNullOrEmpty(_work.PicturePath);
    }

    private void BrowseLandscapeFolder(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Choose a folder of weather-matched landscape pictures",
            UseDescriptionForTitle = true,
        };
        if (Directory.Exists(_work.LandscapeFolder)) dlg.SelectedPath = _work.LandscapeFolder;

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _work.LandscapeFolder = dlg.SelectedPath;
        _txtLandscapeFolder.Text = dlg.SelectedPath;
        RefreshLandscapeSummary();
        MarkDirty();
    }

    private void OpenLandscapeFolder()
    {
        try
        {
            Directory.CreateDirectory(_work.LandscapeFolder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_work.LandscapeFolder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Log($"OpenLandscapeFolder failed for \"{_work.LandscapeFolder}\": {ex.Message}");
        }
    }

    private void RefreshLandscapeSummary()
    {
        var scan = _summaryLib.EnsureScanned(_work.LandscapeFolder);
        _lblLandscapeSummary.Text = LandscapeSummary.Describe(scan);
    }

    private void BuildEffectsTab(Panel host)
    {
        _fxPanel = MakeTabPanel(host);

        int y = 18;
        SectionHead(_fxPanel, "Ambient effects", ref y);

        AddCheck(_fxPanel, _chkFx, ref y, "Show weather && ambient effects", () =>
        {
            _work.EffectsOn = _chkFx.Checked;
            _fxGroup.Visible = _chkFx.Checked;
            MarkDirty();
        });
        y += 4;

        _fxGroup = new Panel { Left = 0, Top = y, Width = ContentW, BackColor = Color.Transparent };
        _fxPanel.Controls.Add(_fxGroup);

        int gy = 0;
        SectionHead(_fxGroup, "Time of day", ref gy);

        DarkCombo(_cmbTod, 16, gy, 320);
        _cmbTod.Items.AddRange(new[] { "Automatic (use real sunrise/sunset)", "Always day", "Always night" });
        _cmbTod.SelectedIndexChanged += (_, _) =>
        {
            _work.TimeOfDay = (TimeOfDay)_cmbTod.SelectedIndex;
            MarkDirty();
        };
        _fxGroup.Controls.Add(_cmbTod);
        gy += 36;

        SectionHead(_fxGroup, "Strength", ref gy);
        AddSlider(_fxGroup, ref gy, "Effect strength", _trkStrength, _lblStrength, 10, 100, _work.EffectsStrength, "%",
            v => { _work.EffectsStrength = v; MarkDirty(); });
        _fxGroup.Height = gy;
    }

    private void BuildPreviewArea(Panel host)
    {
        var titleLbl = new Label
        {
            Text = "PREVIEW",
            Left = 0, Top = 12, Width = PreviewW - 1,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = FGDim,
            Font = new Font("Segoe UI", 7f, FontStyle.Bold),
            AutoSize = false, Height = 16,
        };
        host.Controls.Add(titleLbl);

        // Preview render area (16:9, ~230×130)
        int pw = PreviewW - 20;
        int ph = (int)(pw * 9f / 16f);
        int px = 10;
        int py = 36;

        // Border panel
        var border = new Panel
        {
            Left = px - 1, Top = py - 1, Width = pw + 2, Height = ph + 2,
            BackColor = Divider,
        };
        host.Controls.Add(border);

        _preview.Left = px; _preview.Top = py; _preview.Width = pw; _preview.Height = ph;
        _preview.BackColor = Color.Black;
        host.Controls.Add(_preview);
        _preview.BringToFront(); // must render above the border panel, not be clipped behind it

        // Condition info label below preview
        var condLbl = new Label
        {
            Name = "condLbl",
            Left = 0, Top = py + ph + 12, Width = PreviewW - 1,
            TextAlign = ContentAlignment.TopCenter,
            ForeColor = FGDim,
            Font = new Font("Segoe UI", 7.5f),
            AutoSize = false, Height = 60,
        };
        host.Controls.Add(condLbl);

        // Bind preview updates
        _preview.CondLbl = condLbl;
    }

    private void BuildBottomBar()
    {
        var bar = new Panel { Left = 0, Top = ContentH, Width = FormW, Height = BottomH, BackColor = BG };
        Controls.Add(bar);

        // Separator
        var sep = new Label { Left = 0, Top = 0, Width = FormW, Height = 1, BackColor = Divider };
        bar.Controls.Add(sep);

        var btnFullscreen = new Button
        {
            Text = "Preview full screen", Left = 12, Top = 11, Width = 160, Height = 30,
            FlatStyle = FlatStyle.Flat, BackColor = BG3, ForeColor = FGMid,
            Font = new Font("Segoe UI", 9f), Cursor = Cursors.Hand,
        };
        btnFullscreen.FlatAppearance.BorderColor = Divider;
        btnFullscreen.Click += (_, _) =>
        {
            SyncWorkFromControls();
            var tmp = Copy(_work);
            var fx  = new WeatherService(tmp);
            var form = new ScreensaverForm(tmp, fx, Screen.PrimaryScreen!, true, false);
            form.FormClosed += (_, _) => fx.Dispose();
            form.Show();
        };
        bar.Controls.Add(btnFullscreen);

        var btnOK = new Button
        {
            Text = "OK", Left = FormW - 202, Top = 11, Width = 90, Height = 30,
            FlatStyle = FlatStyle.Flat, BackColor = Accent, ForeColor = Color.FromArgb(10, 10, 10),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand,
            DialogResult = DialogResult.OK,
        };
        btnOK.FlatAppearance.BorderSize = 0;
        btnOK.Click += (_, _) =>
        {
            // Validate city
            if (_radCity.Checked && _work.LocationAuto == false && string.IsNullOrWhiteSpace(_work.CityName))
            {
                _lblTestResult.ForeColor = Color.FromArgb(220, 130, 80);
                _lblTestResult.Text = "Enter a city name, or switch to Automatic.";
                SwitchTab(1);
                return;
            }
            SyncWorkFromControls();
            CopyTo(_work, _original);
            var saveErr = _original.TrySave();
            if (saveErr != null)
            {
                MessageBox.Show(this, $"Could not save settings:\n{saveErr}",
                    "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        bar.Controls.Add(btnOK);

        var btnCancel = new Button
        {
            Text = "Cancel", Left = FormW - 106, Top = 11, Width = 90, Height = 30,
            FlatStyle = FlatStyle.Flat, BackColor = BG3, ForeColor = FGMid,
            Font = new Font("Segoe UI", 9f), Cursor = Cursors.Hand,
            DialogResult = DialogResult.Cancel,
        };
        btnCancel.FlatAppearance.BorderColor = Divider;
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        bar.Controls.Add(btnCancel);

        AcceptButton = btnOK;
        CancelButton = btnCancel;
    }

    // ── Tab switching ────────────────────────────────────────────────────────

    internal void SwitchTab(int idx)
    {
        Panel[] tabs = { _clockPanel, _wxPanel, _picPanel, _fxPanel };
        foreach (var t in tabs) t.Visible = false;
        tabs[idx].Visible = true;
        _activeTab = tabs[idx];

        for (int i = 0; i < _tabBtns.Length; i++)
        {
            _tabBtns[i].BackColor = i == idx ? BG3 : BG;
            _tabBtns[i].ForeColor = i == idx ? FGBright : FGMid;
        }
    }

    // ── Load / sync ──────────────────────────────────────────────────────────

    private void LoadValues()
    {
        _posPicker.Selected = _work.Position;
        _cmbFont.SelectedIndex    = (int)_work.Font;
        _cmbWeight.SelectedIndex  = _work.Weight == ClockWeight.Light ? 0 : 1;
        SetSlider(_trkSize,     _lblSize,     _work.Size,             "px");
        SetSlider(_trkBright,   _lblBright,   _work.Brightness,       "%");
        SetSlider(_trkMargin,   _lblMargin,   _work.Margin,           "px");
        _chk24h.Checked     = _work.Hours24;
        _chkSec.Checked     = _work.ShowSeconds;
        _chkDate.Checked    = _work.ShowDate;
        _chkRotate.Checked  = _work.Rotate;
        _cmbDateFmt.SelectedIndex = (int)_work.DateFmt;
        _cmbDateFmt.Visible       = _work.ShowDate;

        _chkWeather.Checked     = _work.WeatherOn;
        _wxGroup.Visible        = _work.WeatherOn;
        _cmbUnit.SelectedIndex  = _work.Unit == WeatherUnit.C ? 0 : 1;
        _chkCondName.Checked    = _work.ShowConditionName;
        _radAuto.Checked        = _work.LocationAuto;
        _radCity.Checked        = !_work.LocationAuto;
        _txtCity.Text           = _work.CityName;
        _txtCity.Enabled        = !_work.LocationAuto;
        _btnTest.Enabled        = !_work.LocationAuto && _work.CityName.Length > 0;
        _cmbPreview.SelectedIndex = (int)_work.PreviewCondition;

        _radMyPicture.Checked    = _work.PictureSource == PictureSource.MyPicture;
        _radLandscape.Checked    = _work.PictureSource == PictureSource.MatchingLandscape;
        _myPicGroup.Visible      = _work.PictureSource == PictureSource.MyPicture;
        _landscapeGroup.Visible  = _work.PictureSource == PictureSource.MatchingLandscape;
        _txtPicPath.Text         = Path.GetFileName(_work.PicturePath);
        _txtLandscapeFolder.Text = _work.LandscapeFolder;
        RefreshLandscapeSummary();
        UpdatePicGroupVisibility();
        _cmbFit.SelectedIndex  = _work.PictureFit == PictureFit.Cover ? 0 : 1;
        SetSlider(_trkPicBright, _lblPicBright, _work.PictureBrightness, "%");

        _chkFx.Checked         = _work.EffectsOn;
        _fxGroup.Visible       = _work.EffectsOn;
        _cmbTod.SelectedIndex  = (int)_work.TimeOfDay;
        SetSlider(_trkStrength, _lblStrength, _work.EffectsStrength, "%");

        _preview.UpdateSettings(Copy(_work));
    }

    private void SyncWorkFromControls()
    {
        _work.Position  = _posPicker.Selected;
        _work.Font      = (ClockFont)_cmbFont.SelectedIndex;
        _work.Weight    = _cmbWeight.SelectedIndex == 0 ? ClockWeight.Light : ClockWeight.Bold;
        _work.Size      = _trkSize.Value;
        _work.Brightness  = _trkBright.Value;
        _work.Margin    = _trkMargin.Value;
        _work.Hours24     = _chk24h.Checked;
        _work.ShowSeconds = _chkSec.Checked;
        _work.ShowDate    = _chkDate.Checked;
        _work.Rotate      = _chkRotate.Checked;
        _work.DateFmt     = (DateFormat)_cmbDateFmt.SelectedIndex;
        _work.WeatherOn = _chkWeather.Checked;
        _work.Unit      = _cmbUnit.SelectedIndex == 0 ? WeatherUnit.C : WeatherUnit.F;
        _work.ShowConditionName = _chkCondName.Checked;
        _work.LocationAuto = _radAuto.Checked;
        _work.CityName  = _txtCity.Text.Trim();
        _work.PreviewCondition = (PreviewCondition)_cmbPreview.SelectedIndex;
        _work.PictureSource = _radMyPicture.Checked ? PictureSource.MyPicture : PictureSource.MatchingLandscape;
        _work.PictureFit  = _cmbFit.SelectedIndex == 0 ? PictureFit.Cover : PictureFit.Contain;
        _work.PictureBrightness = _trkPicBright.Value;
        _work.EffectsOn   = _chkFx.Checked;
        _work.TimeOfDay   = (TimeOfDay)_cmbTod.SelectedIndex;
        _work.EffectsStrength = _trkStrength.Value;
    }

    private void MarkDirty()
    {
        _preview.UpdateSettings(Copy(_work));
    }

    // ── Events ───────────────────────────────────────────────────────────────

    private async void TestLocation_Click(object? sender, EventArgs e)
    {
        _btnTest.Enabled     = false;
        _lblTestResult.ForeColor = FGMid;
        _lblTestResult.Text  = "Checking…";

        try
        {
            _testWeather ??= new WeatherService(_work);
            var result = await _testWeather.TestAsync(_radCity.Checked ? _work.CityName : null);
            _lblTestResult.ForeColor = Color.FromArgb(100, 200, 120);
            _lblTestResult.Text = result;
        }
        catch (Exception ex)
        {
            _lblTestResult.ForeColor = Color.FromArgb(220, 100, 80);
            _lblTestResult.Text = ex.Message;
        }
        finally
        {
            _btnTest.Enabled = _radCity.Checked && _txtCity.Text.Trim().Length > 0;
        }
    }

    private void BrowsePicture(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "Choose a background picture",
            Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All files|*.*",
            CheckFileExists = true,
        };
        if (!string.IsNullOrEmpty(_work.PicturePath) && File.Exists(_work.PicturePath))
            dlg.InitialDirectory = Path.GetDirectoryName(_work.PicturePath);

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        // Copy the file into the managed picture folder so it survives moves/deletes.
        string dest;
        try
        {
            Directory.CreateDirectory(Settings.PictureDir);
            string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
            // Unique name so the renderer sees a new path and rebuilds the background.
            dest = Path.Combine(Settings.PictureDir, $"background-{DateTime.Now:yyyyMMdd-HHmmss-fff}{ext}");
            File.Copy(dlg.FileName, dest, overwrite: true);
            Logger.Log($"Picture copied: {dlg.FileName} → {dest}");
            DeleteOldPictures(dest);
        }
        catch (Exception ex)
        {
            Logger.Log($"Picture copy failed: {ex.Message}");
            MessageBox.Show(this, $"Could not copy the picture:\n{ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _work.PicturePath = dest;
        _txtPicPath.Text  = Path.GetFileName(dest);
        UpdatePicGroupVisibility();
        MarkDirty();
    }

    private static void DeleteOldPictures(string keep)
    {
        foreach (var file in Directory.GetFiles(Settings.PictureDir))
        {
            if (string.Equals(file, keep, StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                File.Delete(file);
                Logger.Log($"Old picture deleted: {file}");
            }
            catch (Exception ex)
            {
                Logger.Log($"Could not delete old picture {file}: {ex.Message}");
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _previewTimer.Dispose();
            _testWeather?.Dispose();
            _preview.Dispose();
        }
        base.Dispose(disposing);
    }

    // ── Builder helpers ──────────────────────────────────────────────────────

    private static Panel MakeTabPanel(Panel host)
    {
        var p = new Panel
        {
            Left = 0, Top = 0, Width = ContentW, Height = ContentH,
            BackColor = BG2, Visible = false,
        };
        host.Controls.Add(p);
        return p;
    }

    private static void SectionHead(Panel p, string text, ref int y)
    {
        p.Controls.Add(new Label
        {
            Text = text.ToUpperInvariant(),
            Left = 16, Top = y, Width = ContentW - 32,
            ForeColor = FGDim,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            AutoSize = false, Height = 16,
        });
        y += 20;
    }

    private static void AddSlider(Panel parent, ref int y, string label,
        DarkSlider trk, Label valLbl, int min, int max, int init, string unit,
        Action<int> onChange)
    {
        var hdrPanel = new Panel { Left = 16, Top = y, Width = ContentW - 32, Height = 18, BackColor = Color.Transparent };
        hdrPanel.Controls.Add(new Label
        {
            Text = label, Left = 0, Top = 0, Width = hdrPanel.Width - 72,
            ForeColor = FGMid, AutoSize = false, Height = 18,
        });
        valLbl.Left = hdrPanel.Width - 72; valLbl.Top = 0;
        valLbl.Width = 72; valLbl.Height = 18;
        valLbl.Text = $"{init} {unit}";
        valLbl.TextAlign = ContentAlignment.MiddleRight;
        valLbl.ForeColor = FGBright;
        hdrPanel.Controls.Add(valLbl);
        parent.Controls.Add(hdrPanel);
        y += 20;

        trk.Left = 12; trk.Top = y; trk.Width = ContentW - 24; trk.Height = 22;
        trk.Minimum = min; trk.Maximum = max; trk.Value = Math.Clamp(init, min, max);
        trk.ValueChanged += (_, _) => { valLbl.Text = $"{trk.Value} {unit}"; onChange(trk.Value); };
        parent.Controls.Add(trk);
        y += 28;
    }

    private static void SetSlider(DarkSlider trk, Label lbl, int value, string unit)
    {
        trk.Value = Math.Clamp(value, trk.Minimum, trk.Maximum);
        lbl.Text  = $"{value} {unit}";
    }

    private static void AddCheck(Panel parent, CheckBox chk, ref int y, string text, Action onChange)
    {
        chk.Left = 16; chk.Top = y; chk.Width = ContentW - 32; chk.Height = 22;
        chk.Text = text;
        chk.BackColor = Color.Transparent;
        chk.ForeColor = FGMid;
        chk.AutoSize  = false;
        chk.CheckedChanged += (_, _) => onChange();
        parent.Controls.Add(chk);
        y += 26;
    }

    private static void DarkCombo(ComboBox cb, int left, int top, int width)
    {
        cb.Left = left; cb.Top = top; cb.Width = width; cb.Height = 26;
        cb.DropDownStyle = ComboBoxStyle.DropDownList;
        cb.FlatStyle     = FlatStyle.Flat;
        cb.BackColor     = BG3;
        cb.ForeColor     = FGBright;
        cb.DrawMode      = DrawMode.OwnerDrawFixed;
        cb.ItemHeight    = 22;
        cb.DrawItem += (s, e) =>
        {
            e.DrawBackground();
            var box = (ComboBox)s!;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using var bg  = new SolidBrush(sel ? Color.FromArgb(55, 55, 68) : BG3);
            e.Graphics.FillRectangle(bg, e.Bounds);
            if (e.Index >= 0)
            {
                using var fg = new SolidBrush(sel ? FGBright : FGMid);
                e.Graphics.DrawString(box.Items[e.Index]?.ToString(), e.Font!, fg,
                    new RectangleF(e.Bounds.X + 6, e.Bounds.Y + 2, e.Bounds.Width, e.Bounds.Height));
            }
            if ((e.State & DrawItemState.Focus) != 0)
                e.DrawFocusRectangle();
        };
    }

    private static void StyleButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.BackColor = BG3;
        btn.ForeColor = FGMid;
        btn.Cursor    = Cursors.Hand;
        btn.FlatAppearance.BorderColor = Divider;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 60);
    }

    private static void StyleRadio(RadioButton r)
    {
        r.BackColor = Color.Transparent;
        r.ForeColor = FGMid;
        r.AutoSize  = false;
        r.Height    = 22;
    }

    // ── Settings copy helpers ─────────────────────────────────────────────────

    private static Settings Copy(Settings s) => new()
    {
        Position = s.Position, Font = s.Font, Weight = s.Weight,
        Size = s.Size, Brightness = s.Brightness, Margin = s.Margin,
        Hours24 = s.Hours24, ShowSeconds = s.ShowSeconds, ShowDate = s.ShowDate, Rotate = s.Rotate,
        DateFmt = s.DateFmt,
        WeatherOn = s.WeatherOn, Unit = s.Unit, ShowConditionName = s.ShowConditionName,
        LocationAuto = s.LocationAuto, CityName = s.CityName, PreviewCondition = s.PreviewCondition,
        PictureSource = s.PictureSource, PicturePath = s.PicturePath, LandscapeFolder = s.LandscapeFolder,
        PictureFit = s.PictureFit, PictureBrightness = s.PictureBrightness,
        EffectsOn = s.EffectsOn, EffectsStrength = s.EffectsStrength, TimeOfDay = s.TimeOfDay,
    };

    private static void CopyTo(Settings src, Settings dst)
    {
        dst.Position = src.Position; dst.Font = src.Font; dst.Weight = src.Weight;
        dst.Size = src.Size; dst.Brightness = src.Brightness; dst.Margin = src.Margin;
        dst.Hours24 = src.Hours24; dst.ShowSeconds = src.ShowSeconds; dst.ShowDate = src.ShowDate; dst.Rotate = src.Rotate;
        dst.DateFmt = src.DateFmt;
        dst.WeatherOn = src.WeatherOn; dst.Unit = src.Unit; dst.ShowConditionName = src.ShowConditionName;
        dst.LocationAuto = src.LocationAuto; dst.CityName = src.CityName; dst.PreviewCondition = src.PreviewCondition;
        dst.PictureSource = src.PictureSource; dst.PicturePath = src.PicturePath; dst.LandscapeFolder = src.LandscapeFolder;
        dst.PictureFit = src.PictureFit; dst.PictureBrightness = src.PictureBrightness;
        dst.EffectsOn = src.EffectsOn; dst.EffectsStrength = src.EffectsStrength; dst.TimeOfDay = src.TimeOfDay;
    }

    // ── Nested: corner position picker ───────────────────────────────────────

    private class PositionPicker : Control
    {
        private ClockPosition _sel = ClockPosition.BottomRight;
        public ClockPosition Selected { get => _sel; set { _sel = value; Invalidate(); } }
        public event Action<ClockPosition>? SelectionChanged;

        private static readonly (ClockPosition pos, float rx, float ry)[] Slots =
        {
            (ClockPosition.TopLeft,     0.12f, 0.12f),
            (ClockPosition.TopRight,    0.88f, 0.12f),
            (ClockPosition.Center,      0.50f, 0.50f),
            (ClockPosition.BottomLeft,  0.12f, 0.88f),
            (ClockPosition.BottomRight, 0.88f, 0.88f),
        };

        public PositionPicker()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            TabStop = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BG3);

            // Monitor outline
            int bw = Width - 1, bh = Height - 1;
            using var borderPen = new Pen(Divider, 1.5f);
            g.DrawRectangle(borderPen, 1, 1, bw - 2, bh - 2);

            const float r = 5.5f;
            foreach (var (pos, rx, ry) in Slots)
            {
                float cx = rx * (bw);
                float cy = ry * (bh);
                bool active = pos == _sel;

                if (active)
                {
                    using var ring = new SolidBrush(Color.FromArgb(40, 120, 160, 255));
                    g.FillEllipse(ring, cx - r * 2, cy - r * 2, r * 4, r * 4);
                }
                using var dot = new SolidBrush(active ? Accent : Color.FromArgb(70, 70, 80));
                g.FillEllipse(dot, cx - r, cy - r, r * 2, r * 2);
            }

            if (Focused)
            {
                ControlPaint.DrawFocusRectangle(g, new Rectangle(2, 2, bw - 4, bh - 4));
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            Focus();
            const float hitR = 11f;
            int bw = Width - 1, bh = Height - 1;
            foreach (var (pos, rx, ry) in Slots)
            {
                float cx = rx * bw, cy = ry * bh;
                float dx = e.X - cx, dy = e.Y - cy;
                if (dx * dx + dy * dy < hitR * hitR)
                {
                    Selected = pos;
                    SelectionChanged?.Invoke(pos);
                    return;
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            ClockPosition[] order = {
                ClockPosition.TopLeft, ClockPosition.TopRight,
                ClockPosition.Center,
                ClockPosition.BottomLeft, ClockPosition.BottomRight
            };
            int idx = Array.IndexOf(order, _sel);
            int next = idx;
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Down)
                next = (idx + 1) % order.Length;
            else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Up)
                next = (idx - 1 + order.Length) % order.Length;
            else { base.OnKeyDown(e); return; }
            Selected = order[next];
            SelectionChanged?.Invoke(Selected);
            e.Handled = true;
        }
    }

    // ── Nested: custom slider control ────────────────────────────────────────

    private class DarkSlider : Control
    {
        private int _min, _max, _val;
        private bool _dragging;
        public int Minimum { get => _min; set { _min = value; Invalidate(); } }
        public int Maximum { get => _max; set { _max = value; Invalidate(); } }
        public int Value
        {
            get => _val;
            set
            {
                int c = Math.Clamp(value, _min, _max);
                if (c == _val) return;
                _val = c; Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler? ValueChanged;

        public DarkSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            _min = 0; _max = 100; _val = 0;
            TabStop = true;
            Height = 22;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? BG2);

            int trackH   = 4;
            int trackY   = (Height - trackH) / 2;
            int thumbR   = 6;
            int trackL   = thumbR + 4;
            int trackR   = Width - thumbR - 4;
            int trackW   = Math.Max(trackR - trackL, 1);
            float range  = _max - _min;
            float frac   = range > 0 ? (_val - _min) / range : 0f;
            int thumbCx  = trackL + (int)(frac * trackW);

            using var bgBrush   = new SolidBrush(Color.FromArgb(55, 55, 65));
            g.FillRectangle(bgBrush, trackL, trackY, trackW, trackH);

            if (thumbCx > trackL)
            {
                using var fillBrush = new SolidBrush(Color.FromArgb(90, 130, 230));
                g.FillRectangle(fillBrush, trackL, trackY, thumbCx - trackL, trackH);
            }

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int ty = (Height - thumbR * 2) / 2;
            using var thumbBrush = new SolidBrush(Color.FromArgb(200, 210, 255));
            g.FillEllipse(thumbBrush, thumbCx - thumbR, ty, thumbR * 2, thumbR * 2);

            if (Focused)
                ControlPaint.DrawFocusRectangle(g, new Rectangle(0, 0, Width - 1, Height - 1));
        }

        private int XToValue(int x)
        {
            int thumbR  = 6;
            int trackL  = thumbR + 4;
            int trackR  = Width - thumbR - 4;
            int trackW  = Math.Max(trackR - trackL, 1);
            float frac  = Math.Clamp((float)(x - trackL) / trackW, 0f, 1f);
            return _min + (int)MathF.Round(frac * (_max - _min));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { _dragging = true; Value = XToValue(e.X); Focus(); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging) Value = XToValue(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) _dragging = false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int step = e.Shift ? 10 : 1;
            if      (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up)   { Value += step; e.Handled = true; }
            else if (e.KeyCode == Keys.Left  || e.KeyCode == Keys.Down) { Value -= step; e.Handled = true; }
            else base.OnKeyDown(e);
        }
    }

    // ── Nested: live preview panel ───────────────────────────────────────────

    private class ScreenPreviewPanel : Control
    {
        private const int VirtW = 600;
        private const int VirtH = 338;

        private readonly EffectsRenderer _fx = new();
        private Settings? _settings;
        private double _tSec;
        private long _lastTick = Environment.TickCount64;
        private bool _dirty = true;
        public Label? CondLbl;

        public ScreenPreviewPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        public void UpdateSettings(Settings s)
        {
            _settings = s;
            // Rebuild immediately rather than waiting for the next timer tick — otherwise
            // the very first paint (and any forced capture via DrawToBitmap) can race ahead
            // of the timer and render before the picture/effects are ever built.
            RebuildFx();
            Invalidate();
        }

        public void Tick()
        {
            long now = Environment.TickCount64;
            float dt = Math.Min((now - _lastTick) / 1000f, 0.05f);
            _lastTick = now;
            _tSec += dt;

            if (_dirty && _settings != null)
                RebuildFx();

            Invalidate();
        }

        private void RebuildFx()
        {
            if (_settings == null) return;

            WeatherCondition? cond = null;
            bool isNight = false;

            if (_settings.EffectsOn)
            {
                if (_settings.PreviewCondition != PreviewCondition.Auto)
                {
                    cond = _settings.PreviewCondition switch
                    {
                        PreviewCondition.Clear        => WeatherCondition.Clear,
                        PreviewCondition.PartlyCloudy => WeatherCondition.PartlyCloudy,
                        PreviewCondition.Cloudy       => WeatherCondition.Cloudy,
                        PreviewCondition.Rain         => WeatherCondition.Rain,
                        PreviewCondition.Snow         => WeatherCondition.Snow,
                        PreviewCondition.Storm        => WeatherCondition.Storm,
                        _                             => WeatherCondition.Clear,
                    };
                }
                else
                    cond = WeatherCondition.Clear;

                isNight = _settings.TimeOfDay == TimeOfDay.Night ||
                          (_settings.TimeOfDay == TimeOfDay.Auto &&
                           (DateTime.Now.Hour >= 20 || DateTime.Now.Hour < 6));
            }

            var previewWeather = MakePreviewWeather();
            SceneTime sceneTime = previewWeather?.GetSceneTime(_settings.TimeOfDay) ?? SceneTime.Day;
            Season season = SeasonCalc.GetSeason(DateTime.Now, 0f);

            _fx.Build(VirtW, VirtH, cond, isNight, _settings.EffectsStrength / 100f,
                _settings.Position, _settings, _tSec, sceneTime, season);
            _dirty = false;

            if (CondLbl != null)
            {
                string condName = cond.HasValue ? WmoMapper.Name(cond.Value) : "–";
                string picStatus = _settings.PictureSource == PictureSource.MatchingLandscape
                    ? (_fx.CurrentBackgroundPath != null ? $"Picture: {Path.GetFileName(_fx.CurrentBackgroundPath)}" : "No matching picture")
                    : (string.IsNullOrEmpty(_settings.PicturePath) ? "No background" : "Background set");
                CondLbl.Text = $"Condition: {condName}\nTime: {sceneTime}\n{picStatus}";
            }
        }

        private WeatherResult? MakePreviewWeather()
        {
            if (_settings == null) return null;
            WeatherCondition cond = _settings.PreviewCondition switch
            {
                PreviewCondition.Clear        => WeatherCondition.Clear,
                PreviewCondition.PartlyCloudy => WeatherCondition.PartlyCloudy,
                PreviewCondition.Cloudy       => WeatherCondition.Cloudy,
                PreviewCondition.Rain         => WeatherCondition.Rain,
                PreviewCondition.Snow         => WeatherCondition.Snow,
                PreviewCondition.Storm        => WeatherCondition.Storm,
                _                             => WeatherCondition.Clear,
            };
            return new WeatherResult
            {
                Condition = cond, TempC = 14f,
                Sunrise = DateTime.Today.AddHours(6),
                Sunset  = DateTime.Today.AddHours(20),
                FetchedAt = DateTime.Now,
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.Black);
            if (_settings == null) return;

            // Render scene at virtual resolution so fonts scale correctly
            using var vbmp = new Bitmap(VirtW, VirtH);
            using (var vg = Graphics.FromImage(vbmp))
            {
                vg.Clear(Color.Black);
                vg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                _fx.Draw(vg, _tSec, 0.033f, VirtW, VirtH, false);

                float opacity = _settings.Brightness / 100f;
                var weather = _settings.WeatherOn ? MakePreviewWeather() : null;
                ClockRenderer.Draw(vg, _settings, weather, new Rectangle(0, 0, VirtW, VirtH), opacity, _fx.HasBackground);
            }

            // Scale the virtual bitmap to fit the panel
            float scale = Math.Min((float)Width / VirtW, (float)Height / VirtH);
            float offX  = (Width  - VirtW * scale) / 2f;
            float offY  = (Height - VirtH * scale) / 2f;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
            g.DrawImage(vbmp, offX, offY, VirtW * scale, VirtH * scale);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _fx.Dispose();
            base.Dispose(disposing);
        }
    }
}
