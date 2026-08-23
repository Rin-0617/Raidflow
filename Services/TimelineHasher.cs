using System.Security.Cryptography;
using System.Text;
using RaidFlow.Models;

namespace RaidFlow.Services;

public static class TimelineHasher
{
    public static string Compute(RaidFlowDocument plan)
    {
        var builder = new StringBuilder();
        builder.Append(plan.TimelineId).Append('|');
        builder.Append(plan.ContentName).Append('|');
        builder.Append(plan.Revision).Append('|');
        builder.Append(plan.ContentLevel).Append('|');

        foreach (var timelineEvent in plan.Events.OrderBy(timelineEvent => timelineEvent.TimeSeconds))
        {
            builder.Append(timelineEvent.Id)
                .Append(':')
                .Append(timelineEvent.TimeSeconds.ToString("0.0"))
                .Append(':')
                .Append(timelineEvent.Name)
                .Append(':')
                .Append(timelineEvent.Type)
                .Append('|');
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes)[..12];
    }
}
