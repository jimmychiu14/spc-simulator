using Spc.Api.Models;

namespace Spc.Api.Services;

public class SpcService
{
    /// <summary>
    /// Western Electric Rules (1-4 basic rules)
    /// </summary>
    public List<string> CheckRules(List<double> values, double mean, double stdDev, double ucl, double lcl)
    {
        var violatedRules = new List<string>();
        
        if (values.Count < 3 || stdDev == 0) return violatedRules;
        
        double oneSigma = stdDev;
        double twoSigma = 2 * stdDev;
        double threeSigma = 3 * stdDev;

        // Rule 1: Single point beyond 3σ
        var lastValue = values[^1];
        if (lastValue > ucl || lastValue < lcl)
        {
            violatedRules.Add("Rule1");
        }

        // Rule 2: 2 out of 3 consecutive points beyond 2σ on same side
        if (values.Count >= 3)
        {
            for (int i = 0; i <= values.Count - 3; i++)
            {
                int beyondCount = 0;
                double? side = null;
                for (int j = i; j < i + 3; j++)
                {
                    if (values[j] > mean + twoSigma) { beyondCount++; side = 1; }
                    else if (values[j] < mean - twoSigma) { beyondCount++; side = -1; }
                }
                if (beyondCount >= 2) violatedRules.Add("Rule2");
            }
        }

        // Rule 3: 4 out of 5 consecutive points beyond 1σ on same side
        if (values.Count >= 5)
        {
            for (int i = 0; i <= values.Count - 5; i++)
            {
                int beyondCount = 0;
                for (int j = i; j < i + 5; j++)
                {
                    if (values[j] > mean + oneSigma || values[j] < mean - oneSigma)
                        beyondCount++;
                }
                if (beyondCount >= 4) violatedRules.Add("Rule3");
            }
        }

        // Rule 4: 8 consecutive points on one side of center line
        if (values.Count >= 8)
        {
            int aboveCount = 0;
            int belowCount = 0;
            for (int i = values.Count - 8; i < values.Count; i++)
            {
                if (values[i] > mean) aboveCount++;
                else if (values[i] < mean) belowCount++;
            }
            if (aboveCount == 8 || belowCount == 8) violatedRules.Add("Rule4");
        }

        return violatedRules.Distinct().ToList();
    }

    public double CalculateCPK(double usl, double lsl, double mean, double stdDev)
    {
        if (stdDev == 0) return 0;
        var cpu = (usl - mean) / (3 * stdDev);
        var cpl = (mean - lsl) / (3 * stdDev);
        return Math.Min(cpu, cpl);
    }

    public double CalculatePPK(double usl, double lsl, double mean, double stdDev)
    {
        if (stdDev == 0) return 0;
        // For PPK, we use sample std dev (n-1), simplified here
        var ppu = (usl - mean) / (3 * stdDev);
        var ppl = (mean - lsl) / (3 * stdDev);
        return Math.Min(ppu, ppl);
    }

    /// <summary>
    /// Generate random measurement data for simulation
    /// </summary>
    public double GenerateRandomValue(double targetMean, double targetStdDev, bool includeAnomaly = false)
    {
        var random = new Random();
        double u1 = 1.0 - random.NextDouble();
        double u2 = 1.0 - random.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        
        var value = targetMean + (targetStdDev * randStdNormal);
        
        // 5% chance of anomaly (out of control)
        if (includeAnomaly || random.NextDouble() < 0.05)
        {
            value += (random.NextDouble() > 0.5 ? 1 : -1) * 4 * targetStdDev;
        }
        
        return Math.Round(value, 4);
    }
}
