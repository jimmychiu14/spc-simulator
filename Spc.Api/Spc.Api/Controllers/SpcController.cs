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
}
