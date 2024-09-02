using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;

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
}