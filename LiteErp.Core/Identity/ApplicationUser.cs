using Microsoft.AspNetCore.Identity;

namespace LiteErp.Core.Identity;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public int? CompanyId { get; set; }   // default company scope for the user (optional)
}
