using Spc.Api.Models;

namespace Spc.Api.Services;

public class SpcService
{
    // Constants for X-bar and R chart control limits (for subgroup size n=5)
    // These are from standard SPC tables
    public static readonly Dictionary<int, (double A2, double D3, double D4, double d2)> _constants = new()
    {
        { 2, (0.577, 0, 3.267, 1.128) },
        { 3, (0.483, 0, 2.574, 1.693) },
        { 4, (0.419, 0, 2.282, 2.059) },
        { 5, (0.373, 0, 2.114, 2.326) },
        { 6, (0.337, 0, 2.004, 2.534) },
        { 7, (0.308, 0.076, 1.924, 2.704) },
        { 8, (0.285, 0.136, 1.864, 2.847) },
        { 9, (0.267, 0.184, 1.816, 2.970) },
        { 10, (0.253, 0.223, 1.777, 3.078) }
    };

    // Constants for S chart (B3, B4, c4) for subgroup sizes 2-10
    public static readonly Dictionary<int, (double B3, double B4, double c4)> _sChartConstants = new()
    {
        { 2, (0, 3.267, 0.7979) },
        { 3, (0, 2.568, 0.8862) },
        { 4, (0, 2.266, 0.9213) },
        { 5, (0, 2.089, 0.9400) },
        { 6, (0.030, 1.970, 0.9515) },
        { 7, (0.118, 1.882, 0.9594) },
        { 8, (0.185, 1.815, 0.9650) },
        { 9, (0.239, 1.761, 0.9693) },
        { 10, (0.284, 1.716, 0.9727) }
    };

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

    /// <summary>
    /// Check rules for X-bar chart (using standard deviation)
    /// </summary>
    public List<string> CheckXBarRules(List<double> xBarValues, double xBarMean, double rBar, int n)
    {
        var violatedRules = new List<string>();
        
        if (xBarValues.Count < 2) return violatedRules;
        
        // Calculate sigma from R-bar (estimated sigma = R-bar / d2)
        var d2 = _constants.GetValueOrDefault(n).d2;
        if (d2 == 0) d2 = 2.326; // default for n=5
        double sigma = rBar / d2;

        // Control limits for X-bar chart
        var A2 = _constants.GetValueOrDefault(n).A2;
        if (A2 == 0) A2 = 0.373; // default for n=5
        
        double ucl = xBarMean + A2 * rBar;
        double lcl = xBarMean - A2 * rBar;

        // Rule 1: Point beyond 3σ
        var lastXBar = xBarValues[^1];
        if (lastXBar > ucl || lastXBar < lcl)
            violatedRules.Add("Rule1");

        // Rule 2: 2 out of 3 beyond 2σ
        if (xBarValues.Count >= 3)
        {
            double twoSigma = 2 * A2 * rBar / 3;
            for (int i = 0; i <= xBarValues.Count - 3; i++)
            {
                int beyond = 0;
                for (int j = i; j < i + 3; j++)
                {
                    if (xBarValues[j] > xBarMean + twoSigma || xBarValues[j] < xBarMean - twoSigma)
                        beyond++;
                }
                if (beyond >= 2) violatedRules.Add("Rule2");
            }
        }

        // Rule 3: 4 out of 5 beyond 1σ
        if (xBarValues.Count >= 5)
        {
            double oneSigma = A2 * rBar / 3;
            for (int i = 0; i <= xBarValues.Count - 5; i++)
            {
                int beyond = 0;
                for (int j = i; j < i + 5; j++)
                {
                    if (xBarValues[j] > xBarMean + oneSigma || xBarValues[j] < xBarMean - oneSigma)
                        beyond++;
                }
                if (beyond >= 4) violatedRules.Add("Rule3");
            }
        }

        // Rule 4: 8 consecutive on one side
        if (xBarValues.Count >= 8)
        {
            int above = 0, below = 0;
            for (int i = xBarValues.Count - 8; i < xBarValues.Count; i++)
            {
                if (xBarValues[i] > xBarMean) above++;
                else if (xBarValues[i] < xBarMean) below++;
            }
            if (above == 8 || below == 8) violatedRules.Add("Rule4");
        }

        return violatedRules.Distinct().ToList();
    }

    /// <summary>
    /// Check rules for R chart
    /// </summary>
    public List<string> CheckRRules(List<double> rValues, double rBar, int n)
    {
        var violatedRules = new List<string>();
        
        if (rValues.Count < 2) return violatedRules;

        var constants = _constants.GetValueOrDefault(n);
        double D3 = constants.D3;
        double D4 = constants.D4;
        
        if (D3 == 0) D3 = 0; // For n <= 6, D3 = 0
        if (D4 == 0) D4 = 2.114; // default for n=5

        double ucl = D4 * rBar;
        double lcl = D3 * rBar;

        // Rule 1: Point beyond control limits
        var lastR = rValues[^1];
        if (lastR > ucl || (lcl > 0 && lastR < lcl))
            violatedRules.Add("Rule1");

        // Rule 4: 8 consecutive on one side
        if (rValues.Count >= 8)
        {
            int above = 0, below = 0;
            for (int i = rValues.Count - 8; i < rValues.Count; i++)
            {
                if (rValues[i] > rBar) above++;
                else if (rValues[i] < rBar) below++;
            }
            if (above == 8 || below == 8) violatedRules.Add("Rule4");
        }

        return violatedRules.Distinct().ToList();
    }

    /// <summary>
    /// Check rules for S chart (standard deviation chart)
    /// </summary>
    public List<string> CheckSRules(List<double> sValues, double sBar, int n)
    {
        var violatedRules = new List<string>();
        
        if (sValues.Count < 2) return violatedRules;

        var constants = _sChartConstants.GetValueOrDefault(n);
        double B3 = constants.B3;
        double B4 = constants.B4;
        
        if (B3 == 0) B3 = 0; 
        if (B4 == 0) B4 = 2.089; // default for n=5

        double ucl = B4 * sBar;
        double lcl = B3 * sBar;

        // Rule 1: Point beyond control limits
        var lastS = sValues[^1];
        if (lastS > ucl || (lcl > 0 && lastS < lcl))
            violatedRules.Add("Rule1");

        // Rule 4: 8 consecutive on one side
        if (sValues.Count >= 8)
        {
            int above = 0, below = 0;
            for (int i = sValues.Count - 8; i < sValues.Count; i++)
            {
                if (sValues[i] > sBar) above++;
                else if (sValues[i] < sBar) below++;
            }
            if (above == 8 || below == 8) violatedRules.Add("Rule4");
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

    /// <summary>
    /// Generate subgroup data for X-bar and R chart simulation
    /// </summary>
    public List<double> GenerateSubgroupValues(int subgroupSize, double targetMean, double targetStdDev, bool includeAnomaly = false)
    {
        var values = new List<double>();
        var random = new Random();
        
        for (int i = 0; i < subgroupSize; i++)
        {
            double u1 = 1.0 - random.NextDouble();
            double u2 = 1.0 - random.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            double value = targetMean + (targetStdDev * randStdNormal);
            
            // 5% chance of anomaly in any measurement
            if (includeAnomaly || random.NextDouble() < 0.05)
            {
                value += (random.NextDouble() > 0.5 ? 1 : -1) * 3 * targetStdDev;
            }
            
            values.Add(Math.Round(value, 4));
        }
        
        return values;
    }
}
