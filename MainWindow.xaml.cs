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
using CliWrap.Buffered;
using System.Threading.Tasks;

namespace RunningLog;

public enum ChineseDayOfWeek
{
    Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday
}

public partial class MainWindow : Window
{
    private int _year = DateTime.Now.Year;
    private Dictionary<DateTime, double> _data = [];
    private readonly string _dataDir = @"E:\Code\MyCode\RunningLog\data";
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

    public MainWindow()
    {
        InitializeComponent();
        InitializeWindowPosition();
        LoadData();
        InitializeTodayDistance();
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

    private void OnYearButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && int.TryParse(button.Content.ToString(), out int year))
        {
            _year = year;
            LoadData();
            skElement.InvalidateVisual();
        }
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        DrawBackground(canvas);
        DrawHeader(canvas);
        DrawHeatmap(canvas);
        SaveAsPng(e.Surface);
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
            Color = textColor
        };

        int runningDays = _data.Count(entry => entry.Value > 0);
        double totalDistance = _data.Values.Sum();

        string statsText = $"共跑步 {runningDays} 天，总里程 {totalDistance:F2} km";
        string yearText = $"{_year}";

        var yearTextWidth = yearPaint.MeasureText(yearText);
        var statsTextWidth = statsPaint.MeasureText(statsText);

        var centerX = (FixedWidth - yearTextWidth) / 2;

        canvas.DrawText(yearText, centerX, YearLabelHeight, yearPaint);
        canvas.DrawText(statsText, FixedWidth - statsTextWidth - 20, YearLabelHeight + StatsLabelHeight, statsPaint);
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

                SKColor color = _isDarkMode
                    ? (v == 0 ? new SKColor(68, 68, 68) : GetDayColor(v))
                    : (v == 0 ? SKColors.LightGray : GetDayColor(v));

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

    private void SaveAsPng(SKSurface surface)
    {
        string png = Path.Combine(_dataDir, $"{_year}.png");
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 80);
        using var stream = File.OpenWrite(png);
        data.SaveTo(stream);
    }

    private static SKColor GetDayColor(double distance)
    {
        double lowThreshold = 2.5;
        double highThreshold = 5.0;

        if (distance < lowThreshold)
        {
            return new SKColor(173, 255, 47);
        }

        if (distance >= highThreshold)
        {
            return new SKColor(0, 128, 0);
        }

        SKColor startColor = new SKColor(173, 255, 47);
        SKColor endColor = new SKColor(0, 128, 0);

        double normalizedDistance = (distance - lowThreshold) / (highThreshold - lowThreshold);

        byte r = (byte)(startColor.Red + (endColor.Red - startColor.Red) * normalizedDistance);
        byte g = (byte)(startColor.Green + (endColor.Green - startColor.Green) * normalizedDistance);
        byte b = (byte)(startColor.Blue + (endColor.Blue - startColor.Blue) * normalizedDistance);

        return new SKColor(r, g, b);
    }

    private static ChineseDayOfWeek GetChineseDayOfWeek(DayOfWeek dayOfWeek)
        => (ChineseDayOfWeek)(((int)dayOfWeek + 6) % 7);

    private async void BtnOk_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ValidateInput(out DateTime selectedDate, out double distance))
        {
            MessageBox.Show("请输入有效的距离和日期。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        UpdateDataAndSave(selectedDate, distance);
        await CommitAndPush($"跑步 {distance} 公里", @"E:\Code\MyCode\RunningLog");
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

    private void BtnLightMode_OnClick(object sender, RoutedEventArgs e)
    {
        _isDarkMode = false;
        skElement.InvalidateVisual();
    }

    private void BtnDarkMode_OnClick(object sender, RoutedEventArgs e)
    {
        _isDarkMode = true;
        skElement.InvalidateVisual();
    }

    private async Task CommitAndPush(string commitMessage, string repositoryPath)
    {
        try
        {
            await ExecuteGitCommand(repositoryPath, $"commit -a -m \"{commitMessage}\"");
            await ExecuteGitCommand(repositoryPath, "push");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"发生错误: {ex.Message}");
        }
    }

    private async Task ExecuteGitCommand(string workingDirectory, string arguments)
    {
        await Cli.Wrap("git")
            .WithArguments(arguments)
            .WithWorkingDirectory(workingDirectory)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(s => _logger.Debug(s)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(s => _logger.Debug(s)))
            .ExecuteAsync();
    }
}