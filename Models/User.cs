
using System;
using System.Collections.Generic;

namespace QFamilyAPI.Models;

public partial class User
{
    public int UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string? Email { get; set; }

    public string PasswordHash { get; set; } = null!;

    public string Role { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public string? Username { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<Child> ChildParents { get; set; }
        = new List<Child>();

    public virtual ICollection<Child> ChildUsers { get; set; }
        = new List<Child>();
}
