﻿using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SpaServices.AngularCli;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
		private readonly bool isDevelopment;

		/// <summary>
		/// Initializes a new instance of the <see cref="Startup"/> class.
		/// </summary>
		/// <param name="configuration">The configuration.</param>
		/// <param name="env"></param>
		public Startup(
			IConfiguration configuration,
			IHostingEnvironment env)
		{
			this.configuration = configuration;
			isDevelopment = env.IsDevelopment();
		}

		/// <summary>
		/// Configures the services injected at runtime.
		/// </summary>
		/// <param name="services">The services.</param>
		/// <returns>The service provider.</returns>
		public IServiceProvider ConfigureServices(IServiceCollection services)
		{
			services.AddDbContext();
//			services.AddAuthentication()
//				.AddBotAuthentication(configuration)
//				.AddManageConversationAuthentication(configuration);
//			services.AddMvc(options => options.Filters.Add<TrustServiceUrlAttribute>());
			services.AddSpaStaticFiles(options => options.RootPath = "wwwroot");

			services.AddBot<Bots.SchedulerBot>(options =>
			{
				string botFilePath = configuration.GetSection("BotCoreSettings::BotFilePath").Value;
				string secretKey = configuration.GetSection("BotCoreSettings::SecretKey")?.Value;

				// Loads .bot configuration file and adds a singleton that your Bot can access through dependency injection.
				BotConfiguration botConfig = BotConfiguration.Load(botFilePath, secretKey);

				// Retrieve current endpoint.
				string environment = isDevelopment ? "development" : "production";
				ConnectedService service = botConfig.Services.FirstOrDefault(s => s.Type == "endpoint" && s.Name == environment);
				if (!(service is EndpointService endpointService))
				{
					throw new InvalidOperationException($"The .bot file does not contain an endpoint with name '{environment}'.");
				}

				options.CredentialProvider = new SimpleCredentialProvider(endpointService.AppId, endpointService.AppPassword);

				// Creates a logger for the application to use.
				ILogger logger = _loggerFactory.CreateLogger<EchoWithCounterBot>();

				// Catches any errors that occur during a conversation turn and logs them.
				options.OnTurnError = async (context, exception) =>
				{
					logger.LogError($"Exception caught : {exception}");
					await context.SendActivityAsync("Sorry, it looks like something went wrong.");
				};
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
		/// <param name="env">The environment.</param>
		public void Configure(IApplicationBuilder app, IHostingEnvironment env)
		{
			app.UseDefaultFiles();
			app.UseStaticFiles();
			app.UseMiddleware<ApplicationContextMiddleware>();
			app.UseAuthentication();
			app.UseMvc();

			if (!isDevelopment)
			{
				app.UseStatusCodePages("text/plain", "Status code page, status code: {0}");
			}
			else
			{
				app.UseDeveloperExceptionPage();
			}

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
