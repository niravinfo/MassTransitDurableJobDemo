using MassTransit;
using MassTransit.Contracts.JobService;
using MassTransit.JobService;

namespace JobDistributionStrategyDemo.DistributionStrategies;

public class GroupKeyJobDistributionStrategy : IJobDistributionStrategy
{
    public Task<ActiveJob?> IsJobSlotAvailable(
        ConsumeContext<AllocateJobSlot> context,
        JobTypeInfo jobTypeInfo)
    {
        // Read configurable property name and max groups from job type properties
        var groupKeyProp = jobTypeInfo.Properties.TryGetValue(GroupKeyConstants.JobTypePropertyKey, out var p)
            ? p as string ?? GroupKeyConstants.PropertyName
            : GroupKeyConstants.PropertyName;

        var maxGroups = jobTypeInfo.Properties.TryGetValue(GroupKeyConstants.MaxConcurrentGroupsKey, out var m)
            ? Convert.ToInt32(m)
            : GroupKeyConstants.DefaultMaxConcurrentGroups;

        var incomingGroupKey = context.Message.JobProperties
            ?.GetValueOrDefault(groupKeyProp) as string;

        if (incomingGroupKey == null)
        {
            // Delegate instance selection to the default strategy
            return DefaultJobDistributionStrategy.Instance.IsJobSlotAvailable(context, jobTypeInfo);
        }

        // Block if the same group is already running (sequential within group)
        var sameGroupRunning = jobTypeInfo.ActiveJobs.Any(j =>
            j.Properties?.GetValueOrDefault(groupKeyProp) as string == incomingGroupKey);

        if (sameGroupRunning)
        {
            return Task.FromResult<ActiveJob?>(null);
        }

        // Block if max concurrent groups already reached
        var activeGroupCount = jobTypeInfo.ActiveJobs
            .Select(j => j.Properties?.GetValueOrDefault(groupKeyProp) as string)
            .Where(k => k != null)
            .Distinct()
            .Count();

        if (activeGroupCount >= maxGroups)
        {
            return Task.FromResult<ActiveJob?>(null);
        }

        // Delegate instance selection to the default strategy
        return DefaultJobDistributionStrategy.Instance.IsJobSlotAvailable(context, jobTypeInfo);
    }
}
