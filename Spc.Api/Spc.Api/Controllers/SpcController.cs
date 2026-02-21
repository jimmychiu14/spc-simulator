using Microsoft.AspNetCore.Mvc;
using Spc.Api.Data;
using Spc.Api.Models;
using Spc.Api.Services;

namespace Spc.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SpcController : ControllerBase
{
    private readonly SpcRepository _repository;
    private readonly SpcService _spcService;

    public SpcController()
    {
        _repository = new SpcRepository();
        _spcService = new SpcService();
    }

    [HttpPost("judge")]
    public async Task<JudgeResponse> Judge([FromBody] JudgeRequest request)
    {
        var timestamp = request.Timestamp ?? DateTime.Now;
        
        // Save measurement
        var measurement = new Measurement
        {
            MachineId = request.MachineId,
            ItemName = request.ItemName,
            Value = request.Value,
            Timestamp = timestamp
        };
        
        await _repository.AddMeasurementAsync(measurement);

        // Get recent measurements for analysis
        var recentData = await _repository.GetRecentMeasurementsAsync(request.MachineId, request.ItemName, 30);
        
        if (recentData.Count < 2)
        {
            return new JudgeResponse
            {
                Status = "OK",
                Mean = request.Value
            };
        }

        var values = recentData.Select(m => m.Value).ToList();
        var (mean, stdDev, ucl, lcl) = await _repository.GetStatisticsAsync(request.MachineId, request.ItemName, 30);

        // Check rules
        var violatedRules = _spcService.CheckRules(values, mean, stdDev, ucl ?? 0, lcl ?? 0);

        // Determine status
        string status = "OK";
        if (violatedRules.Count > 0)
        {
            status = "OUT_OF_CONTROL";
        }
        else if (values.Count >= 3 && Math.Abs(values[^1] - mean) > 2 * stdDev)
        {
            status = "WARNING";
        }

        // Calculate CPK (assuming ±3σ as USL/LSL for demo)
        double? cpk = null;
        if (ucl.HasValue && lcl.HasValue && stdDev > 0)
        {
            var usl = mean + 3 * stdDev;
            var lsl = mean - 3 * stdDev;
            cpk = _spcService.CalculateCPK(usl, lsl, mean, stdDev);
        }

        return new JudgeResponse
        {
            Status = status,
            RuleViolated = violatedRules,
            Cpk = Math.Round(cpk ?? 0, 3),
            Mean = Math.Round(mean, 4),
            StdDev = Math.Round(stdDev, 4),
            Ucl = ucl.HasValue ? Math.Round(ucl.Value, 4) : null,
            Lcl = lcl.HasValue ? Math.Round(lcl.Value, 4) : null
        };
    }

    [HttpGet("data")]
    public async Task<List<Measurement>> GetData([FromQuery] string machineId, [FromQuery] string itemName, [FromQuery] int limit = 30)
    {
        return await _repository.GetRecentMeasurementsAsync(machineId, itemName, limit);
    }

    [HttpGet("simulate")]
    public async Task<JudgeResponse> Simulate([FromQuery] string machineId = "M001", [FromQuery] string itemName = "Thickness")
    {
        // Generate random value
        double targetMean = 100.0;
        double targetStdDev = 2.0;
        var value = _spcService.GenerateRandomValue(targetMean, targetStdDev);
        
        var request = new JudgeRequest
        {
            MachineId = machineId,
            ItemName = itemName,
            Value = value,
            Timestamp = DateTime.Now
        };
        
        return await Judge(request);
    }

    /// <summary>
    /// Submit subgroup data for X-bar and R chart analysis
    /// </summary>
    [HttpPost("subgroup")]
    public async Task<XBarRChartResponse> SubmitSubgroup([FromBody] SubgroupRequest request)
    {
        var timestamp = request.Timestamp ?? DateTime.Now;
        
        // Save each measurement in the subgroup
        for (int i = 0; i < request.Values.Count; i++)
        {
            var measurement = new Measurement
            {
                MachineId = request.MachineId,
                ItemName = request.ItemName,
                Value = request.Values[i],
                Timestamp = timestamp,
                SubgroupIndex = i
            };
            await _repository.AddMeasurementAsync(measurement);
        }

        return await CalculateXBarRChart(request.MachineId, request.ItemName, request.Values.Count);
    }

    /// <summary>
    /// Simulate subgroup data for X-bar and R chart
    /// </summary>
    [HttpGet("simulate-xbar-r")]
    public async Task<XBarRChartResponse> SimulateXBarRChart(
        [FromQuery] string machineId = "M001", 
        [FromQuery] string itemName = "Thickness",
        [FromQuery] int subgroupSize = 5)
    {
        // Generate subgroup data
        double targetMean = 100.0;
        double targetStdDev = 2.0;
        
        var values = _spcService.GenerateSubgroupValues(subgroupSize, targetMean, targetStdDev);
        
        var request = new SubgroupRequest
        {
            MachineId = machineId,
            ItemName = itemName,
            Values = values,
            Timestamp = DateTime.Now
        };
        
        return await SubmitSubgroup(request);
    }

