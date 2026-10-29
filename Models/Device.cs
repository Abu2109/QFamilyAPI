using System;
using System.Collections.Generic;

namespace QFamilyAPI.Models;

public partial class Device
{
    public int DeviceId { get; set; }

    public int ChildId { get; set; }

    public string DeviceName { get; set; } = null!;

    public string DeviceCode { get; set; } = null!;

    public string Platform { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual Child Child { get; set; } = null!;
}
