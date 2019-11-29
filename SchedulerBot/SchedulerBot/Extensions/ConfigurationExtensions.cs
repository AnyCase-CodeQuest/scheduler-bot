using System;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureKeyVault;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SchedulerBot.Authentication;
using SchedulerBot.Database.Core;
using SchedulerBot.Infrastructure.Interfaces.Configuration;

namespace SchedulerBot.Extensions
{
	/// <summary>
	/// Provides extension methods used during the application configuration.
	/// </summary>
	internal static class ConfigurationExtensions
	{
		#region Public Methods

		/// <summary>
		/// Adds the azure secrets.
		/// </summary>
		/// <param name="builder">The builder.</param>
		/// <returns>The same <see cref="ConfigurationBinder"/> instance which has been passed to the method.</returns>
		public static IConfigurationBuilder AddAzureSecrets(this IConfigurationBuilder builder)
		{
			string keyVaultEndpoint = GetKeyVaultEndpoint();

			if (!string.IsNullOrEmpty(keyVaultEndpoint))
			{
				AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();
				KeyVaultClient keyVaultClient = new KeyVaultClient(
					new KeyVaultClient.AuthenticationCallback(
						azureServiceTokenProvider.KeyVaultTokenCallback));
				builder.AddAzureKeyVault(
					keyVaultEndpoint, keyVaultClient, new DefaultKeyVaultSecretManager());
			}

			return builder;
		}

		/// <summary>
		/// Verifies whether the database is migrated and applies migrations if not.
		/// </summary>
		/// <param name="host">The host.</param>
		/// <returns>The same <see cref="IWebHost"/> instance which has been passed to the method.</returns>
		public static IWebHost EnsureDatabaseMigrated(this IWebHost host)
		{
			using (IServiceScope scope = host.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
			{
				IServiceProvider scopeServiceProvider = scope.ServiceProvider;
				SchedulerBotContext context = scopeServiceProvider.GetRequiredService<SchedulerBotContext>();

				context.Database.Migrate();
			}

			return host;
		}

//		/// <summary>
//		/// Configures the authentication for the bot.
//		/// </summary>
//		/// <param name="builder">The builder.</param>
//		/// <param name="configuration">The configuration.</param>
//		/// <returns>
//		/// The same authentication builder that is passed as an argument
//		/// so that it can be used in further configuration chain.
//		/// </returns>
//		public static AuthenticationBuilder AddBotAuthentication(
//			this AuthenticationBuilder builder,
//			IConfiguration configuration)
//		{
//			SimpleCredentialProvider credentialProvider = new SimpleCredentialProvider(
//				configuration["Secrets:MicrosoftAppCredentials:Id"],
//				configuration["Secrets:MicrosoftAppCredentials:Password"]);
//
//			return builder.AddBotAuthentication(credentialProvider);
//		}

		/// <summary>
		/// Configures the authentication scheme used for managing conversations.
		/// </summary>
		/// <param name="builder">The builder.</param>
		/// <param name="configuration">The configuration.</param>
		/// <returns>
		/// The same authentication builder that is passed as an argument
		/// so that it can be used in further configuration chain.
		/// </returns>
		public static AuthenticationBuilder AddManageConversationAuthentication(
			this AuthenticationBuilder builder,
			IConfiguration configuration)
		{
			return builder
				.AddJwtBearer(
					ManageConversationAuthenticationConfiguration.AuthenticationSchemeName,
					ManageConversationAuthenticationConfiguration.AuthenticationSchemeDisplayName,
					options => ConfigureJwtValidation(options, configuration));
		}

		/// <summary>
		/// Registers the database context.
		/// </summary>
		/// <param name="services">The services.</param>
		/// <returns>
		/// The same service collection that is passed as an argument
		/// so that it can be used in further configuration chain.
		/// </returns>
		public static IServiceCollection AddDbContext(this IServiceCollection services)
		{
			return services.AddDbContext<SchedulerBotContext>((provider, builder) =>
			{
				ISecretConfiguration secretConfiguration = provider.GetRequiredService<ISecretConfiguration>();
				string connectionString = secretConfiguration.ConnectionString;

				builder.UseSqlServer(connectionString);
			});
		}

		#endregion

		#region Private Methods

		private static string GetKeyVaultEndpoint() => Environment.GetEnvironmentVariable("KEYVAULT_ENDPOINT");
		
		private static void ConfigureJwtValidation(JwtBearerOptions options, IConfiguration configuration)
		{
			TokenValidationParameters validationParameters = options.TokenValidationParameters;

			validationParameters.ValidateIssuer = true;
			validationParameters.ValidateIssuerSigningKey = true;
			validationParameters.ValidateAudience = true;
			validationParameters.ValidateLifetime = true;
			validationParameters.RequireSignedTokens = true;
			validationParameters.RequireExpirationTime = true;
			validationParameters.IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(configuration["Secrets:Authentication:SigningKey"]));
			validationParameters.ValidAudience = configuration["Secrets:Authentication:Audience"];
			validationParameters.ValidIssuer = configuration["Secrets:Authentication:Issuer"];
		}

		#endregion
	}
}
