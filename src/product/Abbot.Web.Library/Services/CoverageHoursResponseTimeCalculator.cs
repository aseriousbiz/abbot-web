using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Services;

/// <summary>
/// This class has one purpose, calculate the response time for a conversation during working hours.
/// </summary>
public class CoverageHoursResponseTimeCalculator
{
    readonly IUserRepository _userRepository;

    public CoverageHoursResponseTimeCalculator(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<TimeSpan> CalculateResponseTimeAsync(Conversation conversation, DateTime responseDateUtc)
    {
        var responders = await GetWorkersAsync(conversation.Room);

        return responders.CalculateResponseTimeWorkingHours(
            WorkingHours.Default,
            conversation.LastStateChangeOn,
            responseDateUtc);
    }

    // Retrieve the workers that are covering the room to determine business hours.
    async Task<IReadOnlyList<IWorker>> GetWorkersAsync(Room room)
    {
        IReadOnlyList<IWorker> firstResponders = room.GetFirstResponders().ToList();
        if (!firstResponders.Any())
        {
            firstResponders = await _userRepository.GetDefaultFirstRespondersAsync(room.Organization);
            if (!firstResponders.Any())
            {
                firstResponders = new[]
                {
                    // There are no first responders, let's manufacture one.
                    new Member { TimeZoneId = "America/Los_Angeles", WorkingHours = WorkingHours.Default }
                };
            }
        }

        return firstResponders.ToList();
    }
}
