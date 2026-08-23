using FFmpegVideoCutter.Models;
using FFmpegVideoCutter.Services;
using FFmpegVideoCutter.Utils;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;

namespace FFmpegVideoCutter.UI;

public sealed partial class MainForm : Form
{
    private readonly CutService _cut;
    private readonly FfmpegLocator _locator;
    private readonly List<VideoItem> _batchItems = new();
    private readonly List<CutError> _errors = new();
    private readonly System.Windows.Forms.Timer _previewTimer;

    private LibVLC? _libVLC;
    private MediaPlayer? _player;
    private VideoItem? _previewItem;
    private CancellationTokenSource? _cts;

    private string? _currentVideoPath;
    private string _currentVideoCodec = "";
    private double _duration;
    private bool _processing;
    private HashSet<string> _availableEncoders = new(StringComparer.OrdinalIgnoreCase);

    private RadioButton _singleRadio = null!;
    private RadioButton _batchRadio = null!;
    private TextBox _sourcePath = null!;
    private TextBox _startText = null!;
    private TextBox _endText = null!;
    private TextBox _outputPath = null!;
    private ComboBox _profileCombo = null!;
    private ComboBox _rateCombo = null!;
    private TrackBar _volume = null!;
    private Button _playBtn = null!;
    private Button _backBtn = null!;
    private Button _fwdBtn = null!;
    private Label _infoLabel = null!;
    private Label _progressLabel = null!;
    private Label _overallLabel = null!;
    private ProgressBar _progressBar = null!;
    private LinkLabel _errorLink = null!;
    private TimelineControl _timeline = null!;
    private VideoView _videoView = null!;
    private SplitContainer _split = null!;
    private Panel _singlePanel = null!;
    private ListView _listView = null!;
    private CheckBox _applyAllCheck = null!;
    private TextBox _uniformStart = null!;
    private TextBox _uniformEnd = null!;
    private ComboBox _uniformProfile = null!;
    private Button _applyAllBtn = null!;
    private Button _addBtn = null!;
    private Button _removeBtn = null!;
    private Button _clearBtn = null!;
    private Button _startBtn = null!;
    private Button _stopBtn = null!;
    private Button _openDirBtn = null!;

    public MainForm(FfmpegLocator locator)
    {
        _locator = locator;
        _cut = new CutService(locator.FfmpegPath!, locator.FfprobePath!);

        _previewTimer = new System.Windows.Forms.Timer { Interval = 200 };
        _previewTimer.Tick += OnPreviewTick;

        Text = "FFmpeg 视频截取工具 v1.0";
        ClientSize = new Size(1080, 720);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 620);

