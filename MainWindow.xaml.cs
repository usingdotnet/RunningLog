using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using NLog;
using CliWrap;
using Tomlet;
using CliWrap.Buffered;
using System.Diagnostics;
using System.Reflection;
using ScottPlot;
using ScottPlot.Plottables;
using System.Data.SQLite;
using Dapper;
using Dapper.Contrib;
using Dapper.Contrib.Extensions;

namespace RunningLog;

public enum ChineseDayOfWeek
{
    Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday
}

public partial class MainWindow : Window
{
    private int _year = DateTime.Now.Year;
    private Dictionary<DateTime, List<RunData>> _data = new Dictionary<DateTime, List<RunData>>();
    private string _dataDir = "";
    private string _repoDir = "";
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private bool _isDarkMode = true;
    private readonly DateTime _today = DateTime.Now.Date;

    private const int CellSize = 12;
    private const int CellPadding = 2;
    private const int FixedWidth = 790;
    private const int LabelHeight = 30;
    private const int LeftMargin = 30;
    private const int MonthLabelHeight = 20;
    private const int YearLabelHeight = 30;
    private const int HeaderHeight = YearLabelHeight + 10;
    private AppConfig _config;
    private string ConfigFile = "config.toml";
    private int _lastInsertedId = 0;

    private void LoadConfig()
    {
        string exePath = Assembly.GetExecutingAssembly().Location;
        string exeDirectory = Path.GetDirectoryName(exePath);
        ConfigFile = Path.Combine(exeDirectory, ConfigFile);
        if (File.Exists(ConfigFile))
        {
            _logger.Debug($"配置文件 {ConfigFile}");
            string tomlString = File.ReadAllText(ConfigFile);
            _config = TomletMain.To<AppConfig>(tomlString);
        }
        else
        {
            _config = new AppConfig();
            SaveConfig();
        }

        _isDarkMode = _config.IsDarkMode;
        _repoDir = _config.RepoDir;
        _dataDir = Path.Combine(_repoDir, "data");
    }

    private void SaveConfig()
    {
        _config.IsDarkMode = _isDarkMode;
        string tomlString = TomletMain.TomlStringFrom(_config);
        File.WriteAllText(ConfigFile, tomlString);
    }

    public MainWindow()
    {
        InitializeComponent();
        LoadConfig();
        InitializeWindowPosition();
        LoadData();
        UpdateYearButtonsVisibility();
        SetGitRelatedButtonsVisibility();
    }

    private void SetGitRelatedButtonsVisibility()
    {
        var visibility = IsGitRepository() ? Visibility.Visible : Visibility.Collapsed;
        BtnRevert.Visibility = visibility;
        BtnPublish.Visibility = visibility;
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {

    }

    private void InitializeWindowPosition()
    {
        double screenHeight = SystemParameters.PrimaryScreenHeight;
        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double taskbarHeight = SystemParameters.PrimaryScreenHeight - SystemParameters.WorkArea.Height;

        Top = screenHeight - Height - taskbarHeight + 7;
        Left = (screenWidth - Width) / 2;
    }

    private void LoadData()
    {
        string dbPath = Path.Combine(_dataDir, "RunningLog.db");
        using (var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
        {
            connection.Open();
            var startDate = new DateTime(_year, 1, 1);
            var endDate = new DateTime(_year + 1, 1, 1); // 下一年的1月1日
            _data = connection.Query<RunData>("SELECT * FROM RunData WHERE Date >= @StartDate AND Date < @EndDate", new { StartDate = startDate.ToString("yyyy-MM-dd"), EndDate = endDate.ToString("yyyy-MM-dd") })
                .GroupBy(r => DateTime.Parse(r.Date))
                .ToDictionary(g => g.Key, g => g.ToList());
        }
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        DrawBackground(canvas);
        DrawHeader(canvas);
        DrawHeatmap(canvas);
        DrawLastRunInfo(canvas);
        DrawLegend(canvas);

        // 绘制每月跑量图表
        var monthlyDistances = GetMonthlyDistances();
        DrawMonthlyDistancePlot(monthlyDistances);
        SavePng(e.Surface);
    }

    private void DrawBackground(SKCanvas canvas)
    {
        var backgroundColor = _isDarkMode ? new SKColor(34, 34, 34) : SKColors.White;
        canvas.Clear(backgroundColor);
    }

    private void DrawHeader(SKCanvas canvas)
    {
        var textColor = _isDarkMode ? SKColors.White : new SKColor(34, 34, 34);

        var yearPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            TextSize = 20,
            Typeface = SKTypeface.FromFamilyName("Microsoft YaHei", SKFontStyle.Bold),
            Color = textColor
        };

        var statsPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            TextSize = 16,
            Typeface = SKTypeface.FromFamilyName("Microsoft YaHei"),
            Color = textColor,
        };

        string yearText = $"{_year}";
        canvas.DrawText(yearText, LeftMargin, YearLabelHeight - 5, yearPaint);

        // 绘制统计信息
        int runningDays = _data.Count(entry => entry.Value.Sum(r => r.Distance) > 0);
        double totalDistance = _data.Values.SelectMany(distances => distances).Sum(r => r.Distance);
        string statsText = $"{runningDays} days, {totalDistance:F2} km";
        var statsTextWidth = statsPaint.MeasureText(statsText);
        canvas.DrawText(statsText, FixedWidth - statsTextWidth - 20, YearLabelHeight - 5, statsPaint);
    }

