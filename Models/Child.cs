using System;
using System.Collections.Generic;

namespace QFamilyAPI.Models;

public partial class Child
{
    public int ChildId { get; set; }

    public int ParentId { get; set; }

    public string FullName { get; set; } = null!;

    public DateOnly? BirthDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? UserId { get; set; }

    public virtual ICollection<Device> Devices { get; set; } = new List<Device>();

    public virtual User Parent { get; set; } = null!;

    public virtual ICollection<UsageRule> UsageRules { get; set; } = new List<UsageRule>();

    public virtual User? User { get; set; }
}
