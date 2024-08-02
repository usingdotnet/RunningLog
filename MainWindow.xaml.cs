// 版本19
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
        private const int Year = 2022; // 提取年份为类字段
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

            // 使用支持中文的字体
            var paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                TextSize = 16,
                Typeface = SKTypeface.FromFamilyName("Microsoft YaHei") // 或者其他支持中文的字体
            };

            // 绘制指定的星期几标签（只绘制一、三、五）
            var daysOfWeek = new[] { "一", "三", "五" };
            paint.Color = SKColors.Black;
            int labelVerticalOffset = (cellSize + padding) / 2 + 3; // 标签垂直偏移量
            for (int i = 0; i < daysOfWeek.Length; i++)
            {
                int row = Array.IndexOf(new[] { "一", "二", "三", "四", "五", "六", "日" }, daysOfWeek[i]);
                canvas.DrawText(daysOfWeek[i], padding / 2, labelHeight + row * (cellSize + padding) + labelVerticalOffset, paint);
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
                paint.Color = SKColors.Black;
                canvas.DrawText($"{month}月", monthOffsetX, labelHeight / 2 + monthLabelHeight / 2, paint);
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
                    if (index >= totalDays) break;

                    var date = startDate.AddDays(index);
                    int count = _data.ContainsKey(date) ? _data[date] : 0;

                    SKColor color = count == 0 ? SKColors.LightGray : GetGreenColor(count, _maxCount);
                    paint.Color = color;

                    var rect = new SKRect(
                        col * (cellSize + padding) + dayLabelWidth,
                        row * (cellSize + padding) + labelHeight,
                        col * (cellSize + padding) + cellSize + dayLabelWidth,
                        row * (cellSize + padding) + cellSize + labelHeight
                    );

                    canvas.DrawRect(rect, paint);
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
                var date = startDate.AddDays(i);
                // 计算每周的周一、三、五的跑步数据
                if (date.DayOfWeek == DayOfWeek.Monday || date.DayOfWeek == DayOfWeek.Wednesday || date.DayOfWeek == DayOfWeek.Friday)
                {
                    data[date] = 5; // 跑步5公里
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
