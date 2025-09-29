using CsvHelper;
using NLog;
using ScottPlot;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Tomlet;

namespace RunningLog;

public enum ChineseDayOfWeek
{
    Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday
}

public partial class MainWindow : Window
{
    private int _year = DateTime.Now.Year;
    private Dictionary<DateTime, List<RunData>> _data;
    private string _dataDir = "";
    private string _repoDir = "";
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private bool _isDarkMode = true;

    private const int CellSize = 12;
    private const int CellPadding = 2;
    private const int FixedWidth = 790;
    private const int LabelHeight = 30;
    private const int LeftMargin = 30;
    private const int MonthLabelHeight = 20;
    private const int YearLabelHeight = 30;
    private const int HeaderHeight = YearLabelHeight + 10;
    private AppConfig _config = new();
    private string _configFile = "config.toml";
    private int _lastInsertedId;
    private readonly RunningDataService _runningDataService;
    private readonly GitService _gitService;
    private string _timeOfDay = "";
    private string _place = "";
    private string _fullLog = "";
    private readonly Lock _lock = new();

    public MainWindow()
    {
        InitializeComponent();
        LoadConfig();
        _runningDataService = new RunningDataService(_dataDir);
        _gitService = new GitService(_repoDir);
        InitializeWindowPosition();
        _data = _runningDataService.LoadDataOfYear(_year);
        UpdateYearButtonsVisibility();
        SetGitRelatedButtonsVisibility();
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
    }

    private void LoadConfig()
    {
        string dir = AppContext.BaseDirectory;
        _configFile = Path.Combine(dir, _configFile);
        if (File.Exists(_configFile))
        {
            _logger.Debug($"配置文件 {_configFile}");
            string tomlString = File.ReadAllText(_configFile);
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
        rbPlace1.Content = _config.Place1;
        rbPlace2.Content = _config.Place2;
    }

    private void SaveConfig()
    {
        _config.IsDarkMode = _isDarkMode;
        string tomlString = TomletMain.TomlStringFrom(_config);
        File.WriteAllText(_configFile, tomlString);
    }

    private void SetGitRelatedButtonsVisibility()
    {
        var visibility = _gitService.IsGitRepository() ? Visibility.Visible : Visibility.Collapsed;
        BtnRevert.Visibility = visibility;
        BtnPublish.Visibility = visibility;
    }

    private void InitializeWindowPosition()
    {
        double screenHeight = SystemParameters.PrimaryScreenHeight;
        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double taskbarHeight = SystemParameters.PrimaryScreenHeight - SystemParameters.WorkArea.Height;

        Top = screenHeight - Height - taskbarHeight + 7;
        Left = (screenWidth - Width) / 2;
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        DrawBackground(canvas);
        DrawYear(canvas);
        DrawTopRightInfo(canvas);
        DrawHeatmap(canvas);
        DrawLegend(canvas);

        UpdateReadme();
        UpdateTrendInfo();

        // 绘制每月跑量图表
        double[] monthlyDistances = GetMonthlyDistances();
        SavePng(e.Surface);
        DrawMonthlyDistancePlot(monthlyDistances);
        DrawMonthly();
        DrawYearly();
        ExportToCsv();
    }

    /// <summary>
    /// 更新 github readme 中的年份链接
    /// </summary>
    private void UpdateReadme()
    {
        List<string> urls = [];
        var di = new DirectoryInfo(_dataDir);
        var fis = di.GetFiles("*.png");
        fis = fis.Where(x => x.Name.StartsWith("20")).OrderByDescending(x => x.Name).ToArray();
        foreach (var fi in fis)
        {
            string year = Path.GetFileNameWithoutExtension(fi.Name);
            var url = $"![{year}]({_config.Repo}/blob/main/data/{year}.png)";
            urls.Add(url);
        }

        var readmePath = Path.Combine(_repoDir, "README.md");
        File.WriteAllLines(readmePath, urls);
    }

    private void UpdateTrendInfo()
    {
        var summary = _runningDataService.GetRunDataSummary();

        List<string> rows = [];
        var urlMonthly = $"![Monthly]({_config.Repo}/blob/main/data/CumulativeTrendByMonth.png)";
        var urlYearly = $"![Monthly]({_config.Repo}/blob/main/data/CumulativeTrendByYear.png)";

        var readmePath = Path.Combine(_repoDir, "trend.md");
        rows.Add($"### Total: {summary.DaysRun} days, {summary.TotalDistance} km.");
        rows.Add(Environment.NewLine);
        rows.Add(urlMonthly);
        rows.Add(Environment.NewLine);
        rows.Add(urlYearly);
        File.WriteAllLines(readmePath, rows);
    }

    private void DrawYear(SKCanvas canvas)
    {
        var textColor = _isDarkMode ? SKColors.White : new SKColor(34, 34, 34);

        var yearPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = textColor
        };

        var yearFont = new SKFont(SKTypeface.FromFamilyName("Microsoft YaHei", SKFontStyle.Bold), 20);

        string yearText = $"{_year}";
        canvas.DrawText(yearText, LeftMargin, YearLabelHeight - 5, yearFont, yearPaint);
    }

