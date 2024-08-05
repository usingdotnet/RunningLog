// 版本27
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using NLog;

namespace RunningLog;

public enum ChineseDayOfWeek
{
    Monday = 0,
    Tuesday = 1,
    Wednesday = 2,
    Thursday = 3,
    Friday = 4,
    Saturday = 5,
    Sunday = 6
}

public partial class MainWindow : Window
{
    private int _year = 2024; // 提取年份为类字段并设为可变
    private Dictionary<DateTime, double> _data = [];
    private readonly string _dataDir = @"E:\Code\MyCode\RunningLog\data";
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private bool _isDarkMode = true;

    public MainWindow()
    {
        InitializeComponent();
        // 获取屏幕工作区域
        double screenHeight = SystemParameters.PrimaryScreenHeight;
        double screenWidth = SystemParameters.PrimaryScreenWidth;

        // 获取任务栏高度
        double taskbarHeight = SystemParameters.PrimaryScreenHeight - SystemParameters.WorkArea.Height;

        // 设置窗体的位置，使其底部紧靠任务栏的上边缘
        this.Top = screenHeight - this.Height - taskbarHeight + 7;
        this.Left = (screenWidth - this.Width) / 2;

        LoadData();
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
    }

    private void LoadData()
    {
        var filePath = $"{_year}.csv";
        filePath = Path.Combine(_dataDir, filePath);
        if (File.Exists(filePath))
        {
            _data = LoadDataFromCsv(filePath);
        }
        else
        {
            _data = new Dictionary<DateTime, double>();
        }
    }

    private static Dictionary<DateTime, double> LoadDataFromCsv(string filePath)
    {
        var data = new Dictionary<DateTime, double>();

        foreach (var line in File.ReadLines(filePath))
        {
            var fields = line.Split(',');
            if (fields.Length >= 2 && DateTime.TryParse(fields[0], out var date))
            {
                if (double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var distance))
                {
                    data[date] = distance;
                }
            }
        }

