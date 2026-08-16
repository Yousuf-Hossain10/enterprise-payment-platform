using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Messaging;

public class OutboxDispatcherOptions
{
    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 50;
}
