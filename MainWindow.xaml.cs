using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NLog;
using CliWrap;
using Tomlet;
using Tomlet.Models;
using System.Xml.Serialization;
using CliWrap.Buffered;

namespace RunningLog;

public enum ChineseDayOfWeek
{
    Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday
}

public partial class MainWindow : Window
{
    private int _year = DateTime.Now.Year;
    private Dictionary<DateTime, double> _data = [];
    private string _dataDir = "";
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private bool _isDarkMode = true;
    private readonly DateTime _today = DateTime.Now.Date;

    private const int CellSize = 12;
    private const int CellPadding = 2;
    private const int FixedWidth = 790;
    private const int LabelHeight = 30;
    private const int DayLabelWidth = 30;
    private const int MonthLabelHeight = 20;
    private const int YearLabelHeight = 40;
    private const int StatsLabelHeight = 30;
    private const int HeaderHeight = YearLabelHeight + StatsLabelHeight + 20;
    private AppConfig _config;
    private const string ConfigFile = "config.toml";

    private void LoadConfig()
    {
        if (File.Exists(ConfigFile))
        {
            string tomlString = File.ReadAllText(ConfigFile);
            _config = TomletMain.To<AppConfig>(tomlString);
        }
        else
        {
            _config = new AppConfig();
            SaveConfig();
        }

        _isDarkMode = _config.IsDarkMode;
        _dataDir = _config.DataDir;
    }

    private void SaveConfig()
    {
        _config.IsDarkMode = _isDarkMode;
        _config.DataDir = _dataDir;
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
        BtnPublish.Visibility = IsGitRepository() ? Visibility.Visible : Visibility.Collapsed;
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
        if (_data.TryGetValue(_today, out double dis))
        {
            TxtDistance.Text = dis.ToString("F2");
        }
    }

    private void LoadData()
    {
        var filePath = Path.Combine(_dataDir, $"{_year}.csv");
        _data = File.Exists(filePath) ? LoadDataFromCsv(filePath) : [];
    }

    private static Dictionary<DateTime, double> LoadDataFromCsv(string filePath)
    {
        return File.ReadLines(filePath)
            .Select(line => line.Split(','))
            .Where(fields => fields.Length >= 2)
            .ToDictionary(
                fields => DateTime.Parse(fields[0]),
                fields => double.Parse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture)
            );
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        DrawBackground(canvas);
        DrawHeader(canvas);
        DrawHeatmap(canvas);
        DrawStats(canvas);  // 新增：绘制左下角的统计信息
        DrawLegend(canvas); // 添加这一行
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

