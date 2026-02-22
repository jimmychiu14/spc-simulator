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
        
        var result = await SubmitSubgroup(request);
        result.ChartType = "R";
        return result;
    }

    /// <summary>
    /// Simulate subgroup data for X-bar and S chart
    /// </summary>
    [HttpGet("simulate-xbar-s")]
    public async Task<XBarRChartResponse> SimulateXBarSChart(
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
        
        var result = await SubmitSubgroup(request);
        result.ChartType = "S";
        return result;
    }

    /// <summary>
    /// Get X-bar and R chart data or X-bar and S chart data
    /// </summary>
    [HttpGet("xbar-r-data")]
    public async Task<XBarRChartResponse> GetXBarRChartData(
        [FromQuery] string machineId = "M001", 
        [FromQuery] string itemName = "Thickness",
        [FromQuery] int subgroupSize = 5,
        [FromQuery] int numSubgroups = 15,
        [FromQuery] string chartType = "R")
    {
        var result = await CalculateXBarRChart(machineId, itemName, subgroupSize, numSubgroups);
        result.ChartType = chartType;
        return result;
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

        // Calculate X-bar, R, and S for each subgroup
        var xBarValues = new List<double>();
        var rValues = new List<double>();
        var sValues = new List<double>();
        var timestamps = new List<DateTime>();
        var xBarDataList = new List<SubgroupData>();
        var rDataList = new List<SubgroupData>();
        var sDataList = new List<SubgroupData>();

        for (int i = 0; i < groupedData.Count && i < numSubgroups; i++)
        {
            var subgroup = groupedData[i];
            double xBar = subgroup.Average();
            double r = subgroup.Max() - subgroup.Min();
            
            // Calculate standard deviation for S chart
            double sumSq = subgroup.Sum(x => Math.Pow(x - xBar, 2));
            double s = Math.Sqrt(sumSq / (subgroupSize - 1));
            
            xBarValues.Add(xBar);
            rValues.Add(r);
            sValues.Add(s);
            
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

            sDataList.Add(new SubgroupData
            {
                SubgroupIndex = i + 1,
                Value = Math.Round(s, 4),
                Timestamp = ts
            });
        }

        // Calculate R-bar (average range)
        double rBar = rValues.Average();
        
        // Calculate S-bar (average standard deviation)
        double sBar = sValues.Average();
        
        // Calculate X-bar-bar (average of subgroup means)
        double xBarBar = xBarValues.Average();

        // Get constants for R chart
        var constants = SpcService._constants.GetValueOrDefault(subgroupSize);
        double A2 = constants.A2 > 0 ? constants.A2 : 0.373; // default
        double D3 = constants.D3;
        double D4 = constants.D4 > 0 ? constants.D4 : 2.114; // default

        // Get constants for S chart
        var sConstants = SpcService._sChartConstants.GetValueOrDefault(subgroupSize);
        double B3 = sConstants.B3;
        double B4 = sConstants.B4 > 0 ? sConstants.B4 : 2.089; // default
        double c4 = sConstants.c4 > 0 ? sConstants.c4 : 0.94; // default

        // X-bar chart control limits (using S-bar)
        double xBarUcl = xBarBar + A2 * rBar;
        double xBarLcl = xBarBar - A2 * rBar;

        // R chart control limits
        double rUcl = D4 * rBar;
        double rLcl = D3 * rBar;

        // S chart control limits
        double sUcl = B4 * sBar;
        double sLcl = B3 * sBar;

        // Check rules
        var xBarRules = _spcService.CheckXBarRules(xBarValues, xBarBar, rBar, subgroupSize);
        var rRules = _spcService.CheckRRules(rValues, rBar, subgroupSize);
        var sRules = _spcService.CheckSRules(sValues, sBar, subgroupSize);

        // Calculate estimated sigma using c4
        double estimatedSigma = sBar / c4;

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

        // S chart response
        response.SStatus = sRules.Count > 0 ? "OUT_OF_CONTROL" : "OK";
        response.SRules = sRules;
        response.SMean = Math.Round(sBar, 4);
        response.SUcl = Math.Round(sUcl, 4);
        response.SLcl = Math.Round(sLcl, 4);
        response.SData = sDataList;

        response.OverallMean = Math.Round(xBarBar, 4);
        response.OverallSigma = Math.Round(estimatedSigma, 4);

        return response;
    }

    /// <summary>
    /// Import CSV data for X-bar chart analysis
    /// CSV format: Value,Timestamp (header required)
    /// </summary>
    [HttpPost("import-csv")]
    public async Task<ImportResult> ImportCsv(
        IFormFile file,
        [FromQuery] string machineId = "M001",
        [FromQuery] string itemName = "Thickness",
        [FromQuery] int subgroupSize = 5)
    {
        var result = new ImportResult();

        if (file == null || file.Length == 0)
        {
            result.Success = false;
            result.Message = "No file uploaded";
            return result;
        }

        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var lines = new List<string>();
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line);
            }

            if (lines.Count < 2)
            {
                result.Success = false;
                result.Message = "CSV file must have header and at least one data row";
                return result;
            }

            // Parse header
            var header = lines[0].ToLower().Split(',').Select(h => h.Trim()).ToArray();
            
            // Find column indices
            int valueIndex = Array.IndexOf(header, "value");
            int timestampIndex = Array.IndexOf(header, "timestamp");

            if (valueIndex < 0)
            {
                result.Success = false;
                result.Message = "CSV must have a 'Value' column";
                return result;
            }

            // Parse data
            var values = new List<double>();
            var timestamps = new List<DateTime>();
            var errors = new List<string>();

            for (int i = 1; i < lines.Count; i++)
            {
                try
                {
                    var cols = lines[i].Split(',');
                    if (cols.Length <= valueIndex) continue;

                    if (!double.TryParse(cols[valueIndex].Trim(), out double value))
                    {
                        errors.Add($"Row {i + 1}: Invalid value '{cols[valueIndex]}'");
                        continue;
                    }

                    values.Add(value);

                    // Parse timestamp if available
                    DateTime timestamp = DateTime.Now;
                    if (timestampIndex >= 0 && cols.Length > timestampIndex)
                    {
                        if (DateTime.TryParse(cols[timestampIndex].Trim(), out DateTime parsedTs))
                        {
                            timestamp = parsedTs;
                        }
                    }
                    timestamps.Add(timestamp);
                }
                catch (Exception ex)
                {
                    errors.Add($"Row {i + 1}: {ex.Message}");
                }
            }

            if (values.Count == 0)
            {
                result.Success = false;
                result.Message = "No valid data rows found";
                result.Errors = errors;
                return result;
            }

            // Group values into subgroups
            int subgroupCount = (int)Math.Ceiling((double)values.Count / subgroupSize);
            int recordCount = 0;

            for (int sg = 0; sg < subgroupCount; sg++)
            {
                var subgroupValues = values.Skip(sg * subgroupSize).Take(subgroupSize).ToList();
                var subgroupTimestamps = timestamps.Skip(sg * subgroupSize).Take(subgroupSize).ToList();

                // Use middle timestamp for the subgroup
                var timestamp = subgroupTimestamps.Count > 0 
                    ? subgroupTimestamps[subgroupTimestamps.Count / 2] 
                    : DateTime.Now;

                // Save each measurement
                for (int j = 0; j < subgroupValues.Count; j++)
                {
                    var measurement = new Measurement
                    {
                        MachineId = machineId,
                        ItemName = itemName,
                        Value = subgroupValues[j],
                        Timestamp = timestamp,
                        SubgroupIndex = j
                    };
                    await _repository.AddMeasurementAsync(measurement);
                    recordCount++;
                }
            }

            result.Success = true;
            result.Message = $"Successfully imported {recordCount} records into {subgroupCount} subgroups";
            result.RecordsImported = recordCount;
            result.SubgroupsCreated = subgroupCount;
            result.Errors = errors;

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Error processing CSV: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Clear all measurement data or data for specific machine/item
    /// </summary>
    [HttpDelete("data")]
    public async Task<object> ClearData(
        [FromQuery] string? machineId = null,
        [FromQuery] string? itemName = null)
    {
        int deleted = await _repository.ClearMeasurementsAsync(machineId, itemName);
        
        if (!string.IsNullOrEmpty(machineId) && !string.IsNullOrEmpty(itemName))
        {
            return new { success = true, message = $"Deleted {deleted} records for {machineId}/{itemName}" };
        }
        else if (!string.IsNullOrEmpty(machineId))
        {
            return new { success = true, message = $"Deleted {deleted} records for {machineId}" };
        }
        else
        {
            return new { success = true, message = $"Deleted all {deleted} records" };
        }
    }
}
