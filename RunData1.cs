using Dapper.Contrib.Extensions;

namespace RunningLog;

[Table("RunData")]
public class RunData1:RunDataBase
{
    public int Id { get; set; }


    public string Date { get; set; }
}