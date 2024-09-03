using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Dapper.Contrib.Extensions;

namespace RunningLog;

internal class RunningDataService
{
    public string DataDir { get; set; }

    public Dictionary<DateTime, List<RunData>> LoadData(int year)
    {
        string dbPath = Path.Combine(DataDir, "RunningLog.db");
        using (var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
        {
            connection.Open();
            var startDate = new DateTime(year, 1, 1);
            var endDate = new DateTime(year + 1, 1, 1); // 下一年的1月1日
            var data = connection.Query<RunData>("SELECT * FROM RunData WHERE Date >= @StartDate AND Date < @EndDate", new { StartDate = startDate.ToString("yyyy-MM-dd"), EndDate = endDate.ToString("yyyy-MM-dd") })
                .GroupBy(r => DateTime.Parse(r.Date))
                .ToDictionary(g => g.Key, g => g.ToList());
            return data;
        }
    }

    public int Save(RunData runData)
    {
        using (var connection = new SQLiteConnection($"Data Source={Path.Combine(DataDir, "RunningLog.db")};Version=3;"))
        {
            connection.Open();
            var lastInsertedId = (int)connection.Insert(runData); // 返回新插入记录的ID
            return lastInsertedId;
        }
    }

    public bool DoesYearHasData(int year)
    {
        string dbPath = Path.Combine(DataDir, "RunningLog.db");
        using (var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
        {
            connection.Open();
            var startDate = new DateTime(year, 1, 1);
            var endDate = new DateTime(year + 1, 1, 1); // 下一年的1月1日
            var c = connection.ExecuteScalar<int>(
                "SELECT count(*) FROM RunData WHERE Date >= @StartDate AND Date < @EndDate",
                new { StartDate = startDate.ToString("yyyy-MM-dd"), EndDate = endDate.ToString("yyyy-MM-dd") });

            return c > 0;
        }
    }

    public async Task<bool> Delete(int lastInsertedId)
    {
        await using (var connection = new SQLiteConnection($"Data Source={Path.Combine(DataDir, "RunningLog.db")};Version=3;"))
        {
            connection.Open();
            var runDataToDelete = new RunData { Id = lastInsertedId };
            return await connection.DeleteAsync(runDataToDelete);
        }
    }
}