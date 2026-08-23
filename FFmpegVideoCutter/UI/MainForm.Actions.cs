using System.Diagnostics;
using FFmpegVideoCutter.Models;
using FFmpegVideoCutter.Utils;
using LibVLCSharp.Shared;

namespace FFmpegVideoCutter.UI;

public sealed partial class MainForm
{
    private Media? _currentMedia;

    private async Task OnStartCutAsync()
    {
        if (_processing) return;
        if (_batchRadio.Checked) await RunBatchAsync();
        else await RunSingleAsync();
    }

    private async Task RunSingleAsync()
    {
        if (_currentVideoPath is null) { Warn("请先选择源视频"); return; }

        var start = TimeSpan.FromSeconds(_timeline.Start);
        var end = TimeSpan.FromSeconds(_timeline.End);
        if (!TimeHelper.IsValidRange(start, end, _duration)) { Warn("时间范围无效"); return; }

        var profile = GetSelectedProfile();
        if (!profile.IsCopy && !_availableEncoders.Contains(profile.VideoCodec))
        { Warn($"编码器 {profile.VideoCodec} 不可用"); return; }

        var outPath = _outputPath.Text.Trim();
        if (string.IsNullOrEmpty(outPath)) { Warn("请指定输出路径"); return; }

        var sourceCodec = NormalizeCodec(_currentVideoCodec);
        var targetCodec = NormalizeCodec(profile.VideoCodec);
        var forceReencode = false;
        if (!profile.IsCopy && sourceCodec == targetCodec)
        {
            var r = MessageBox.Show(
                $"检测到该文件已是 {targetCodec} 编码，是否直接复制以加快速度？",
                "智能提示",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (r == DialogResult.Yes) profile = EncodeProfile.Copy;
            else forceReencode = true;
        }

        var job = new CutJob
        {
            InputPath = _currentVideoPath,
            OutputPath = outPath,
            Start = start,
            End = end,
            Profile = profile,
            ForceReencode = forceReencode,
        };

        SetProcessing(true);
        _cts = new CancellationTokenSource();
        try
        {
            var result = await _cut.CutAsync(job,
                new Progress<CutProgress>(p => _progressBar.Value = (int)p.Percent),
                _cts.Token);
            MessageBox.Show(result.Success ? "截取完成" : $"截取失败：\n{result.Error}",
                "结果", MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }
        catch (OperationCanceledException) { }
        finally { SetProcessing(false); }
    }

    private async Task RunBatchAsync()
    {
        if (_batchItems.Count == 0) { Warn("请先添加视频文件"); return; }

        var profile = GetSelectedProfile();
        if (!profile.IsCopy && !_availableEncoders.Contains(profile.VideoCodec))
        { Warn($"编码器 {profile.VideoCodec} 不可用"); return; }

        var outDir = _outputPath.Text.Trim();
        if (string.IsNullOrEmpty(outDir)) { Warn("请指定输出目录"); return; }

        try { Directory.CreateDirectory(outDir); }
        catch { Warn("无法创建输出目录"); return; }

        SetProcessing(true);
        _errors.Clear();
        UpdateErrorLink();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            var total = _batchItems.Count;
            var done = 0;
            foreach (var item in _batchItems)
            {
                token.ThrowIfCancellationRequested();

                if (!TimeHelper.IsValidRange(item.StartTime, item.EndTime, item.Duration))
                {
                    item.Status = VideoStatus.Failed;
                    item.ErrorMessage = "时间范围无效";
                    _errors.Add(new CutError { FileName = item.FileName, ErrorCode = 1, Time = DateTime.Now, Message = "时间范围无效" });
                    UpdateListView();
                    UpdateErrorLink();
                    continue;
                }

                var outName = Path.GetFileName(FileHelper.BuildOutputName(
                    item.FilePath, item.StartTime, item.EndTime, profile.Extension));
                var outPath = Path.Combine(outDir, outName);

                item.Status = VideoStatus.Processing;
                UpdateListView();
                _progressLabel.Text = item.FileName;
                _overallLabel.Text = $"{done}/{total} 文件";
                _progressBar.Value = 0;

                var result = await _cut.CutAsync(new CutJob
                {
                    InputPath = item.FilePath,
                    OutputPath = outPath,
                    Start = item.StartTime,
                    End = item.EndTime,
                    Profile = profile,
                }, new Progress<CutProgress>(p => _progressBar.Value = (int)p.Percent), token);

                if (result.Success)
                {
                    item.Status = VideoStatus.Completed;
                }
                else
                {
                    item.Status = VideoStatus.Failed;
                    item.ErrorMessage = result.Error;
                    _errors.Add(new CutError { FileName = item.FileName, ErrorCode = result.ExitCode, Time = DateTime.Now, Message = result.Error });
                }

                done++;
                UpdateListView();
                UpdateErrorLink();
            }

            var ok = _batchItems.Count(x => x.Status == VideoStatus.Completed);
            _overallLabel.Text = $"完成 {ok}/{total}";
            _progressBar.Value = 100;
        }
        catch (OperationCanceledException)
        {
            _overallLabel.Text = "已停止";
        }
        finally
        {
            SetProcessing(false);
        }
    }

    private void SetProcessing(bool processing)
    {
        _processing = processing;
        _startBtn.Enabled = !processing;
        _stopBtn.Enabled = processing;
        _addBtn.Enabled = !processing;
        _removeBtn.Enabled = !processing;
        _clearBtn.Enabled = !processing;
        _applyAllBtn.Enabled = !processing;
        _singleRadio.Enabled = !processing;
        _batchRadio.Enabled = !processing;
        _profileCombo.Enabled = !processing;
        _uniformProfile.Enabled = !processing;
        if (processing) _progressBar.Value = 0;
    }

    private void BrowseSource()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "视频文件|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm;*.ts;*.m4v|所有文件|*.*",
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _sourcePath.Text = dlg.FileName;
            LoadSource(dlg.FileName);
        }
    }

    private void LoadSource(string path)
    {
        try
        {
            var info = _cut.GetInfo(path);
            _currentVideoPath = path;
            _currentVideoCodec = info.VideoCodec;
            _duration = info.Duration;
            _previewItem = null;
            _timeline.Duration = _duration;
            _timeline.SetSelection(0, _duration);
            _startText.Text = TimeHelper.Format(TimeSpan.Zero);
            _endText.Text = TimeHelper.Format(TimeSpan.FromSeconds(_duration));
            UpdateInfoLabel(info);
            _outputPath.Text = BuildSingleOutputName();
            LoadPreview(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show("读取视频信息失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateInfoLabel(VideoInfo info)
    {
        _infoLabel.Text =
            $"分辨率: {info.Resolution}  帧率: {info.FrameRateText}  时长: {info.DurationText}\n" +
            $"编码: {info.VideoCodec}  比特率: {info.BitRateText}  大小: {info.SizeText}\n" +
            $"音频: {(info.HasAudio ? $"{info.AudioCodec} {info.AudioSampleRate}Hz {info.Channels}ch" : "无")}";
    }

    private void BrowseOutput()
    {
        if (_batchRadio.Checked)
        {
            using var dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog() == DialogResult.OK) _outputPath.Text = dlg.SelectedPath;
        }
        else
        {
            var ext = GetSelectedProfile().Extension;
            using var dlg = new SaveFileDialog
            {
                Filter = $"{ext} 文件|*{ext}|所有文件|*.*",
                FileName = Path.GetFileName(_outputPath.Text),
            };
            if (dlg.ShowDialog() == DialogResult.OK) _outputPath.Text = dlg.FileName;
        }
    }

    private void OpenOutputDirectory()
    {
        var path = _outputPath.Text.Trim();
        if (string.IsNullOrEmpty(path)) return;
        var target = _batchRadio.Checked ? path : Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(target)) return;
        try
        {
            Directory.CreateDirectory(target);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{target}\"") { UseShellExecute = true });
        }
        catch { }
    }

    private string BuildSingleOutputName()
    {
        if (_currentVideoPath is null) return "";
        return FileHelper.BuildOutputName(
            _currentVideoPath,
            TimeSpan.FromSeconds(_timeline.Start),
            TimeSpan.FromSeconds(_timeline.End),
            GetSelectedProfile().Extension);
    }

    private void OnProfileChanged()
    {
        if (!_batchRadio.Checked && _currentVideoPath is not null)
            _outputPath.Text = BuildSingleOutputName();
    }

    private EncodeProfile GetSelectedProfile() =>
        _profileCombo.SelectedItem is EncodeProfile p ? p : EncodeProfile.Copy;

    private static string NormalizeCodec(string codec) => codec switch
    {
        "libx265" => "hevc",
        "libx264" => "h264",
        "libvpx-vp9" => "vp9",
        "libsvtav1" => "av1",
        _ => codec,
    };

    private void EnsurePlayer()
    {
        if (_player is not null) return;
        try
        {
            _libVLC = new LibVLC();
            _player = new MediaPlayer(_libVLC);
            _videoView.MediaPlayer = _player;
        }
        catch (Exception ex)
        {
            MessageBox.Show("视频预览初始化失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void LoadPreview(string path)
    {
        EnsurePlayer();
        if (_player is null || _libVLC is null) return;
        try
        {
            _player.Stop();
            _currentMedia?.Dispose();
            _currentMedia = new Media(_libVLC, path, FromType.FromPath);
            _player.Media = _currentMedia;
            _player.Play();
            _player.Volume = _volume.Value;
            _player.SetRate(ParseRate(_rateCombo.Text));
            _playBtn.Text = "❚❚";
            _previewTimer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show("预览播放失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnPreviewTick(object? sender, EventArgs e)
    {
        if (_player is null) return;
        var t = _player.Time;
        if (t >= 0) _timeline.Position = t / 1000.0;
    }

    private void TogglePlay()
    {
        if (_player is null) return;
        if (_player.IsPlaying)
        {
            _player.Pause();
            _playBtn.Text = "▶";
        }
        else
        {
            _player.Play();
            _playBtn.Text = "❚❚";
        }
    }

    private void Step(int deltaSeconds)
    {
        if (_player is null) return;
        var length = _player.Length > 0 ? _player.Length : (long)(_duration * 1000);
        var cur = _player.Time < 0 ? 0 : _player.Time;
        var next = Math.Clamp(cur + deltaSeconds * 1000L, 0, length);
        _player.Time = next;
        _timeline.Position = next / 1000.0;
    }

    private void OnPositionClicked(object? sender, TimelineEventArgs e)
    {
        if (_player is not null) _player.Time = (long)(e.Seconds * 1000);
    }

    private void OnSelectionChanged(object? sender, EventArgs e) => SyncTimesFromTimeline();

    private void OnTimelineRightClicked(object? sender, TimelineEventArgs e)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("设为起始点", null, (_, _) => { _timeline.Start = e.Seconds; SyncTimesFromTimeline(); });
        menu.Items.Add("设为终止点", null, (_, _) => { _timeline.End = e.Seconds; SyncTimesFromTimeline(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("从此到尾", null, (_, _) => { _timeline.Start = e.Seconds; _timeline.End = _duration; SyncTimesFromTimeline(); });
        menu.Items.Add("从头到此", null, (_, _) => { _timeline.Start = 0; _timeline.End = e.Seconds; SyncTimesFromTimeline(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("删除标记点", null, (_, _) => { _timeline.SetSelection(0, _duration); SyncTimesFromTimeline(); });
        menu.Show(_timeline, _timeline.PointToClient(Cursor.Position));
    }

    private void SyncTimesFromTimeline()
    {
        _startText.Text = TimeHelper.Format(TimeSpan.FromSeconds(_timeline.Start));
        _endText.Text = TimeHelper.Format(TimeSpan.FromSeconds(_timeline.End));

        if (_batchRadio.Checked && _previewItem is not null)
        {
            _previewItem.StartTime = TimeSpan.FromSeconds(_timeline.Start);
            _previewItem.EndTime = TimeSpan.FromSeconds(_timeline.End);
            UpdateListView();
        }
        else if (!_batchRadio.Checked && _currentVideoPath is not null)
        {
            _outputPath.Text = BuildSingleOutputName();
        }
    }

    private void ApplyTimeBoxes()
    {
        if (!TimeHelper.TryParse(_startText.Text, out var s)) return;
        if (!TimeHelper.TryParse(_endText.Text, out var e)) return;
        if (e <= s) return;
        _timeline.SetSelection(s.TotalSeconds, Math.Min(e.TotalSeconds, _duration));
        SyncTimesFromTimeline();
    }

    private void TakeScreenshot()
    {
        if (_currentVideoPath is null || _locator.FfmpegPath is null) return;
        var dir = Path.GetDirectoryName(_currentVideoPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(_currentVideoPath);
        var outPath = Path.Combine(dir, $"{name}_snapshot_{DateTime.Now:HHmmss}.png");
        var pos = TimeHelper.ToFfmpegTime(TimeSpan.FromSeconds(_timeline.Position));

        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = _locator.FfmpegPath,
                Arguments = $"-y -ss {pos} -i \"{_currentVideoPath}\" -frames:v 1 -q:v 2 \"{outPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            p?.WaitForExit();
            var ok = p?.ExitCode == 0;
            MessageBox.Show(ok ? $"截图已保存：{outPath}" : "截图失败",
                "截图", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show("截图失败：" + ex.Message, "截图", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static float ParseRate(string text) =>
        float.TryParse(text.TrimEnd('x'), out var r) ? r : 1f;

    private void AddFiles()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "视频文件|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm;*.ts;*.m4v|所有文件|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        Cursor = Cursors.WaitCursor;
        try
        {
            foreach (var path in dlg.FileNames)
            {
                try
                {
                    var info = _cut.GetInfo(path);
                    _batchItems.Add(new VideoItem
                    {
                        FilePath = path,
                        Width = info.Width,
                        Height = info.Height,
                        VideoCodec = info.VideoCodec,
                        BitRate = info.BitRate,
                        Duration = info.Duration,
                        AudioCodec = info.AudioCodec,
                        StartTime = TimeSpan.Zero,
                        EndTime = TimeSpan.FromSeconds(info.Duration),
                    });
                }
                catch { }
            }
            UpdateListView();
        }
        finally { Cursor = Cursors.Default; }
    }

    private void RemoveSelected()
    {
        foreach (ListViewItem lvi in _listView.SelectedItems)
            if (lvi.Tag is VideoItem item) _batchItems.Remove(item);
        UpdateListView();
    }

    private void ClearList()
    {
        _batchItems.Clear();
        UpdateListView();
    }

    private void UpdateListView()
    {
        _listView.BeginUpdate();
        _listView.Items.Clear();
        foreach (var item in _batchItems)
        {
            var lvi = new ListViewItem(item.FileName);
            lvi.SubItems.Add(item.ResolutionText);
            lvi.SubItems.Add(item.VideoCodec);
            lvi.SubItems.Add(item.RangeText);
            lvi.SubItems.Add(item.StatusText);
            lvi.Tag = item;
            _listView.Items.Add(lvi);
        }
        _listView.EndUpdate();
    }

    private void OnListDoubleClick()
    {
        if (_listView.SelectedItems.Count == 0) return;
        if (_listView.SelectedItems[0].Tag is VideoItem item)
            PreviewBatchItem(item);
    }

    private void PreviewBatchItem(VideoItem item)
    {
        _previewItem = item;
        _duration = item.Duration;
        _currentVideoPath = item.FilePath;
        _currentVideoCodec = item.VideoCodec;
        _timeline.Duration = _duration;
        _timeline.SetSelection(item.StartTime.TotalSeconds, item.EndTime.TotalSeconds);
        _startText.Text = TimeHelper.Format(item.StartTime);
        _endText.Text = TimeHelper.Format(item.EndTime);
        _infoLabel.Text =
            $"分辨率: {item.ResolutionText}  编码: {item.VideoCodec}  时长: {TimeSpan.FromSeconds(item.Duration):hh\\:mm\\:ss}";
        LoadPreview(item.FilePath);
    }

    private void ApplyToAll()
    {
        if (_batchItems.Count == 0) { Warn("列表为空"); return; }
        if (!TimeHelper.TryParse(_uniformStart.Text, out var s) || !TimeHelper.TryParse(_uniformEnd.Text, out var e))
        { Warn("统一设置的起止时间格式无效"); return; }
        if (s >= e) { Warn("起始时间必须小于终止时间"); return; }

        foreach (var item in _batchItems)
        {
            item.StartTime = s;
            item.EndTime = e;
            item.Status = VideoStatus.Pending;
        }

        if (_uniformProfile.SelectedItem is EncodeProfile up)
            _profileCombo.SelectedItem = up;

        UpdateListView();
    }

    private void ShowErrors()
    {
        if (_errors.Count == 0) return;
        using var f = new ErrorDetailsForm(_errors);
        f.ShowDialog(this);
    }

    private void UpdateErrorLink()
    {
        var failed = _errors.Count;
        _errorLink.Text = $"[{failed} 失败]";
        _errorLink.Visible = failed > 0;
    }

    private static void Warn(string msg) =>
        MessageBox.Show(msg, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
