// 版本26
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SteakCreateTool
{
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
        private Dictionary<DateTime, double> _data;
        private double _maxCount;

        public MainWindow()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            var filePath = $"{_year}.csv";
            if (File.Exists(filePath))
            {
                _data = LoadDataFromCsv(filePath);
            }
            else
            {
                _data = GenerateSampleData();
            }
            _maxCount = _data.Values.Max(); // 计算最大 count 值
        }

        private Dictionary<DateTime, double> LoadDataFromCsv(string filePath)
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
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.White);

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
                Typeface = SKTypeface.FromFamilyName("Microsoft YaHei", SKFontStyle.Bold)
            };

            // 统计信息字体设置
            var statsPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                TextSize = 16,
                Typeface = SKTypeface.FromFamilyName("Microsoft YaHei")
            };

            // 星期和月份标签字体设置
            var labelPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                TextSize = 14,
                Typeface = SKTypeface.FromFamilyName("Microsoft YaHei")
            };

            // 计算统计信息
            int runningDays = _data.Count(entry => entry.Value > 0);
            double totalDistance = _data.Values.Sum();

            string statsText = $"跑步天数 {runningDays}，总里程 {totalDistance:F1} km";
            string yearText = $"{_year}";

            // 绘制年份
            yearPaint.Color = SKColors.Black;
            var yearTextWidth = yearPaint.MeasureText(yearText);

            // 绘制统计信息
            statsPaint.Color = SKColors.Black;
            var statsTextWidth = statsPaint.MeasureText(statsText);

            // 计算居中的位置
            var centerX = (fixedWidth - yearTextWidth) / 2;

            canvas.DrawText(yearText, centerX, yearLabelHeight, yearPaint);
            canvas.DrawText(statsText, fixedWidth - statsTextWidth - 20, yearLabelHeight + statsLabelHeight, statsPaint);

            // 绘制指定的星期几标签（只绘制一、三、五）
            var daysOfWeek = new[] { "一", "三", "五" };
            labelPaint.Color = SKColors.Black;
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
                labelPaint.Color = SKColors.Black;
                canvas.DrawText($"{month}月", monthOffsetX, headerHeight + monthLabelHeight / 2, labelPaint);
            }

            // 计算总列数和总行数
            int totalCols = (int)Math.Ceiling((DateTime.IsLeapYear(_year) ? 366 : 365 + (int)GetChineseDayOfWeek(startDate.DayOfWeek)) / (double)rows);
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
                    double count = _data.ContainsKey(date) ? _data[date] : 0;

                    SKColor color = count == 0 ? SKColors.LightGray : GetGreenColor(count, _maxCount);
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
        }

        private SKColor GetGreenColor(double distance, double maxDistance)
        {
            // 计算距离的比例
            double normalizedDistance = Math.Min(1.0, distance / maxDistance);

            // 设置渐变的起始和终止颜色
            SKColor startColor = new SKColor(255, 255, 0); // 黄色
            SKColor endColor = new SKColor(0, 128, 0); // 深绿色

            // 插值计算颜色
            byte r = (byte)(startColor.Red + (endColor.Red - startColor.Red) * normalizedDistance);
            byte g = (byte)(startColor.Green + (endColor.Green - startColor.Green) * normalizedDistance);
            byte b = (byte)(startColor.Blue + (endColor.Blue - startColor.Blue) * normalizedDistance);

            return new SKColor(r, g, b);
        }

        private Dictionary<DateTime, double> GenerateSampleData()
        {
            var rand = new Random();
            var data = new Dictionary<DateTime, double>();
            var startDate = new DateTime(_year, 1, 1);
            int totalDays = DateTime.IsLeapYear(_year) ? 366 : 365;

            // 生成数据的日期范围
            for (int i = 0; i < 175; i++)
            {
                double x = rand.NextDouble(); // 使用 double 类型生成随机数
                var date = startDate.AddDays(i);
                // 计算每周的周一、三、五的跑步数据
                if (date.DayOfWeek == DayOfWeek.Monday || date.DayOfWeek == DayOfWeek.Wednesday || date.DayOfWeek == DayOfWeek.Friday)
                {
                    data[date] = 5 * x; // 跑步5公里
                }
                else
                {
                    data[date] = 0; // 未跑步
                }
            }
            return data;
        }

        private ChineseDayOfWeek GetChineseDayOfWeek(DayOfWeek dayOfWeek)
        {
            // 将 .NET 的 DayOfWeek 转换为中国习惯的星期枚举
            return (ChineseDayOfWeek)(((int)dayOfWeek + 6) % 7);
        }
    }
}
