namespace Spc.Api.Models;

public class Measurement
{
    public long Id { get; set; }
    public string MachineId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
    public int SubgroupIndex { get; set; } = 0; // Which subgroup this belongs to
}

/// <summary>
/// Request with multiple values (subgroup)
/// </summary>
public class SubgroupRequest
{
    public string MachineId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public List<double> Values { get; set; } = new();
    public DateTime? Timestamp { get; set; }
}

public class JudgeRequest
{
    public string MachineId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime? Timestamp { get; set; }
}

public class JudgeResponse
{
    public string Status { get; set; } = "OK";
    public List<string> RuleViolated { get; set; } = new();
    public double? Cpk { get; set; }
    public double? Ppk { get; set; }
    public double? Mean { get; set; }
    public double? StdDev { get; set; }
    public double? Ucl { get; set; }
    public double? Lcl { get; set; }
}

/// <summary>
/// Response for X-bar and R chart or X-bar and S chart
/// </summary>
public class XBarRChartResponse
{
    // X-bar chart data
    public string XBarStatus { get; set; } = "OK";
    public List<string> XBarRules { get; set; } = new();
    public double? XBarMean { get; set; }
    public double? XBarUcl { get; set; }
    public double? XBarLcl { get; set; }
    public double? XBarCpk { get; set; }
    public List<SubgroupData> XBarData { get; set; } = new();

    // R chart data
    public string RStatus { get; set; } = "OK";
    public List<string> RRules { get; set; } = new();
    public double? RMean { get; set; }
    public double? RUcl { get; set; }
    public double? RLcl { get; set; }
    public List<SubgroupData> RData { get; set; } = new();

    // S chart data (standard deviation)
    public string SStatus { get; set; } = "OK";
    public List<string> SRules { get; set; } = new();
    public double? SMean { get; set; }
    public double? SUcl { get; set; }
    public double? SLcl { get; set; }
    public List<SubgroupData> SData { get; set; } = new();

    // Chart type: "R" or "S"
    public string ChartType { get; set; } = "R";

    // Combined stats
    public double? OverallMean { get; set; }
    public double? OverallSigma { get; set; }
    public int SubgroupSize { get; set; } = 5; // Default n=5
}

public class SubgroupData
{
    public int SubgroupIndex { get; set; }
    public double Value { get; set; } // X-bar or R value
    public DateTime Timestamp { get; set; }
}

public class ControlLimit
{
    public double UCL { get; set; }
    public double CL { get; set; }
    public double LCL { get; set; }
}
