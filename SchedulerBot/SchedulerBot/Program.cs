using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using SchedulerBot.Extensions;

namespace SchedulerBot
{
	public class Program
	{
		public static void Main(string[] args) =>
			BuildWebHost(args)
				.Build()
				.EnsureDatabaseMigrated()
				.Run();

		public static IWebHostBuilder BuildWebHost(string[] args) =>
			WebHost.CreateDefaultBuilder(args)
//				.ConfigureAppConfiguration((ctx, builder) => builder.AddAzureSecrets())
				.UseStartup<Startup>();
	}
}
