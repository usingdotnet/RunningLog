using System.Data.SQLite;
using System.IO;
using Dapper;
using Dapper.Contrib.Extensions;

namespace RunningLog;

internal class RunningDataService
{
    public string DataDir { get; set; }

    private readonly string _conStr;

    public RunningDataService(string dataDir)
    {
        DataDir = dataDir;
        string dbPath = Path.Combine(DataDir, "RunningLog.db");
        _conStr = $"Data Source={dbPath};Version=3;";
    }

    public Dictionary<DateTime, List<RunData>> LoadDataOfYear(int year)
    {
        using var connection = new SQLiteConnection(_conStr);
        connection.Open();
        var startDate = new DateTime(year, 1, 1);
        var endDate = new DateTime(year + 1, 1, 1); // 下一年的1月1日
        var data = connection.Query<RunData>("SELECT * FROM RunData WHERE Date >= @StartDate AND Date < @EndDate", new { StartDate = startDate.ToString("yyyy-MM-dd"), EndDate = endDate.ToString("yyyy-MM-dd") })
            .GroupBy(r => r.Date)
            .ToDictionary(g => g.Key, g => g.ToList());
        return data;
    }

    public List<MonthlyRunningRecord> GetMonthlyRunningRecords()
    {
        using var connection = new SQLiteConnection(_conStr);
        connection.Open();
        var records = connection.Query<MonthlyRunningRecord>("SELECT * FROM '按月累计统计' ORDER BY Month").ToList();
        return records;
    }

    public List<YearlyRunningRecord> GetYearlyRunningRecords()
    {
        using var connection = new SQLiteConnection(_conStr);
        connection.Open();
        var records = connection.Query<YearlyRunningRecord>("SELECT * FROM '按年累计统计' ORDER BY Year").ToList();
        return records;
    }

    public RunDataSummary GetRunDataSummary()
    {
        using var connection = new SQLiteConnection(_conStr);
        connection.Open();
        var summary = connection.QuerySingle<RunDataSummary>("SELECT * FROM '总里程'");
        return summary;
    }

    public int Save(RunData runData)
    {
        using var connection = new SQLiteConnection(_conStr);
        connection.Open();
        var lastInsertedId = (int)connection.Insert(runData); // 返回新插入记录的ID
        return lastInsertedId;
    }

    public bool DoesYearHasData(int year)
    {
        using var connection = new SQLiteConnection(_conStr);
        connection.Open();
        var startDate = new DateTime(year, 1, 1);
        var endDate = new DateTime(year + 1, 1, 1); // 下一年的1月1日
        var c = connection.ExecuteScalar<int>(
            "SELECT count(*) FROM RunData WHERE Date >= @StartDate AND Date < @EndDate",
            new { StartDate = startDate.ToString("yyyy-MM-dd"), EndDate = endDate.ToString("yyyy-MM-dd") });

        return c > 0;
    }

    public async Task<bool> Delete(int lastInsertedId)
    {
        await using var connection = new SQLiteConnection(_conStr);
        connection.Open();
        var runDataToDelete = new RunData { Id = lastInsertedId };
        return await connection.DeleteAsync(runDataToDelete);
    }
}