    private float CalculateHeatmapBottom()
    {
        var startDate = new DateTime(_year, 1, 1);
        int totalDays = DateTime.IsLeapYear(_year) ? 366 : 365;
        int totalRows = 7; // 一周7天
        return HeaderHeight + LabelHeight + totalRows * (CellSize + CellPadding) + CellPadding;
    }

    private void DrawLastRunInfo(SKCanvas canvas)
    {
        var textColor = _isDarkMode ? SKColors.White : new SKColor(34, 34, 34);

        var lastRunPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            TextSize = 16,
            Typeface = SKTypeface.FromFamilyName("Microsoft YaHei"),
            Color = textColor,
        };

        var lastRun = _data.OrderByDescending(x => x.Key).FirstOrDefault();
        if (lastRun.Key != default)
        {
            string lastRunText = $"Latest：{lastRun.Key.ToShortDateString()}, {lastRun.Value.Sum(r => r.Distance):F2} km";
            var lastRunTextWidth = lastRunPaint.MeasureText(lastRunText);
            float heatmapBottom = CalculateHeatmapBottom();
            canvas.DrawText(lastRunText, LeftMargin, heatmapBottom + 30, lastRunPaint);
        }
    }

    private void DrawLegend(SKCanvas canvas)
    {
        float legendY = CalculateHeatmapBottom() + 20; // 图例位置在热力图下方20像素
        float legendX = FixedWidth - 11 * CellSize - 60; // 左移40像素，为"10km"文字腾出空间

        var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var textPaint = new SKPaint
        {
            IsAntialias = true,
            Color = _isDarkMode ? SKColors.White : SKColors.Black,
            TextSize = 12,
            Typeface = SKTypeface.FromFamilyName("Microsoft YaHei")
        };

        for (int i = 0; i <= 10; i++)
        {
            float x = legendX + i * CellSize;
            SKColor color = GetDayColor(i);
            paint.Color = color;

            canvas.DrawRect(new SKRect(x, legendY, x + CellSize, legendY + CellSize), paint);
        }

        float textOffsetY = textPaint.TextSize / 2;
        float textOffsetX = 5;

        // 绘制图例文字
        canvas.DrawText("0 km", legendX + CellSize / 2 - textPaint.MeasureText("0km") / 2 - 2 * CellSize, legendY + CellSize / 2 + textOffsetY, textPaint);
        canvas.DrawText("5 km", legendX + 6 * CellSize - textPaint.MeasureText("5km") / 2 + 4, legendY + CellSize + textOffsetY + textOffsetX, textPaint);
        canvas.DrawText("10 km", legendX + 11 * CellSize + textOffsetX, legendY + CellSize / 2 + textOffsetY, textPaint);
    }

    private void DrawHeatmap(SKCanvas canvas)
    {
        var textColor = _isDarkMode ? SKColors.White : new SKColor(34, 34, 34);

        var labelPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Typeface = SKTypeface.FromFamilyName("Microsoft YaHei"),
            Color = textColor
        };

        DrawMonthLabels(canvas, labelPaint);
        DrawHeatmapCells(canvas, labelPaint);
    }

    private void DrawMonthLabels(SKCanvas canvas, SKPaint labelPaint)
    {
        var startDate = new DateTime(_year, 1, 1);
        string[] monthAbbreviations = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
        ];

        labelPaint.TextSize = 12;

        for (int month = 1; month <= 12; month++)
        {
            var monthStart = new DateTime(_year, month, 1);
            int daysBeforeMonth = (monthStart - startDate).Days;
            int monthStartCol = daysBeforeMonth / 7;
            int monthOffsetX = monthStartCol * (CellSize + CellPadding) + LeftMargin;

            // 调整Y坐标,使标签更靠近热力图
            float yPosition = HeaderHeight + MonthLabelHeight + 5;

            canvas.DrawText(monthAbbreviations[month - 1], monthOffsetX, yPosition, labelPaint);
        }
    }

    private void DrawHeatmapCells(SKCanvas canvas, SKPaint labelPaint)
    {
        var startDate = new DateTime(_year, 1, 1);
        int totalDays = DateTime.IsLeapYear(_year) ? 366 : 365;
        int totalCols = (int)Math.Ceiling((totalDays + (int)GetChineseDayOfWeek(startDate.DayOfWeek)) / 7.0);

        for (int col = 0; col < totalCols; col++)
        {
            for (int row = 0; row < 7; row++)
            {
                int index = col * 7 + row - (int)GetChineseDayOfWeek(startDate.DayOfWeek);
                if (index < 0 || index >= totalDays) continue;

                var date = startDate.AddDays(index);
                double totalDistance = _data.TryGetValue(date, out List<RunData> runs) ? runs.Sum(r => r.Distance) : 0;

                SKColor color = GetDayColor(totalDistance);

                labelPaint.Color = color;

                var rect = new SKRect(
                    col * (CellSize + CellPadding) + LeftMargin,
                    row * (CellSize + CellPadding) + HeaderHeight + LabelHeight,
                    col * (CellSize + CellPadding) + CellSize + LeftMargin,
                    row * (CellSize + CellPadding) + CellSize + HeaderHeight + LabelHeight
                );

                canvas.DrawRect(rect, labelPaint);
            }
        }
    }

    private void SavePng(SKSurface surface)
    {
        string dbFilePath = Path.Combine(_dataDir, "RunningLog.db");
        string pngFilePath = Path.Combine(_dataDir, $"{_year}.png");

        if (File.Exists(dbFilePath) && File.Exists(pngFilePath))
        {
            DateTime dbLastModified = File.GetLastWriteTime(dbFilePath);
            DateTime pngLastModified = File.GetLastWriteTime(pngFilePath);

            if (dbLastModified <= pngLastModified)
            {
                // db文件未修改或PNG文件比db文件新,无需重新生成PNG
                //return;
            }
        }

        else if (!File.Exists(dbFilePath))
        {
            // db文件不存在,无法生成PNG
            return;
        }

        // 生成PNG的代码
        string png = Path.Combine(_dataDir, $"{_year}.png");
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 80);
        using var stream = File.OpenWrite(png);
        data.SaveTo(stream);
    }

    private SKColor GetDayColor(double distance)
    {
        if (distance == 0)
        {
            return _isDarkMode ? new SKColor(68, 68, 68) : SKColors.LightGray;
        }

        distance = Math.Clamp(distance, 1.0, 10.0);
        double normalizedDistance = Math.Pow((distance - 1.0) / 9.0, 0.9);

        SKColor startColor = new SKColor(255, 255, 0);  // 黄色
        SKColor endColor = new SKColor(255, 0, 0);      // 红色

        return new SKColor(
            (byte)(startColor.Red + (endColor.Red - startColor.Red) * normalizedDistance),
            (byte)(startColor.Green + (endColor.Green - startColor.Green) * normalizedDistance),
            (byte)(startColor.Blue + (endColor.Blue - startColor.Blue) * normalizedDistance)
        );
    }

    private static ChineseDayOfWeek GetChineseDayOfWeek(DayOfWeek dayOfWeek)
        => (ChineseDayOfWeek)(((int)dayOfWeek + 6) % 7);

    private void OnYearButtonClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is Button button && int.TryParse(button.Content.ToString(), out int year))
        {
            _year = year;
            LoadData();
            skElement.InvalidateVisual();
            UpdateYearButtonsVisibility();
        }
    }

    private void BtnLightMode_OnClick(object sender, RoutedEventArgs e)
    {
        _isDarkMode = false;
        SaveConfig();
        skElement.InvalidateVisual();
    }

    private void BtnDarkMode_OnClick(object sender, RoutedEventArgs e)
    {
        _isDarkMode = true;
        SaveConfig();
        skElement.InvalidateVisual();
    }

    private void BtnOk_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ValidateInput(out DateTime selectedDate, out double distance, out string duration, out double heartRate, out string pace, out string notes))
        {
            ShowMessage("请输入有效的距离、时长、心率、配速和备注。", MessageType.Error);
            return;
        }

        _lastInsertedId = UpdateDataAndSave(selectedDate, distance, duration, heartRate, pace, notes);
        ShowMessage("添加完成。", MessageType.Success);

        // 添加成功后清空输入框
        TxtDistance.Text = string.Empty;
        TxtDuration.Text = string.Empty;
        TxtHeartRate.Text = string.Empty;
        TxtPace.Text = string.Empty;
        TxtNotes.Text = string.Empty;
    }

    private async void BtnRevert_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_lastInsertedId != 0)
            {
                await using (var connection = new SQLiteConnection($"Data Source={Path.Combine(_dataDir, "RunningLog.db")};Version=3;"))
                {
                    connection.Open();
                    var runDataToDelete = new RunData { Id = _lastInsertedId };
                    await connection.DeleteAsync(runDataToDelete);
                    SlideMessage.ShowMessage("成功删除最后添加的记录", MessageType.Success);
                    _lastInsertedId = 0; // 重置ID
                    LoadData();
                    skElement.InvalidateVisual();
                }
            }
            else
            {
                SlideMessage.ShowMessage("没有可删除的记录", MessageType.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"删除操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnPublish_OnClick(object sender, RoutedEventArgs e)
    {
        if (!IsGitRepository())
        {
            ShowMessage("当前目录不是Git仓库。", MessageType.Error);
            return;
        }

        try
        {
            // 检查是否有未提交的更改
            string status = await GetGitStatus();
            bool hasChanges = !string.IsNullOrWhiteSpace(status);

            // 检查是否有未推送的提交
            string unpushedCommits = await GetUnpushedCommits();
            bool hasUnpushedCommits = !string.IsNullOrWhiteSpace(unpushedCommits);

            if (!hasChanges && !hasUnpushedCommits)
            {
                ShowMessage("没有需要发布的更改或未推送的提交。", MessageType.Warning);
                return;
            }

            if (hasChanges)
            {
                var lastRun = _data.OrderByDescending(x => x.Key).FirstOrDefault();
                if (lastRun.Key != default)
                {
                    string date = lastRun.Key.Date.ToShortDateString();
                    await CommitChanges($"{date} 跑步 {lastRun.Value.Sum(r => r.Distance):F2} 公里");
                }
                else
                {
                    ShowMessage("没有可发布的跑步记录。", MessageType.Error);
                    return;
                }
            }

            // 推送所有提交
            await PushChanges();
            ShowMessage("成功发布更改。", MessageType.Success);
        }
        catch (Exception ex)
        {
            ShowMessage($"发布过程中出错: {ex.Message}", MessageType.Error);
        }
    }

    private async Task<string> GetUnpushedCommits()
    {
        var result = await Cli.Wrap("git")
            .WithArguments("log @{u}..HEAD --oneline")
            .WithWorkingDirectory(_repoDir)
            .ExecuteBufferedAsync();

        return result.StandardOutput.Trim();
    }

    private async Task CommitChanges(string message)
    {
        await ExecuteGitCommand($"commit -a -m \"{message}\"");
    }

    private async Task PushChanges()
    {
        await ExecuteGitCommand("push");
    }

    private bool ValidateInput(out DateTime selectedDate, out double distance, out string duration, out double heartRate, out string pace, out string notes)
    {
        selectedDate = default;
        distance = 0;
        duration = "";
        heartRate = 0;
        pace = "";
        notes = string.Empty;

        // 确保日期和距离有效
        if (!DpDate.SelectedDate.HasValue ||
            !double.TryParse(TxtDistance.Text, out distance) ||
            (selectedDate = DpDate.SelectedDate.Value).Year <= 0)
        {
            return false;
        }

        // 解析时长格式
        duration = ParseDuration(TxtDuration.Text);

        // 其他字段可以为空
        double.TryParse(TxtHeartRate.Text, out heartRate);
        pace = TxtPace.Text;
        notes = TxtNotes.Text;

        return true;
    }

    private string ParseDuration(string durationString)
    {
        if (string.IsNullOrEmpty(durationString))
        {
            return ""; // 如果输入为空，返回零时长
        }

        int hours = 0, minutes = 0, seconds = 0;

        // 使用正则表达式解析输入
        var match = System.Text.RegularExpressions.Regex.Match(durationString, @"(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s)?");
        if (match.Success)
        {
            if (match.Groups[1].Success) hours = int.Parse(match.Groups[1].Value);
            if (match.Groups[2].Success) minutes = int.Parse(match.Groups[2].Value);
            if (match.Groups[3].Success) seconds = int.Parse(match.Groups[3].Value);
        }

        return durationString;
    }

    private int UpdateDataAndSave(DateTime selectedDate, double distance, string duration, double heartRate, string pace, string notes)
    {
        LogDistanceChange(selectedDate, distance);
        if (!_data.ContainsKey(selectedDate))
        {
            _data[selectedDate] = new List<RunData>();
        }

        var runData = new RunData
        {
            Date = selectedDate.ToString("yyyy-MM-dd"),
            Distance = distance,
            Duration = duration,
            HeartRate = heartRate,
            Pace = pace,
            Notes = notes
        };

        using (var connection = new SQLiteConnection($"Data Source={Path.Combine(_dataDir, "RunningLog.db")};Version=3;"))
        {
            connection.Open();
            _lastInsertedId = (int)connection.Insert(runData); // 返回新插入记录的ID
        }

        _year = selectedDate.Year;
        LoadData();
        skElement.InvalidateVisual();
        return _lastInsertedId; // 返回ID
    }

    private void LogDistanceChange(DateTime selectedDate, double distance)
    {
        var d = selectedDate.ToString("yyyy-MM-dd");
        if (_data.TryGetValue(selectedDate, out List<RunData> values))
        {
            _logger.Debug($"日期 {d} 的距离由 {string.Join(", ", values.Select(r => r.Distance))} 添加了 {distance}");
        }
        else
        {
            _logger.Debug($"添加日期 {d} 的距离 {distance}");
        }
    }

    private async Task<string> GetGitStatus()
    {
        var result = await Cli.Wrap("git")
            .WithArguments("status --porcelain")
            .WithWorkingDirectory(_repoDir)
            .ExecuteBufferedAsync();

        return result.StandardOutput.Trim();
    }

    private async Task ExecuteGitCommand(string arguments)
    {
        await Cli.Wrap("git")
            .WithArguments(arguments)
            .WithWorkingDirectory(_repoDir)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(s => _logger.Debug(s)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(s => _logger.Debug(s)))
            .ExecuteAsync();
    }

    private bool IsGitRepository()
    {
        return Directory.Exists(Path.Combine(_repoDir, ".git"));
    }

    private void ShowMessage(string message, MessageType type)
    {
        SlideMessage.ShowMessage(message, type);
    }

    private void UpdateYearButtonsVisibility()
    {
        int currentYear = DateTime.Now.Year;
        foreach (var child in ((StackPanel)FindName("YearButtonsPanel")).Children)
        {
            if (child is Button button && int.TryParse(button.Content.ToString(), out int buttonYear))
            {
                button.Visibility = buttonYear <= currentYear ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {

    }

    private void BtnOpen_OnClick(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_dataDir))
        {
            Process.Start("explorer.exe", _dataDir);
        }
        else
        {
            ShowMessage("数据目录不存在", MessageType.Error);
        }
    }

    private double[] GetMonthlyDistances()
    {
        double[] monthlyDistances = new double[12];
        foreach (var entry in _data)
        {
            int month = entry.Key.Month - 1; // 0-based index
            monthlyDistances[month] += entry.Value.Sum(r => r.Distance);
        }
        return monthlyDistances;
    }

    private void DrawMonthlyDistancePlot(double[] monthlyDistances)
    {
        WpfPlot1.Plot.Clear();
        var plt = WpfPlot1.Plot;
        List<Bar> bars = new List<Bar>();
        foreach (var v in monthlyDistances.Index())
        {
            var bar = new Bar() { Position = v.Index + 1, Value = v.Item, Error = 0, FillColor = Colors.Orange };
            double v1 = Math.Round(bar.Value, 2);
            if (v1 > 0)
            {
                bar.Label = $"{v1.ToString()} km";
            }
            else
            {
                bar.Label = string.Empty;
            }

            bars.Add(bar);
        }

        var bp = plt.Add.Bars(bars);
        bp.ValueLabelStyle.Bold = false;
        bp.ValueLabelStyle.FontSize = 13;
        bp.ValueLabelStyle.OffsetY = 5;
        bp.ValueLabelStyle.AntiAliasBackground = true;
        bp.ValueLabelStyle.AntiAliasText = true;
        plt.Title("Monthly Running Distance", 15);
        plt.YLabel("Distance (km)", 15);
        plt.Axes.SetLimitsY(0, monthlyDistances.Max() * 1.25);
        Tick[] ticks =
        {
            new(1, "Jan"),
            new(2, "Feb"),
            new(3, "Mar"),
            new(4, "Apr"),
            new(5, "May"),
            new(6, "Jun"),
            new(7, "Jul"),
            new(8, "Aug"),
            new(9, "Sep"),
            new(10, "Oct"),
            new(11, "Nov"),
            new(12, "Dec"),
        };

        plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);
        plt.Axes.Bottom.MajorTickStyle.Length = 0;
        WpfPlot1.Plot.Font.Automatic();
        WpfPlot1.Refresh();
    }
}