        BuildUi();
        LoadAvailableEncoders();
        SwitchMode(single: true);
    }

    private void BuildUi()
    {
        var modePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            Padding = new Padding(8, 6, 0, 0),
        };
        _singleRadio = new RadioButton { Text = "单个视频", Checked = true, AutoSize = true };
        _batchRadio = new RadioButton { Text = "批量截取", AutoSize = true };
        _singleRadio.CheckedChanged += (_, _) => SwitchMode(_singleRadio.Checked);
        modePanel.Controls.Add(_singleRadio);
        modePanel.Controls.Add(_batchRadio);

        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel2,
            SplitterDistance = 620,
        };

        _split.Panel1.Controls.Add(BuildLeftPanel());
        _split.Panel2.Controls.Add(BuildRightPanel());

        var buttonBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8, 8, 0, 0),
        };
        _startBtn = new Button { Text = "开始截取", Width = 90 };
        _stopBtn = new Button { Text = "停止", Width = 70, Enabled = false };
        _openDirBtn = new Button { Text = "打开输出目录", Width = 100 };
        _startBtn.Click += async (_, _) => await OnStartCutAsync();
        _stopBtn.Click += (_, _) => _cts?.Cancel();
        _openDirBtn.Click += (_, _) => OpenOutputDirectory();
        buttonBar.Controls.Add(_startBtn);
        buttonBar.Controls.Add(_stopBtn);
        buttonBar.Controls.Add(_openDirBtn);

        Controls.Add(_split);
        Controls.Add(modePanel);
        Controls.Add(buttonBar);
    }

    private Control BuildLeftPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var tlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
        };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        _singlePanel = BuildSourcePanel();
        tlp.Controls.Add(_singlePanel, 0, 0);
        tlp.Controls.Add(BuildPreviewPanel(), 0, 1);
        tlp.Controls.Add(BuildTimePanel(), 0, 2);
        tlp.Controls.Add(BuildInfoPanel(), 0, 3);
        tlp.Controls.Add(BuildEncodingPanel(), 0, 4);
        tlp.Controls.Add(BuildOutputPanel(), 0, 5);

        panel.Controls.Add(tlp);
        return panel;
    }

    private Panel BuildSourcePanel()
    {
        var p = new Panel { Dock = DockStyle.Fill };
        var lbl = new Label { Text = "源视频:", Left = 0, Top = 8, Width = 56 };
        _sourcePath = new TextBox { Left = 58, Top = 5, Width = 380, Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right };
        var browse = new Button { Text = "浏览...", Left = 446, Top = 3, Width = 70 };
        browse.Click += (_, _) => BrowseSource();
        p.Controls.Add(lbl);
        p.Controls.Add(_sourcePath);
        p.Controls.Add(browse);
        return p;
    }

    private Control BuildPreviewPanel()
    {
        var group = new GroupBox { Text = "视频预览", Dock = DockStyle.Fill };

        _videoView = new VideoView { Dock = DockStyle.Fill, BackColor = Color.Black };
        _timeline = new TimelineControl { Dock = DockStyle.Bottom, Height = 40 };
        _timeline.PositionClicked += OnPositionClicked;
        _timeline.RightClicked += OnTimelineRightClicked;
        _timeline.SelectionChanged += OnSelectionChanged;

        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, Padding = new Padding(4, 4, 0, 0) };
        _backBtn = new Button { Text = "◀◀", Width = 44 };
        _playBtn = new Button { Text = "▶", Width = 44 };
        _fwdBtn = new Button { Text = "▶▶", Width = 44 };
        _backBtn.Click += (_, _) => Step(-10);
        _playBtn.Click += (_, _) => TogglePlay();
        _fwdBtn.Click += (_, _) => Step(10);

        var volLbl = new Label { Text = "音量", AutoSize = true, Margin = new Padding(8, 8, 0, 0) };
        _volume = new TrackBar { Width = 110, Minimum = 0, Maximum = 100, Value = 80, TickStyle = TickStyle.None, Height = 26 };
        _volume.ValueChanged += (_, _) => { if (_player is not null) _player.Volume = _volume.Value; };

        var rateLbl = new Label { Text = "速度", AutoSize = true, Margin = new Padding(8, 8, 0, 0) };
        _rateCombo = new ComboBox { Width = 60, DropDownStyle = ComboBoxStyle.DropDownList };
        _rateCombo.Items.AddRange(new object[] { "0.5x", "1.0x", "1.5x", "2.0x" });
        _rateCombo.SelectedIndex = 1;
        _rateCombo.SelectedIndexChanged += (_, _) => { if (_player is not null) _player.SetRate(ParseRate(_rateCombo.Text)); };

        bar.Controls.Add(_backBtn);
        bar.Controls.Add(_playBtn);
        bar.Controls.Add(_fwdBtn);
        bar.Controls.Add(volLbl);
        bar.Controls.Add(_volume);
        bar.Controls.Add(rateLbl);
        bar.Controls.Add(_rateCombo);

        group.Controls.Add(_videoView);
        group.Controls.Add(_timeline);
        group.Controls.Add(bar);
        return group;
    }

    private Panel BuildTimePanel()
    {
        var p = new Panel { Dock = DockStyle.Fill };
        var l1 = new Label { Text = "起始:", Left = 0, Top = 9, Width = 40 };
        _startText = new TextBox { Left = 42, Top = 5, Width = 80 };
        var l2 = new Label { Text = "终止:", Left = 130, Top = 9, Width = 40 };
        _endText = new TextBox { Left = 172, Top = 5, Width = 80 };
        var snap = new Button { Text = "截图", Left = 262, Top = 3, Width = 60 };
        snap.Click += (_, _) => TakeScreenshot();
        _startText.Leave += (_, _) => ApplyTimeBoxes();
        _endText.Leave += (_, _) => ApplyTimeBoxes();
        p.Controls.Add(l1);
        p.Controls.Add(_startText);
        p.Controls.Add(l2);
        p.Controls.Add(_endText);
        p.Controls.Add(snap);
        return p;
    }

    private Control BuildInfoPanel()
    {
        var group = new GroupBox { Text = "视频信息", Dock = DockStyle.Fill };
        _infoLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Microsoft YaHei UI", 9f) };
        group.Controls.Add(_infoLabel);
        return group;
    }

    private Control BuildEncodingPanel()
    {
        var group = new GroupBox { Text = "编码设置", Dock = DockStyle.Fill };
        var lbl = new Label { Text = "编码:", Left = 8, Top = 20, Width = 40 };
        _profileCombo = new ComboBox { Left = 50, Top = 16, Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
        _profileCombo.SelectedIndexChanged += (_, _) => OnProfileChanged();
        group.Controls.Add(lbl);
        group.Controls.Add(_profileCombo);
        return group;
    }

    private Control BuildOutputPanel()
    {
        var group = new GroupBox { Text = "输出", Dock = DockStyle.Fill };
        _outputPath = new TextBox { Left = 8, Top = 20, Width = 430, Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right };
        var browse = new Button { Text = "浏览...", Left = 446, Top = 17, Width = 70 };
        browse.Click += (_, _) => BrowseOutput();
        group.Controls.Add(_outputPath);
        group.Controls.Add(browse);
        return group;
    }

    private Control BuildRightPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));

        tlp.Controls.Add(BuildUniformPanel(), 0, 0);
        tlp.Controls.Add(BuildListPanel(), 0, 1);
        tlp.Controls.Add(BuildListButtons(), 0, 2);
        tlp.Controls.Add(BuildProgressPanel(), 0, 3);

        panel.Controls.Add(tlp);
        return panel;
    }

    private Control BuildUniformPanel()
    {
        var group = new GroupBox { Text = "统一设置", Dock = DockStyle.Fill };
        _applyAllCheck = new CheckBox { Text = "应用到全部", Left = 8, Top = 22, AutoSize = true };
        var l1 = new Label { Text = "起始:", Left = 8, Top = 48, Width = 40 };
        _uniformStart = new TextBox { Left = 50, Top = 45, Width = 72 };
        var l2 = new Label { Text = "终止:", Left = 128, Top = 48, Width = 40 };
        _uniformEnd = new TextBox { Left = 170, Top = 45, Width = 72 };
        _uniformProfile = new ComboBox { Left = 8, Top = 70, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
        _applyAllBtn = new Button { Text = "应用", Left = 196, Top = 69, Width = 60 };
        _applyAllBtn.Click += (_, _) => ApplyToAll();
        group.Controls.Add(_applyAllCheck);
        group.Controls.Add(l1);
        group.Controls.Add(_uniformStart);
        group.Controls.Add(l2);
        group.Controls.Add(_uniformEnd);
        group.Controls.Add(_uniformProfile);
        group.Controls.Add(_applyAllBtn);
        return group;
    }

    private Control BuildListPanel()
    {
        _listView = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false };
        _listView.Columns.Add("文件名", 180);
        _listView.Columns.Add("分辨率", 80);
        _listView.Columns.Add("编码", 70);
        _listView.Columns.Add("起止时间", 150);
        _listView.Columns.Add("状态", 60);
        _listView.DoubleClick += (_, _) => OnListDoubleClick();
        return _listView;
    }

    private Control BuildListButtons()
    {
        var p = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 0) };
        _addBtn = new Button { Text = "添加文件", Width = 80 };
        _removeBtn = new Button { Text = "移除选中", Width = 80 };
        _clearBtn = new Button { Text = "清空", Width = 70 };
        _addBtn.Click += (_, _) => AddFiles();
        _removeBtn.Click += (_, _) => RemoveSelected();
        _clearBtn.Click += (_, _) => ClearList();
        p.Controls.Add(_addBtn);
        p.Controls.Add(_removeBtn);
        p.Controls.Add(_clearBtn);
        return p;
    }

    private Control BuildProgressPanel()
    {
        var group = new GroupBox { Text = "处理进度", Dock = DockStyle.Fill };
        _progressBar = new ProgressBar { Left = 8, Top = 22, Width = 300, Height = 18, Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right };
        _overallLabel = new Label { Text = "就绪", Left = 8, Top = 46, AutoSize = true };
        _progressLabel = new Label { Text = "", Left = 8, Top = 66, AutoSize = true };
        _errorLink = new LinkLabel { Text = "", Left = 8, Top = 84, AutoSize = true, Visible = false };
        _errorLink.LinkClicked += (_, _) => ShowErrors();
        group.Controls.Add(_progressBar);
        group.Controls.Add(_overallLabel);
        group.Controls.Add(_progressLabel);
        group.Controls.Add(_errorLink);
        return group;
    }

    private void LoadAvailableEncoders()
    {
        try { _availableEncoders = _cut.GetAvailableVideoEncoders(); }
        catch { _availableEncoders = new HashSet<string>(StringComparer.OrdinalIgnoreCase); }

        var profiles = EncodeProfile.All
            .Select(p => p.IsCopy || _availableEncoders.Contains(p.VideoCodec)
                ? p
                : p with { Name = p.Name + "（不可用）" })
            .ToList();

        _profileCombo.DataSource = profiles;
        _profileCombo.DisplayMember = "Name";

        _uniformProfile.DataSource = new List<EncodeProfile>(profiles);
        _uniformProfile.DisplayMember = "Name";
    }

    private void SwitchMode(bool single)
    {
        _split.Panel2Collapsed = single;
        _singlePanel.Visible = single;
        if (single)
            _outputPath.Text = _currentVideoPath is null ? "" : BuildSingleOutputName();
        else
            _outputPath.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
    }
}