        return data;
    }

    private void OnYearButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            _year = int.Parse(button.Content.ToString());
            LoadData();
            skElement.InvalidateVisual(); // 重新绘制
        }
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var black = new SKColor(34, 34, 34);
        var canvas = e.Surface.Canvas;
        var backgroundColor = _isDarkMode ?black : SKColors.White;
        var textColor = _isDarkMode ? SKColors.White : black;
        canvas.Clear(backgroundColor);

        int cellSize = 12;
        int padding = 2;
        int labelHeight = 30; // 标签高度
        int dayLabelWidth = 30; // 星期几标签宽度
        int monthLabelHeight = 20; // 月份标签高度
        int yearLabelHeight = 40; // 年份标签高度
        int statsLabelHeight = 30; // 统计信息标签高度
        int headerHeight = yearLabelHeight + statsLabelHeight + 20; // 第一行高度，加大间距
        int fixedWidth = 790; // 固定宽度

        // 年份标题字体设置
        var yearPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            TextSize = 20,
            Typeface = SKTypeface.FromFamilyName("Microsoft YaHei", SKFontStyle.Bold),
            Color = textColor
        };

        // 统计信息字体设置
        var statsPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            TextSize = 16,
            Typeface = SKTypeface.FromFamilyName("Microsoft YaHei"),
            Color = textColor
        };

        // 星期和月份标签字体设置
        var labelPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            TextSize = 14,
            Typeface = SKTypeface.FromFamilyName("Microsoft YaHei"),
            Color = textColor
        };

        // 计算统计信息
        int runningDays = _data.Count(entry => entry.Value > 0);
        double totalDistance = _data.Values.Sum();

        string statsText = $"共跑步 {runningDays} 天，总里程 {totalDistance:F2} km";
        string yearText = $"{_year}";

        // 绘制年份
        var yearTextWidth = yearPaint.MeasureText(yearText);

        // 绘制统计信息
        var statsTextWidth = statsPaint.MeasureText(statsText);

        // 计算居中的位置
        var centerX = (fixedWidth - yearTextWidth) / 2;

        canvas.DrawText(yearText, centerX, yearLabelHeight, yearPaint);
        canvas.DrawText(statsText, fixedWidth - statsTextWidth - 20, yearLabelHeight + statsLabelHeight, statsPaint);

        // 绘制指定的星期几标签（只绘制一、三、五）
        var daysOfWeek = new[] { "一", "三", "五" };
        int labelVerticalOffset = (cellSize + padding) / 2 + 3; // 标签垂直偏移量
        for (int i = 0; i < daysOfWeek.Length; i++)
        {
            int row = Array.IndexOf(new[] { "一", "二", "三", "四", "五", "六", "日" }, daysOfWeek[i]);
            canvas.DrawText(daysOfWeek[i], padding / 2, headerHeight + labelHeight + row * (cellSize + padding) + labelVerticalOffset, labelPaint);
        }

        // 绘制月份标签
        var startDate = new DateTime(_year, 1, 1);
        int totalDays = DateTime.IsLeapYear(_year) ? 366 : 365;
        int rows = 7; // 一周7天

        for (int month = 1; month <= 12; month++)
        {
            var monthStart = new DateTime(_year, month, 1);
            int daysBeforeMonth = (monthStart - startDate).Days;
            int monthStartCol = (daysBeforeMonth + (int)GetChineseDayOfWeek(startDate.DayOfWeek)) / rows;
            int monthOffsetX = monthStartCol * (cellSize + padding) + dayLabelWidth;

            // 绘制月份名称
            canvas.DrawText($"{month}月", monthOffsetX, headerHeight + monthLabelHeight / 2, labelPaint);
        }

        // 计算总列数和总行数
        int totalCols = (int)Math.Ceiling((totalDays + (int)GetChineseDayOfWeek(startDate.DayOfWeek)) / (double)rows);
        int totalRows = rows;

        // 绘制热力图格子
        for (int col = 0; col < totalCols; col++)
        {
            for (int row = 0; row < totalRows; row++)
            {
                int index = col * totalRows + row - (int)GetChineseDayOfWeek(startDate.DayOfWeek);
                if (index < 0) continue; // 跳过前一周的日期
                if (index >= (DateTime.IsLeapYear(_year) ? 366 : 365)) break;

                var date = startDate.AddDays(index);
                double v = _data.TryGetValue(date, out double value) ? value : 0;

                SKColor color;
                if (_isDarkMode)
                {
                    color = v == 0 ? new SKColor(68, 68, 68) : GetDayColor(v);
                }
                else
                {
                    color = v == 0 ? SKColors.LightGray : GetDayColor(v);
                }

                labelPaint.Color = color;

                var rect = new SKRect(
                    col * (cellSize + padding) + dayLabelWidth,
                    row * (cellSize + padding) + headerHeight + labelHeight,
                    col * (cellSize + padding) + cellSize + dayLabelWidth,
                    row * (cellSize + padding) + cellSize + headerHeight + labelHeight
                );

                canvas.DrawRect(rect, labelPaint);
            }
        }

        using (var image = e.Surface.Snapshot())
        using (var data = image.Encode(SKEncodedImageFormat.Png, 80))
        using (var stream = File.OpenWrite($"{_year}.png"))
        {
            data.SaveTo(stream);
        }
    }

    private static SKColor GetDayColor(double distance)
    {
        // 设置特定距离的颜色阈值
        double lowThreshold = 2.5; // 低阈值，低于该距离使用较浅的绿色
        double highThreshold = 5.0; // 高阈值，高于该距离使用深绿色

        // 如果距离低于低阈值，则使用较浅的绿色
        if (distance < lowThreshold)
        {
            return new SKColor(173, 255, 47); // 浅绿色
        }

        // 如果距离大于等于高阈值，则使用深绿色
        if (distance >= highThreshold)
        {
            return new SKColor(0, 128, 0); // 深绿色
        }

        // 设置渐变的起始和终止颜色
        SKColor startColor = new SKColor(173, 255, 47); // 浅绿色
        SKColor endColor = new SKColor(0, 128, 0); // 深绿色

        // 计算渐变比例（基于低阈值和高阈值）
        double normalizedDistance = (distance - lowThreshold) / (highThreshold - lowThreshold);

        // 插值计算颜色
        byte r = (byte)(startColor.Red + (endColor.Red - startColor.Red) * normalizedDistance);
        byte g = (byte)(startColor.Green + (endColor.Green - startColor.Green) * normalizedDistance);
        byte b = (byte)(startColor.Blue + (endColor.Blue - startColor.Blue) * normalizedDistance);

        return new SKColor(r, g, b);
    }

    private static ChineseDayOfWeek GetChineseDayOfWeek(DayOfWeek dayOfWeek)
    {
        // 将 .NET 的 DayOfWeek 转换为中国习惯的星期枚举
        return (ChineseDayOfWeek)(((int)dayOfWeek + 6) % 7);
    }

    private void BtnOk_OnClick(object sender, RoutedEventArgs e)
    {
        if (DpDate.SelectedDate.HasValue && double.TryParse(TxtDistance.Text, out double distance) && distance > 0)
        {
            DateTime selectedDate = DpDate.SelectedDate.Value;
            int selectedYear = selectedDate.Year;

            // 如果选定的日期的年份与当前年份不同，更新年份并加载数据
            if (selectedYear != _year)
            {
                _year = selectedYear;
                LoadData();
            }

            var d = selectedDate.ToString("yyyy-MM-dd");
            if (_data.TryGetValue(selectedDate, out double value))
            {
                _logger.Debug($"日期 {d} 的距离由 {value} 修改为 {distance}");
            }
            else
            {
                _logger.Debug($"添加日期 {d} 的距离 {distance}");
            }

            _data[selectedDate] = distance;

            // 按日期排序并保存到 CSV 文件
            var sortedData = _data.OrderBy(entry => entry.Key).ToList();
            var csvLines = sortedData.Select(entry => $"{entry.Key:yyyy-MM-dd},{entry.Value:F2}").ToArray();
            var file = Path.Combine(_dataDir, $"{_year}.csv");
            File.WriteAllLines(file, csvLines);

            // 重新绘制
            skElement.InvalidateVisual();
        }
        else
        {
            MessageBox.Show("请输入有效的距离和日期。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
}
