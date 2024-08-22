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

namespace RunningLog;

public enum ChineseDayOfWeek
{
    Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday
}

public partial class MainWindow : Window
{
    private int _year = DateTime.Now.Year;
    private Dictionary<DateTime, List<double>> _data = new Dictionary<DateTime, List<double>>();
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
        _dataDir = Path.Combine(_repoDir,"data");
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
        InitializeTodayDistance();
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

    private void InitializeTodayDistance()
    {
        // 移除加载当天跑步距离的代码
        // if (_data.TryGetValue(_today, out List<double> distances))
        // {
        //     TxtDistance.Text = distances.Sum().ToString("F2");
        // }
    }

    private void LoadData()
    {
        var filePath = Path.Combine(_dataDir, $"{_year}.csv");
        _data = File.Exists(filePath) ? LoadDataFromCsv(filePath) : new Dictionary<DateTime, List<double>>();
    }

    private static Dictionary<DateTime, List<double>> LoadDataFromCsv(string filePath)
    {
        return File.ReadLines(filePath)
            .Select(line => line.Split(','))
            .Where(fields => fields.Length >= 2)
            .GroupBy(fields => DateTime.Parse(fields[0]))
            .ToDictionary(
                group => group.Key,
                group => group.Select(fields => double.Parse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture)).ToList()
            );
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        DrawBackground(canvas);
        DrawHeader(canvas);
        DrawHeatmap(canvas);
        DrawLastRunInfo(canvas);
        DrawLegend(canvas);
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
        int runningDays = _data.Count(entry => entry.Value.Sum() > 0);
        double totalDistance = _data.Values.SelectMany(distances => distances).Sum();
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
            string lastRunText = $"Latest：{lastRun.Key.ToShortDateString()}, {lastRun.Value.Sum():F2} km";
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
        string[] monthAbbreviations = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        // 减小字体大小
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
                double totalDistance = _data.TryGetValue(date, out List<double> distances) ? distances.Sum() : 0;

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
        string csvFilePath = Path.Combine(_dataDir, $"{_year}.csv");
        string pngFilePath = Path.Combine(_dataDir, $"{_year}.png");

        if (File.Exists(csvFilePath) && File.Exists(pngFilePath))
        {
            DateTime csvLastModified = File.GetLastWriteTime(csvFilePath);
            DateTime pngLastModified = File.GetLastWriteTime(pngFilePath);

            if (csvLastModified <= pngLastModified)
            {
                // CSV文件未修改或PNG文件比CSV文件新,无需重新生成PNG
                //return;
            }
        }

        else if (!File.Exists(csvFilePath))
        {
            // CSV文件不存在,无法生成PNG
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
        if (!ValidateInput(out DateTime selectedDate, out double distance))
        {
            ShowMessage("请输入有效的距离和日期。",MessageType.Error);
            return;
        }

        UpdateDataAndSave(selectedDate, distance);
        ShowMessage("添加完成。", MessageType.Success);
        
        // 添加成功后清空 TxtDistance
        TxtDistance.Text = string.Empty;
    }

    private async void BtnRevert_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var status = await GetGitStatus();
            if (!string.IsNullOrEmpty(status))
            {
                await ExecuteGitCommand("reset --hard HEAD");
                SlideMessage.ShowMessage("成功撤销所有修改", MessageType.Success);
                LoadData();
                skElement.InvalidateVisual();
            }
            else
            {
                SlideMessage.ShowMessage("没有需要撤销的修改", MessageType.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"重置操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    await CommitChanges($"{date} 跑步 {lastRun.Value.Sum():F2} 公里");
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

    private bool ValidateInput(out DateTime selectedDate, out double distance)
    {
        selectedDate = default;
        distance = 0;
        return DpDate.SelectedDate.HasValue &&
               double.TryParse(TxtDistance.Text, out distance) &&
               distance > 0 &&
               (selectedDate = DpDate.SelectedDate.Value).Year > 0;
    }

    private void UpdateDataAndSave(DateTime selectedDate, double distance)
    {
        if (selectedDate.Year != _year)
        {
            _year = selectedDate.Year;
            LoadData();
        }

        LogDistanceChange(selectedDate, distance);
        if (!_data.ContainsKey(selectedDate))
        {
            _data[selectedDate] = new List<double>();
        }
        _data[selectedDate].Add(distance);
        SaveDataToCsv();
        skElement.InvalidateVisual();
    }

    private void LogDistanceChange(DateTime selectedDate, double distance)
    {
        var d = selectedDate.ToString("yyyy-MM-dd");
        if (_data.TryGetValue(selectedDate, out List<double> values))
        {
            _logger.Debug($"日期 {d} 的距离由 {string.Join(", ", values)} 添加了 {distance}");
        }
        else
        {
            _logger.Debug($"添加日期 {d} 的距离 {distance}");
        }
    }

    private void SaveDataToCsv()
    {
        var sortedData = _data.OrderBy(entry => entry.Key).ToList();
        var csvLines = sortedData.SelectMany(entry => entry.Value.Select(value => $"{entry.Key:yyyy-MM-dd},{value:F2}"));
        var file = Path.Combine(_dataDir, $"{_year}.csv");
        File.WriteAllLines(file, csvLines);
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
}