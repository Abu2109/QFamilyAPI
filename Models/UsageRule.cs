using System;
using System.Collections.Generic;

namespace QFamilyAPI.Models;

public partial class UsageRule
{
    public int RuleId { get; set; }

    public int ChildId { get; set; }

    public int DailyLimitMinutes { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public bool IsEnabled { get; set; }

    public virtual Child Child { get; set; } = null!;
}
