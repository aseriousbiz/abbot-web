using System.Collections.Generic;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Pages.Settings.Rooms;

public record CustomerAssignContainer(IReadOnlyList<Customer> Customers, Room Room);