    private void DrawTopRightInfo(SKCanvas canvas)
    {
        var textColor = _isDarkMode ? SKColors.White : new SKColor(34, 34, 34);

        var font = new SKFont(SKTypeface.FromFamilyName("Consolas"), 16);
        var statsPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = textColor,
        };

        var lr = "Last Run: ";
        var t = "Total: ";

        // 绘制统计信息
        int runningDays = _data.Count(entry => entry.Value.Sum(r => r.Distance) > 0);
        double totalDistance = _data.Values.SelectMany(distances => distances).Sum(r => r.Distance);
        string statsTextContent = $"{runningDays} days, {totalDistance:F2} km";
        string statsText = $"{t,10}{statsTextContent,20}";
        var statsTextWidth = font.MeasureText(statsText);
        canvas.DrawText(statsText, FixedWidth - statsTextWidth - 20, YearLabelHeight - 5, font, statsPaint);

        // 绘制最后一次跑步信息
        var lastRun = _data.OrderByDescending(x => x.Key).FirstOrDefault();
        if (lastRun.Key != default)
        {
            string lastRunContent = $"{lastRun.Key:yyyy/MM/dd}, {lastRun.Value.Sum(r => r.Distance):F2} km";
            string lastRunText = $"{lr,10}{lastRunContent,20}";
            var lastRunTextWidth = font.MeasureText(lastRunText);
            canvas.DrawText(lastRunText, FixedWidth - lastRunTextWidth - 20, YearLabelHeight + 20, font, statsPaint);
        }
    }

    private void DrawBackground(SKCanvas canvas)
    {
        var backgroundColor = _isDarkMode ? new SKColor(34, 34, 34) : SKColors.White;
        canvas.Clear(backgroundColor);
    }

    private float CalculateHeatmapBottom()
    {
        var startDate = new DateTime(_year, 1, 1);
        int totalDays = DateTime.IsLeapYear(_year) ? 366 : 365;
        int totalRows = 7; // 一周7天
        return HeaderHeight + LabelHeight + totalRows * (CellSize + CellPadding) + CellPadding;
    }

    private void DrawLegend(SKCanvas canvas)
    {
        float legendY = CalculateHeatmapBottom() + 40; // 图例位置在热力图下方40像素
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
            //TextSize = 12,
            //Typeface = SKTypeface.FromFamilyName("Microsoft YaHei")
        };
        var font = new SKFont(SKTypeface.FromFamilyName("Microsoft YaHei"), 12);

        for (int i = 0; i <= 10; i++)
        {
            float x = legendX + i * CellSize;
            SKColor color = GetDayColor(i);
            paint.Color = color;

            canvas.DrawRect(new SKRect(x, legendY, x + CellSize, legendY + CellSize), paint);
        }

        float textOffsetY = font.Size / 2;
        float textOffsetX = 5;

        // 绘制图例文字
        canvas.DrawText("0 km", legendX + CellSize / 2 - font.MeasureText("0km") / 2 - 2 * CellSize, legendY + CellSize / 2 + textOffsetY, font, textPaint);
        canvas.DrawText("5 km", legendX + 6 * CellSize - font.MeasureText("5km") / 2 + 4, legendY + CellPadding + CellSize + textOffsetY + textOffsetX, font, textPaint);
        canvas.DrawText("10 km", legendX + 11 * CellSize + textOffsetX, legendY + CellSize / 2 + textOffsetY, font, textPaint);
    }

    private void DrawHeatmap(SKCanvas canvas)
    {
        var textColor = _isDarkMode ? SKColors.White : new SKColor(34, 34, 34);

        var labelPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            //Typeface = SKTypeface.FromFamilyName("Microsoft YaHei"),
            Color = textColor
        };

        DrawMonthLabels(canvas, labelPaint);
        DrawHeatmapCells(canvas, labelPaint);
    }

    private void DrawMonthLabels(SKCanvas canvas, SKPaint labelPaint)
    {
        var startDate = new DateTime(_year, 1, 1);
        string[] monthAbbreviations = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

        var font = new SKFont(SKTypeface.FromFamilyName("Microsoft YaHei"), 12);

        for (int month = 1; month <= 12; month++)
        {
            var monthStart = new DateTime(_year, month, 1);
            int daysBeforeMonth = (monthStart - startDate).Days;
            int monthStartCol = (daysBeforeMonth + (int)GetChineseDayOfWeek(startDate.DayOfWeek)) / 7;
            int monthOffsetX = monthStartCol * (CellSize + CellPadding) + LeftMargin;

            // Adjust Y position to add more space
            float yPosition = HeaderHeight + MonthLabelHeight + 20; // Increase spacing

            canvas.DrawText(monthAbbreviations[month - 1], monthOffsetX, yPosition, font, labelPaint);
        }
    }

    private void DrawHeatmapCells(SKCanvas canvas, SKPaint labelPaint)
    {
        var startDate = new DateTime(_year, 1, 1);
        int totalDays = DateTime.IsLeapYear(_year) ? 366 : 365;
        int startDayOffset = (int)GetChineseDayOfWeek(startDate.DayOfWeek);
        int totalCols = (int)Math.Ceiling((totalDays + startDayOffset) / 7.0);

        for (int col = 0; col < totalCols; col++)
        {
            for (int row = 0; row < 7; row++)
            {
                int index = col * 7 + row - startDayOffset;
                if (index < 0 || index >= totalDays) continue;

                var date = startDate.AddDays(index);
                double totalDistance = _data.TryGetValue(date, out List<RunData> runs) ? runs.Sum(r => r.Distance) : 0;

                SKColor color = GetDayColor(totalDistance);
                labelPaint.Color = color;

                var rect = new SKRect(
                    col * (CellSize + CellPadding) + LeftMargin,
                    row * (CellSize + CellPadding) + HeaderHeight + LabelHeight + 20,
                    col * (CellSize + CellPadding) + CellSize + LeftMargin,
                    row * (CellSize + CellPadding) + CellSize + HeaderHeight + LabelHeight + 20
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
        using var data = image.Encode(SKEncodedImageFormat.Png, 100); // 提高编码质量到100
        using var stream = File.OpenWrite(png);
        data.SaveTo(stream);
    }

    private SKColor GetDayColor(double distance)
    {
        if (distance == 0)
        {
            return _isDarkMode ? new SKColor(68, 68, 68) : SKColors.LightGray;
        }

        distance = Math.Clamp(distance, 1.0, ColorRanges.MaxDistance);

        return distance switch
        {
            <= ColorRanges.VeryShortDistance => InterpolateColor(
                ColorRanges.FrostColor,
                ColorRanges.ColdColor,
                (distance - 1.0) / 1.0),

            <= ColorRanges.ShortDistance => InterpolateColor(
                ColorRanges.ColdColor,
                ColorRanges.MildColor,
                (distance - ColorRanges.VeryShortDistance) / 1.5),

            <= ColorRanges.MediumDistance => InterpolateColor(
                ColorRanges.MildColor,
                ColorRanges.WarmColor,
                (distance - ColorRanges.ShortDistance) / 1.5),

            <= ColorRanges.MediumLongDistance => InterpolateColor(
                ColorRanges.WarmColor,
                ColorRanges.HotColor,
                (distance - ColorRanges.MediumDistance) / 1.5),

            <= ColorRanges.LongDistance => InterpolateColor(
                ColorRanges.HotColor,
                ColorRanges.IntenseColor,
                (distance - ColorRanges.MediumLongDistance) / 1.5),

            _ => InterpolateColor(
                ColorRanges.IntenseColor,
                ColorRanges.ExtremeColor,
                (distance - ColorRanges.LongDistance) / 2.0)
        };
    }

    private static SKColor InterpolateColor(SKColor start, SKColor end, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return new SKColor(
            (byte)(start.Red + (end.Red - start.Red) * t),
            (byte)(start.Green + (end.Green - start.Green) * t),
            (byte)(start.Blue + (end.Blue - start.Blue) * t)
        );
    }

    private static ChineseDayOfWeek GetChineseDayOfWeek(DayOfWeek dayOfWeek)
        => (ChineseDayOfWeek)(((int)dayOfWeek + 6) % 7);

    private void OnYearButtonClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is Button button && int.TryParse(button.Content.ToString(), out int year))
        {
            _year = year;
            _data = _runningDataService.LoadDataOfYear(_year);

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
        if (!ValidateInput(out RunRecordParams record))
        {
            ShowMessage("请输入有效的距离、时长、心率、配速和备注。", MessageType.Error);
            return;
        }

        _lastInsertedId = UpdateDataAndSave(record);
        ShowMessage("添加完成。", MessageType.Success);

        _fullLog = $"{_timeOfDay}在{_place}跑步 {record.Distance} 公里，用时 {record.Duration}，平均配速 {record.Pace}，步频 {record.Cadence}，平均心率 {record.HeartRate}，最大心率 {record.HeartRateMax}，最大摄氧量 {record.VO2Max}，温度 {record.Temperature}℃，湿度 {record.Humidity}%";
        _logger.Debug(_fullLog);

        // 添加成功后清空输入框
        TxtDistance.Text = string.Empty;
        TxtDuration.Text = string.Empty;
        TxtHeartRate.Text = string.Empty;
        TxtPace.Text = string.Empty;
        TxtVo2Max.Text = string.Empty;
    }

    private async void BtnRevert_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_lastInsertedId != 0)
            {
                var r = await _runningDataService.Delete(_lastInsertedId);
                if (r)
                {
                    SlideMessage.ShowMessage("成功删除最后添加的记录", MessageType.Success);
                    _lastInsertedId = 0; // 重置ID
                    _data = _runningDataService.LoadDataOfYear(_year);
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
        try
        {
            if (!_gitService.IsGitRepository())
            {
                ShowMessage("当前目录不是Git仓库。", MessageType.Error);
                return;
            }

            // 检查是否有未提交的更改
            string status = await _gitService.GetGitStatus();
            bool hasChanges = !string.IsNullOrWhiteSpace(status);

            // 检查是否有未推送的提交
            string unpushedCommits = await _gitService.GetUnpushedCommits();
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
                    string date = lastRun.Key.ToShortDateString();
                    await _gitService.CommitChanges($"{date} 跑步 {lastRun.Value.Sum(r => r.Distance):F2} 公里");
                }
                else
                {
                    ShowMessage("没有可发布的跑步记录。", MessageType.Error);
                    return;
                }
            }

            // 推送所有提交
            await _gitService.PushChanges();
            ShowMessage("成功发布更改。", MessageType.Success);
        }
        catch (Exception ex)
        {
            ShowMessage($"发布过程中出错: {ex.Message}", MessageType.Error);
            _logger.Error(ex.Message);
        }
    }

    private void BtnGenerateLog_OnClick(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_fullLog);
    }

    private bool ValidateInput(out RunRecordParams record)
    {
        record = new RunRecordParams();

        // 确保日期和距离有效
        if (!DpDate.SelectedDate.HasValue ||
            !double.TryParse(TxtDistance.Text, out double distance) ||
            (record.Date = DpDate.SelectedDate.Value).Year <= 0)
        {
            return false;
        }

        record.Distance = distance;

        // 解析时长格式
        (record.Duration, record.DurationSeconds) = ParseDuration(TxtDuration.Text);

        // 其他字段可以为空
        double.TryParse(TxtHeartRate.Text, out double heartRate);
        double.TryParse(TxtHeartRateMax.Text, out double heartRateMax);
        record.HeartRate = heartRate;
        record.HeartRateMax = heartRateMax;
        record.VO2Max = TxtVo2Max.Text;
        bool r = int.TryParse(TxtCadence.Text, out int cadence);
        record.Cadence = r ? cadence : 0;
        if (!string.IsNullOrEmpty(TxtPace.Text))
        {
            string[] pt = TxtPace.Text.Split(".");// 允许输入6.23，解析为 6′23″
            if (pt.Length == 2)
            {
                record.Pace = $"{pt[0]}′{pt[1]}″"; //6′46″
            }
            else if (pt.Length == 1)
            {
                record.Pace = $"{pt[0]}′00″"; //6′00″
            }
        }

        double.TryParse(TxtTemperature.Text, out double temperature);
        double.TryParse(TxtHumidity.Text, out double humidity);

        record.Temperature = temperature;
        record.Humidity = humidity;

        GetRunTimeOfDay();
        GetRunPlace();
        record.TimeOfDay = _timeOfDay;
        record.Place = _place;
        record.Notes = string.IsNullOrEmpty(TxtNotes.Text) ? null : TxtNotes.Text;

        return true;
    }

    private void GetRunPlace()
    {
        // 地点
        if (rbPlace1.IsChecked ?? false)
        {
            _place = rbPlace1.Content?.ToString() ?? string.Empty;
        }
        else if (rbPlace2.IsChecked ?? false)
        {
            _place = rbPlace2.Content?.ToString() ?? string.Empty;
        }
        else if (rbPlace3.IsChecked ?? false)
        {
            _place = txtOtherPlace.Text ?? string.Empty;
        }
    }

    private void GetRunTimeOfDay()
    {
        // 时间
        if (rbMorning.IsChecked ?? false)
        {
            _timeOfDay = rbMorning.Content?.ToString() ?? string.Empty;
        }

        if (rbAfternoon.IsChecked ?? false)
        {
            _timeOfDay = rbAfternoon.Content?.ToString() ?? string.Empty;
        }

        if (rbEvening.IsChecked ?? false)
        {
            _timeOfDay = rbEvening.Content?.ToString() ?? string.Empty;
        }
    }

    private static (string str, int total) ParseDuration(string durationString)
    {
        if (string.IsNullOrEmpty(durationString))
        {
            return ("00:00:00", 0);
        }

        int hours = 0, minutes, seconds = 0;
        int totalSeconds;

        // 使用 . 分隔输入
        var parts = durationString.Split('.');
        try
        {
            if (parts.Length == 3)
            {
                // HH:MM:SS
                hours = int.Parse(parts[0]);
                minutes = int.Parse(parts[1]);
                seconds = int.Parse(parts[2]);
            }
            else if (parts.Length == 2)
            {
                // MM:SS（不足一小时）
                minutes = int.Parse(parts[0]);
                seconds = int.Parse(parts[1]);
            }
            else if (parts.Length == 1)
            {
                // 只有一部分，按分钟（如 "45"）
                minutes = int.Parse(parts[0]);
            }
            else
            {
                throw new FormatException("Invalid duration format");
            }

            // 验证时间合法性
            if (hours < 0 || minutes < 0 || seconds < 0 ||
                minutes >= 60 || seconds >= 60)
            {
                throw new ArgumentOutOfRangeException("Invalid time components");
            }

            totalSeconds = hours * 3600 + minutes * 60 + seconds;

            // 统一格式化为 HH:MM:SS
            var formatted = $"{hours:D2}:{minutes:D2}:{seconds:D2}";

            return (formatted, totalSeconds);
        }
        catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is ArgumentException)
        {
            // 解析失败，返回默认值或抛出异常，根据需要调整
            return ("00:00:00", 0);
        }
    }

    private int UpdateDataAndSave(RunRecordParams record)
    {
        LogDistanceChange(record.Date, record.Distance);
        if (!_data.ContainsKey(record.Date))
        {
            _data[record.Date] = [];
        }

        var runData = new RunData
        {
            Date = record.Date,
            Distance = record.Distance,
            Duration = record.Duration,
            DurationSeconds = record.DurationSeconds,
            Pace = record.Pace,
            Cadence = record.Cadence,
            HeartRate = record.HeartRate,
            HeartRateMax = record.HeartRateMax,
            VO2Max = record.VO2Max,
            Temperature = record.Temperature,
            Humidity = record.Humidity,
            TimeOfDay = record.TimeOfDay,
            Place = record.Place,
            Notes = record.Notes,
        };

        _lastInsertedId = _runningDataService.Save(runData);

        _year = record.Date.Year;
        _data = _runningDataService.LoadDataOfYear(_year);

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
                // 检查该年份是否有跑步数据
                bool hasDataForYear = _runningDataService.DoesYearHasData(buttonYear);
                button.Visibility = hasDataForYear && buttonYear <= currentYear ? Visibility.Visible : Visibility.Collapsed;
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
        Plot plt = WpfPlot1.Plot;
        List<Bar> bars = [];
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
        [
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
            new(12, "Dec")
        ];

        plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);
        plt.Axes.Bottom.MajorTickStyle.Length = 0;
        plt.Axes.Margins(bottom: 0, top: .2);
        WpfPlot1.Plot.Font.Automatic();
        WpfPlot1.UserInputProcessor.IsEnabled = false;
        WpfPlot1.Refresh();
    }

    /// <summary>
    /// 绘制每月累计跑量折线图
    /// </summary>
    private void DrawMonthly()
    {
        var monthlyRecords = _runningDataService.GetMonthlyRunningRecords();
        Plot plot = new();

        DateTime[] dates = monthlyRecords
            .Select(r => DateTime.ParseExact(r.Month, "yyyy-MM", null))
            .ToArray();
        double[] ys = monthlyRecords.Select(r => r.CumulativeDistance).ToArray();
        plot.Add.Scatter(dates, ys);
        plot.Axes.DateTimeTicksBottom();
        plot.Title("Cumulative Running Distance Trend By Month", 15);
        plot.YLabel("Distance (km)", 13);
        plot.XLabel("Month", 13);
        //plot.Axes.SetLimitsY(0,ys.Max() * 1.5);
        plot.RenderManager.RenderStarting += (s, e) =>
        {
            Tick[] ticks1 = plot.Axes.Bottom.TickGenerator.Ticks;
            for (int i = 0; i < ticks1.Length; i++)
            {
                DateTime dt = DateTime.FromOADate(ticks1[i].Position);
                string label = $"{dt:yyMM}";
                ticks1[i] = new Tick(ticks1[i].Position, label);
            }
        };

        if (_isDarkMode)
        {
            plot.Add.Palette = new ScottPlot.Palettes.Penumbra();
            // change figure colors
            plot.FigureBackground.Color = Color.FromHex("#181818");
            plot.DataBackground.Color = Color.FromHex("#1f1f1f");

            // change axis and grid colors
            plot.Axes.Color(Color.FromHex("#d7d7d7"));
            plot.Grid.MajorLineColor = Color.FromHex("#404040");

            // change legend colors
            plot.Legend.BackgroundColor = Color.FromHex("#404040");
            plot.Legend.FontColor = Color.FromHex("#d7d7d7");
            plot.Legend.OutlineColor = Color.FromHex("#d7d7d7");
        }

        plot.Font.Automatic();
        string png = Path.Combine(_dataDir, $"CumulativeTrendByMonth.png");
        plot.SavePng(png, 790, 240);
    }

    /// <summary>
    /// 绘制每年累计跑量折线图
    /// </summary>
    private void DrawYearly()
    {
        var yearlyRecords = _runningDataService.GetYearlyRunningRecords();
        Plot plot = new();

        plot.Title("Cumulative Running Distance Trend By Year", 15);
        plot.YLabel("Distance (km)", 13);
        plot.XLabel("Year", 13);

        // create sample data
        int[] dataX = yearlyRecords.Select(x => x.Year).ToArray();
        double[] dataY = yearlyRecords.Select(x => x.CumulativeDistance).ToArray();
        //plot.Axes.SetLimitsY(0, dataY.Max() * 1.5);

        ScottPlot.TickGenerators.NumericAutomatic tickGenY = new();
        tickGenY.TargetTickCount = dataY.Length;
        plot.Axes.Bottom.TickGenerator = tickGenY;
        double[] tickPositions = [0, 250, 500, 750, 1000, 1250, 1500, 1750, 2000, 2250, 2500, 2750, 3000];
        string[] tickLabels = tickPositions.Select(x => x.ToString()).ToArray();
        plot.Axes.Left.SetTicks(tickPositions, tickLabels);

        if (_isDarkMode)
        {
            plot.Add.Palette = new ScottPlot.Palettes.Penumbra();
            // change figure colors
            plot.FigureBackground.Color = Color.FromHex("#181818");
            plot.DataBackground.Color = Color.FromHex("#1f1f1f");

            // change axis and grid colors
            plot.Axes.Color(Color.FromHex("#d7d7d7"));
            plot.Grid.MajorLineColor = Color.FromHex("#404040");

            // change legend colors
            plot.Legend.BackgroundColor = Color.FromHex("#404040");
            plot.Legend.FontColor = Color.FromHex("#d7d7d7");
            plot.Legend.OutlineColor = Color.FromHex("#d7d7d7");
        }

        plot.Add.Scatter(dataX, dataY);

        plot.Font.Automatic();
        string png1 = Path.Combine(_dataDir, $"CumulativeTrendByYear.png");
        plot.SavePng(png1, 790, 240);
    }

    private void ExportToCsv()
    {
        lock (_lock)
        {
            var records = _runningDataService.GetAllRunningRecords();
            var exportRecords = records.Select(r => new
            {
                DT = r.Date.ToString("yyyy-MM-dd"),
                Distance = r.Distance,
                HeartRate = r.HeartRate == 0 ? "" : r.HeartRate.ToString(),
                Pace = r.Pace
            }).ToList();

            string csvPath = Path.Combine(_config.MilesRepoDir, "running.csv");
            using var writer = new StreamWriter(csvPath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteRecords(exportRecords);
            _logger.Debug($"导出 {exportRecords.Count} 条记录到 CSV 文件 {csvPath}");
        }
    }
}

public static class ColorRanges
{
    // 距离分段更细致
    public const double VeryShortDistance = 2.0;   // 极短距离
    public const double ShortDistance = 3.5;       // 短距离
    public const double MediumDistance = 5.0;      // 中距离
    public const double MediumLongDistance = 6.5;  // 中长距离
    public const double LongDistance = 8.0;        // 长距离
    public const double MaxDistance = 10.0;        // 最大距离

    // 更丰富的颜色过渡
    public static readonly SKColor FrostColor = new(150, 220, 255);   // 霜蓝色 (1-2km)
    public static readonly SKColor ColdColor = new(100, 200, 255);    // 浅蓝色 (2-3.5km)
    public static readonly SKColor MildColor = new(144, 238, 144);    // 浅绿色 (3.5-5km)
    public static readonly SKColor WarmColor = new(255, 238, 0);      // 黄色   (5-6.5km)
    public static readonly SKColor HotColor = new(255, 160, 0);       // 橙色   (6.5-8km)
    public static readonly SKColor IntenseColor = new(255, 80, 0);    // 深橙色 (8-9km)
    public static readonly SKColor ExtremeColor = new(200, 0, 100);   // 紫色   (9-10km)
}