using FlowOS.Api.Models;
using Microsoft.EntityFrameworkCore;
using Task = FlowOS.Api.Models.Task;

namespace FlowOS.Api.Data
{
    public class FlowOSContext : DbContext
    {
        public FlowOSContext(DbContextOptions<FlowOSContext> options) : base(options)
        {
        }
        public DbSet<User> Users { get; set; }
        public DbSet<Task> Tasks { get; set; }
        public DbSet<DailyPlan> DailyPlans { get; set; }
    }
}
