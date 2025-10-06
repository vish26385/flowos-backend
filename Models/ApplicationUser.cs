using Microsoft.AspNetCore.Identity;

namespace FlowOS.Api.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
    }
}
