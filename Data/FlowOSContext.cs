using FlowOS.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Task = FlowOS.Api.Models.Task;

namespace FlowOS.Api.Data
{
    public class FlowOSContext : IdentityDbContext<ApplicationUser>
    {
        public FlowOSContext(DbContextOptions<FlowOSContext> options) : base(options)
        {
        }
        public DbSet<Task> Tasks { get; set; }
        public DbSet<DailyPlan> DailyPlans { get; set; }
    }
}
