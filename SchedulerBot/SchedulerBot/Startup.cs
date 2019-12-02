using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SpaServices.AngularCli;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SchedulerBot.DependencyInjection;
using SchedulerBot.Extensions;
using SchedulerBot.Infrastructure.Interfaces.Configuration;
using SchedulerBot.Middleware;

namespace SchedulerBot
{
	/// <summary>
	/// Describes how the application startup happens.
	/// </summary>
	public class Startup
	{
		private readonly IConfiguration configuration;
		private readonly IWebHostEnvironment env;

		/// <summary>
		/// Initializes a new instance of the <see cref="Startup"/> class.
		/// </summary>
		/// <param name="configuration">The configuration.</param>
		/// <param name="env">The environment.</param>
		public Startup(IConfiguration configuration, IWebHostEnvironment env)
		{
			this.configuration = configuration;
			this.env = env;
		}

		/// <summary>
		/// Configures the services injected at runtime.
		/// </summary>
		/// <param name="services">The services.</param>
		/// <returns>The service provider.</returns>
		public IServiceProvider ConfigureServices(IServiceCollection services)
		{
			var value = configuration.GetSection("BotCoreSettings").GetValue<string>("BotFilePath");

			services.AddDbContext();


			services.AddControllers();

			// source https://docs.microsoft.com/en-us/aspnet/core/mvc/compatibility-version?view=aspnetcore-2.2
			//services.AddMvc()
			//	// Include the 2.2 behaviors
			//	.SetCompatibilityVersion(CompatibilityVersion.Version_3_0)
			//	// Except for the following.
			//	.AddMvcOptions(options =>
			//	{
			//		// TODO:
			//		//// Don't combine authorize filters (keep 2.0 behavior).
			//		//options.AllowCombiningAuthorizeFilters = false;
			//		//// All exceptions thrown by an IInputFormatter are treated
			//		//// as model state errors (keep 2.0 behavior).
			//		//options.InputFormatterExceptionPolicy = InputFormatterExceptionPolicy.AllExceptions;
			//	});
			//			services.AddAuthentication()
			//				.AddBotAuthentication(configuration)
			//				.AddManageConversationAuthentication(configuration);
			//			services.AddMvc(options => options.Filters.Add<TrustServiceUrlAttribute>());
			services.AddSpaStaticFiles(options => options.RootPath = "wwwroot");

			// Loads .bot configuration file and adds a singleton that your Bot can access through dependency injection.
			string botFilePath = configuration.GetValue<string>("BotCoreSettings:BotFilePath");
			string secretKey = configuration.GetValue<string>("BotCoreSettings:SecretKey");

			BotConfiguration botConfig = BotConfiguration.Load(botFilePath, secretKey);

			// Retrieve current endpoint.
			string environment = env.IsDevelopment() ? "development" : "production";
			ConnectedService service = botConfig.Services.FirstOrDefault(s => s.Type == "endpoint" && s.Name == environment);
			if (!(service is EndpointService endpointService))
			{
				throw new InvalidOperationException($"The .bot file does not contain an endpoint with name '{environment}'.");
			}

			services.AddBot<Bots.SchedulerBot>(options =>
			{
				//options.CredentialProvider = new SimpleCredentialProvider(endpointService.AppId, endpointService.AppPassword);

				//TODO: Creates a logger for the application to use.
				//ILogger logger = _loggerFactory.CreateLogger<EchoWithCounterBot>();

				// Catches any errors that occur during a conversation turn and logs them.
				//options.OnTurnError = async (context, exception) =>
				//{
				//	logger.LogError($"Exception caught : {exception}");
				//	await context.SendActivityAsync("Sorry, it looks like something went wrong.");
				//};
			});

			ServiceProviderBuilder serviceProviderBuilder = new ServiceProviderBuilder();
			IServiceProvider serviceProvider = serviceProviderBuilder.Build(services);

			configuration.Bind(serviceProvider.GetRequiredService<IApplicationConfiguration>());

			return serviceProvider;
		}

		/// <summary>
		/// Configures the specified application.
		/// </summary>
		/// <param name="app">The application.</param>
		public void Configure(IApplicationBuilder app)
		{
			bool isDevelopment = env.IsDevelopment();
			if (isDevelopment)
			{
				app.UseDeveloperExceptionPage();
			}
			else
			{
				app.UseStatusCodePages("text/plain", "Status code page, status code: {0}");
				app.UseHsts();
			}

			app.UseDefaultFiles();
			app.UseStaticFiles();
			app.UseMiddleware<ApplicationContextMiddleware>();
			app.UseAuthentication();
			app.UseRouting();

			app.UseEndpoints(endpoints =>
			{
				endpoints.MapControllerRoute(
					name: "default",
					pattern: "{controller}/{action=Index}/{id?}");
			});

			app.UseSpa(builder =>
			{
				builder.Options.SourcePath = "ClientApp";

				if (isDevelopment)
				{
					builder.UseAngularCliServer(npmScript: "start");
				}
			});
		}
	}
}
