using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Safety;

public sealed record SensorSample(SensorReading Reading, float SessionMax)
{
    public string Key => $"{Reading.Component}|{Reading.HardwareName}|{Reading.Kind}|{Reading.SensorName}";
}

/// <summary>Polls <see cref="ISensorProvider"/> and remembers the highest value each sensor has
/// reached, the way HWMonitor does — a temperature spike during a benchmark is the interesting
/// number, and it is gone by the time anyone looks at the current reading.</summary>
public sealed class SensorMonitor(ISensorProvider provider)
{
    private readonly Dictionary<string, float> _sessionMax = [];

    public bool IsAvailable => provider.IsAvailable;
    public string? UnavailableReason => provider.UnavailableReason;

    public IReadOnlyList<SensorSample> Poll()
    {
        var samples = new List<SensorSample>();

        foreach (var reading in provider.Read())
        {
            var key = $"{reading.Component}|{reading.HardwareName}|{reading.Kind}|{reading.SensorName}";
            var max = _sessionMax.TryGetValue(key, out var previous) && previous > reading.Value
                ? previous
                : reading.Value;

            _sessionMax[key] = max;
            samples.Add(new SensorSample(reading, max));
        }

        return samples;
    }

    public void ResetSessionMax() => _sessionMax.Clear();
}
