using System;
using System.Collections.Generic;
using System.Text;

namespace RunningLog;

public class YearlyRunningRecord
{
    public int Year { get; set; }

    public int DaysRun { get; set; }

    public int CumulativeDaysRun { get; set; }

    public double TotalDistance { get; set; }

    public double CumulativeDistance { get; set; }
}