    /// <summary>
    /// Get X-bar and R chart data
    /// </summary>
    [HttpGet("xbar-r-data")]
    public async Task<XBarRChartResponse> GetXBarRChartData(
        [FromQuery] string machineId = "M001", 
        [FromQuery] string itemName = "Thickness",
        [FromQuery] int subgroupSize = 5,
        [FromQuery] int numSubgroups = 20)
    {
        return await CalculateXBarRChart(machineId, itemName, subgroupSize, numSubgroups);
    }

    private async Task<XBarRChartResponse> CalculateXBarRChart(string machineId, string itemName, int subgroupSize, int numSubgroups = 20)
    {
        var response = new XBarRChartResponse
        {
            SubgroupSize = subgroupSize
        };

        // Get recent measurements grouped by timestamp
        var allData = await _repository.GetRecentMeasurementsAsync(machineId, itemName, subgroupSize * numSubgroups);
        
        if (allData.Count < subgroupSize)
        {
            // Not enough data, return empty
            return response;
        }

        // Group data by approximate time periods
        var groupedData = new List<List<double>>();
        for (int i = 0; i < allData.Count; i += subgroupSize)
        {
            var subgroup = allData.Skip(i).Take(subgroupSize).Select(m => m.Value).ToList();
            if (subgroup.Count == subgroupSize)
            {
                groupedData.Add(subgroup);
            }
        }

        // Calculate X-bar and R for each subgroup
        var xBarValues = new List<double>();
        var rValues = new List<double>();
        var timestamps = new List<DateTime>();
        var xBarDataList = new List<SubgroupData>();
        var rDataList = new List<SubgroupData>();

        for (int i = 0; i < groupedData.Count && i < numSubgroups; i++)
        {
            var subgroup = groupedData[i];
            double xBar = subgroup.Average();
            double r = subgroup.Max() - subgroup.Min();
            
            xBarValues.Add(xBar);
            rValues.Add(r);
            
            var ts = allData.Count > i * subgroupSize + subgroupSize / 2 
                ? allData[i * subgroupSize + subgroupSize / 2].Timestamp 
                : DateTime.Now;
            timestamps.Add(ts);

            xBarDataList.Add(new SubgroupData
            {
                SubgroupIndex = i + 1,
                Value = Math.Round(xBar, 4),
                Timestamp = ts
            });

            rDataList.Add(new SubgroupData
            {
                SubgroupIndex = i + 1,
                Value = Math.Round(r, 4),
                Timestamp = ts
            });
        }

        // Calculate R-bar (average range)
        double rBar = rValues.Average();
        
        // Calculate X-bar-bar (average of subgroup means)
        double xBarBar = xBarValues.Average();

        // Get constants for subgroup size
        var constants = SpcService._constants.GetValueOrDefault(subgroupSize);
        double A2 = constants.A2 > 0 ? constants.A2 : 0.373; // default
        double D3 = constants.D3;
        double D4 = constants.D4 > 0 ? constants.D4 : 2.114; // default
        double d2 = constants.d2 > 0 ? constants.d2 : 2.326; // default

        // X-bar chart control limits
        double xBarUcl = xBarBar + A2 * rBar;
        double xBarLcl = xBarBar - A2 * rBar;

        // R chart control limits
        double rUcl = D4 * rBar;
        double rLcl = D3 * rBar;

        // Check rules
        var xBarRules = _spcService.CheckXBarRules(xBarValues, xBarBar, rBar, subgroupSize);
        var rRules = _spcService.CheckRRules(rValues, rBar, subgroupSize);

        // Calculate estimated sigma
        double estimatedSigma = rBar / d2;

        // Calculate CPK (using estimated sigma)
        double? xBarCpk = null;
        double usl = xBarBar + 3 * estimatedSigma;
        double lsl = xBarBar - 3 * estimatedSigma;
        if (estimatedSigma > 0)
        {
            xBarCpk = _spcService.CalculateCPK(usl, lsl, xBarBar, estimatedSigma);
        }

        // Build response
        response.XBarStatus = xBarRules.Count > 0 ? "OUT_OF_CONTROL" : "OK";
        response.XBarRules = xBarRules;
        response.XBarMean = Math.Round(xBarBar, 4);
        response.XBarUcl = Math.Round(xBarUcl, 4);
        response.XBarLcl = Math.Round(xBarLcl, 4);
        response.XBarCpk = Math.Round(xBarCpk ?? 0, 4);
        response.XBarData = xBarDataList;

        response.RStatus = rRules.Count > 0 ? "OUT_OF_CONTROL" : "OK";
        response.RRules = rRules;
        response.RMean = Math.Round(rBar, 4);
        response.RUcl = Math.Round(rUcl, 4);
        response.RLcl = Math.Round(rLcl, 4);
        response.RData = rDataList;

        response.OverallMean = Math.Round(xBarBar, 4);
        response.OverallSigma = Math.Round(estimatedSigma, 4);

        return response;
    }
}