        var lastRunPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            TextSize = 14,
            Typeface = SKTypeface.FromFamilyName("Microsoft YaHei"),
            Color = textColor
        };

        string yearText = $"{_year}";
        var yearTextWidth = yearPaint.MeasureText(yearText);
        var centerX = (FixedWidth - yearTextWidth) / 2;

        canvas.DrawText(yearText, centerX, YearLabelHeight, yearPaint);

        // 绘制最后一次跑步信息
        var lastRun = _data.OrderByDescending(x => x.Key).FirstOrDefault();
        if (lastRun.Key != default)
        {
            string lastRunText = $"最近一次跑步：{lastRun.Key.ToShortDateString()}, {lastRun.Value:F2} 公里";
            var lastRunTextWidth = lastRunPaint.MeasureText(lastRunText);
            canvas.DrawText(lastRunText, FixedWidth - lastRunTextWidth - 20, YearLabelHeight + StatsLabelHeight, lastRunPaint);
        }
    }

    private float CalculateHeatmapBottom()
    {
        var startDate = new DateTime(_year, 1, 1);
        int totalDays = DateTime.IsLeapYear(_year) ? 366 : 365;
        int totalRows = 7; // 一周7天
        return HeaderHeight + LabelHeight + totalRows * (CellSize + CellPadding) + CellPadding;
    }

    private void DrawStats(SKCanvas canvas)
    {
        var textColor = _isDarkMode ? SKColors.White : new SKColor(34, 34, 34);

        var statsPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            TextSize = 14,
            Typeface = SKTypeface.FromFamilyName("Microsoft YaHei"),
            Color = textColor
        };

        int runningDays = _data.Count(entry => entry.Value > 0);
        double totalDistance = _data.Values.Sum();

        string daysLabel = "跑步天数:";
        string daysValue = $"{runningDays}";
        string daysUnit = " 天";
        string distanceLabel = "总里程:";
        string distanceValue = $"{totalDistance:F2}";
        string distanceUnit = " 公里";

        float labelWidth = Math.Max(statsPaint.MeasureText(daysLabel), statsPaint.MeasureText(distanceLabel));
        float valueWidth = Math.Max(statsPaint.MeasureText(daysValue), statsPaint.MeasureText(distanceValue));
        float unitWidth = Math.Max(statsPaint.MeasureText(daysUnit), statsPaint.MeasureText(distanceUnit));

        // 计算热力图底部位置
        float heatmapBottom = CalculateHeatmapBottom();

        // 设置统计信息的垂直位置
        float statsY1 = heatmapBottom + 20; // 第一行文本的Y坐标
        float statsY2 = statsY1 + 20; // 第二行文本的Y坐标

        float labelX = 30;
        float valueX = labelX + labelWidth + 10; // 10是标签和值之间的间距
        float unitX = valueX + valueWidth + 5; // 5是值和单位之间的间距

        canvas.DrawText(daysLabel, labelX, statsY1, statsPaint);
        canvas.DrawText(daysValue, unitX - statsPaint.MeasureText(daysValue), statsY1, statsPaint);
        canvas.DrawText(daysUnit, unitX, statsY1, statsPaint);

        canvas.DrawText(distanceLabel, labelX, statsY2, statsPaint);
        canvas.DrawText(distanceValue, unitX - statsPaint.MeasureText(distanceValue), statsY2, statsPaint);
        canvas.DrawText(distanceUnit, unitX, statsY2, statsPaint);
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
        canvas.DrawText("0km", legendX + CellSize / 2 - textPaint.MeasureText("0km") / 2 - 2 * CellSize, legendY + CellSize / 2 + textOffsetY, textPaint);
        canvas.DrawText("5km", legendX + 6 * CellSize - textPaint.MeasureText("5km") / 2 + 4, legendY + CellSize + textOffsetY + textOffsetX, textPaint);
        canvas.DrawText("10km", legendX + 11 * CellSize + textOffsetX, legendY + CellSize / 2 + textOffsetY, textPaint);
    }

    private void DrawHeatmap(SKCanvas canvas)
    {
        var textColor = _isDarkMode ? SKColors.White : new SKColor(34, 34, 34);

        var labelPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            TextSize = 14,
            Typeface = SKTypeface.FromFamilyName("Microsoft YaHei"),
            Color = textColor
        };

        DrawDayLabels(canvas, labelPaint);
        DrawMonthLabels(canvas, labelPaint);
        DrawHeatmapCells(canvas, labelPaint);
    }

    private void DrawDayLabels(SKCanvas canvas, SKPaint labelPaint)
    {
        var daysOfWeek = new[] { "一", "三", "五" };
        int labelVerticalOffset = (CellSize + CellPadding) / 2 + 3;
        for (int i = 0; i < daysOfWeek.Length; i++)
        {
            int row = Array.IndexOf(new[] { "一", "二", "三", "四", "五", "六", "日" }, daysOfWeek[i]);
            canvas.DrawText(daysOfWeek[i], CellPadding / 2, HeaderHeight + LabelHeight + row * (CellSize + CellPadding) + labelVerticalOffset, labelPaint);
        }
    }

    private void DrawMonthLabels(SKCanvas canvas, SKPaint labelPaint)
    {
        var startDate = new DateTime(_year, 1, 1);
        for (int month = 1; month <= 12; month++)
        {
            var monthStart = new DateTime(_year, month, 1);
            int daysBeforeMonth = (monthStart - startDate).Days;
            int monthStartCol = (daysBeforeMonth + (int)GetChineseDayOfWeek(startDate.DayOfWeek)) / 7;
            int monthOffsetX = monthStartCol * (CellSize + CellPadding) + DayLabelWidth;

            canvas.DrawText($"{month}月", monthOffsetX, HeaderHeight + MonthLabelHeight / 2, labelPaint);
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
                double v = _data.TryGetValue(date, out double value) ? value : 0;

                SKColor color = GetDayColor(v);

                labelPaint.Color = color;

                var rect = new SKRect(
                    col * (CellSize + CellPadding) + DayLabelWidth,
                    row * (CellSize + CellPadding) + HeaderHeight + LabelHeight,
                    col * (CellSize + CellPadding) + CellSize + DayLabelWidth,
                    row * (CellSize + CellPadding) + CellSize + HeaderHeight + LabelHeight
                );

                canvas.DrawRect(rect, labelPaint);
            }
        }
    }

    private void SavePng(SKSurface surface)
    {
        string csvFilePath = Path.Combine(_config.DataDir, $"{_year}.csv");
        string pngFilePath = Path.Combine(_config.DataDir, $"{_year}.png");

        if (File.Exists(csvFilePath) && File.Exists(pngFilePath))
        {
            DateTime csvLastModified = File.GetLastWriteTime(csvFilePath);
            DateTime pngLastModified = File.GetLastWriteTime(pngFilePath);

            if (csvLastModified <= pngLastModified)
            {
                // CSV文件未修改或PNG文件比CSV文件新,无需重新生成PNG
                return;
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

        double minDistance = 1.0;
        double maxDistance = 10.0;

        // 将距离限制在 1-10km 范围内
        distance = Math.Max(minDistance, Math.Min(maxDistance, distance));

        // 使用更强的非线性函数计算归一化的距离值 (0.0 - 1.0)
        double normalizedDistance = Math.Pow((distance - minDistance) / (maxDistance - minDistance), 0.9);

        // 定义起始颜色（黄色）和结束颜色（红色）
        SKColor startColor = new SKColor(255, 255, 0);  // 黄色
        SKColor endColor = new SKColor(255, 0, 0);      // 红色

        // 使用插值计算中间颜色
        byte r = (byte)(startColor.Red + (endColor.Red - startColor.Red) * normalizedDistance);
        byte g = (byte)(startColor.Green + (endColor.Green - startColor.Green) * normalizedDistance);
        byte b = (byte)(startColor.Blue + (endColor.Blue - startColor.Blue) * normalizedDistance);

        return new SKColor(r, g, b);
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

        var lastRun = _data.OrderByDescending(x => x.Key).FirstOrDefault();
        if (lastRun.Key != default)
        {
            await CommitAndPush($"跑步 {lastRun.Value:F2} 公里");
        }
        else
        {
            ShowMessage("没有可发布的跑步记录。", MessageType.Error);
        }
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
        _data[selectedDate] = distance;
        SaveDataToCsv();
        skElement.InvalidateVisual();
    }

    private void LogDistanceChange(DateTime selectedDate, double distance)
    {
        var d = selectedDate.ToString("yyyy-MM-dd");
        if (_data.TryGetValue(selectedDate, out double value))
        {
            _logger.Debug($"日期 {d} 的距离由 {value} 修改为 {distance}");
        }
        else
        {
            _logger.Debug($"添加日期 {d} 的距离 {distance}");
        }
    }

    private void SaveDataToCsv()
    {
        var sortedData = _data.OrderBy(entry => entry.Key).ToList();
        var csvLines = sortedData.Select(entry => $"{entry.Key:yyyy-MM-dd},{entry.Value:F2}");
        var file = Path.Combine(_dataDir, $"{_year}.csv");
        File.WriteAllLines(file, csvLines);
    }

    private async Task CommitAndPush(string commitMessage)
    {
        try
        {
            // 检查仓库状态
            string status = await GetGitStatus();
            if (string.IsNullOrWhiteSpace(status))
            {
                ShowMessage("没有需要提交的更改。", MessageType.Warning);
                return;
            }

            await ExecuteGitCommand($"commit -a -m \"{commitMessage}\"");
            await ExecuteGitCommand("push");
            ShowMessage("已成功发布最新的跑步记录。", MessageType.Success);
        }
        catch (Exception ex)
        {
            ShowMessage($"发生错误: {ex.Message}", MessageType.Error);
        }
    }

    private async Task<string> GetGitStatus()
    {
        var result = await Cli.Wrap("git")
            .WithArguments("status --porcelain")
            .WithWorkingDirectory(_dataDir)
            .ExecuteBufferedAsync();

        return result.StandardOutput.Trim();
    }

    private async Task ExecuteGitCommand(string arguments)
    {
        await Cli.Wrap("git")
            .WithArguments(arguments)
            .WithWorkingDirectory(_dataDir)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(s => _logger.Debug(s)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(s => _logger.Debug(s)))
            .ExecuteAsync();
    }

    private bool IsGitRepository()
    {
        return Directory.Exists(Path.Combine(_dataDir, ".git"));
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
}