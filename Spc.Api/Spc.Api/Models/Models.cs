namespace Spc.Api.Models;

public class Measurement
{
    public long Id { get; set; }
    public string MachineId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
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

public class ControlLimit
{
    public double UCL { get; set; }
    public double CL { get; set; }
    public double LCL { get; set; }
}
