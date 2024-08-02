// 版本25
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

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
        private const int Year = 2024; // 提取年份为类字段
        private Dictionary<DateTime, int> _data;
        private int _maxCount;

        public MainWindow()
        {
            InitializeComponent();

            // 示例数据，实际应用中可以从其他数据源获取
            _data = GenerateSampleData();
            _maxCount = _data.Values.Max(); // 计算最大 count 值
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
            int totalDistance = _data.Values.Sum();

            string statsText = $"跑步天数 {runningDays}，总里程 {totalDistance} km";
            string yearText = $"{Year}";

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
            var startDate = new DateTime(Year, 1, 1);
            int totalDays = DateTime.IsLeapYear(Year) ? 366 : 365;
            int rows = 7; // 一周7天

            for (int month = 1; month <= 12; month++)
            {
                var monthStart = new DateTime(Year, month, 1);
                int daysBeforeMonth = (monthStart - startDate).Days;
                int monthStartCol = (daysBeforeMonth + (int)GetChineseDayOfWeek(startDate.DayOfWeek)) / rows;
                int monthOffsetX = monthStartCol * (cellSize + padding) + dayLabelWidth;

                // 绘制月份名称
                labelPaint.Color = SKColors.Black;
                canvas.DrawText($"{month}月", monthOffsetX, headerHeight + monthLabelHeight / 2, labelPaint);
            }

            // 计算总列数和总行数
            int totalCols = (int)Math.Ceiling((DateTime.IsLeapYear(Year) ? 366 : 365 + (int)GetChineseDayOfWeek(startDate.DayOfWeek)) / (double)rows);
            int totalRows = rows;

            // 绘制热力图格子
            for (int col = 0; col < totalCols; col++)
            {
                for (int row = 0; row < totalRows; row++)
                {
                    int index = col * totalRows + row - (int)GetChineseDayOfWeek(startDate.DayOfWeek);
                    if (index < 0) continue; // 跳过前一周的日期
                    if (index >= (DateTime.IsLeapYear(Year) ? 366 : 365)) break;

                    var date = startDate.AddDays(index);
                    int count = _data.ContainsKey(date) ? _data[date] : 0;

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

        private SKColor GetGreenColor(int count, int maxCount)
        {
            double percentage = Math.Min(1.0, count / (double)maxCount);
            byte g = (byte)(255 * percentage);
            return new SKColor(0, g, 0); // 从浅绿色到深绿色
        }

        private Dictionary<DateTime, int> GenerateSampleData()
        {
            var rand = new Random();
            var data = new Dictionary<DateTime, int>();
            var startDate = new DateTime(Year, 1, 1);
            int totalDays = DateTime.IsLeapYear(Year) ? 366 : 365;

            // 生成数据的日期范围
            for (int i = 0; i < 175; i++)
            {
                int x = rand.Next(2);
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
