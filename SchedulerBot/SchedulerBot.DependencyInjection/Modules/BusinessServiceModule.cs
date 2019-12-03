using Autofac;
using Microsoft.Extensions.Hosting;
using SchedulerBot.Processors;

namespace SchedulerBot.DependencyInjection.Modules
{
	/// <summary>
	/// Registers the business services.
	/// </summary>
	/// <seealso cref="Module" />
	internal class BusinessServiceModule : Module
	{
		/// <inheritdoc />
		protected override void Load(ContainerBuilder builder)
		{
			base.Load(builder);

//			builder
//				.RegisterType<ScheduledMessageProcessor>()
//				.As<IHostedService>()
//				.SingleInstance();
		}
	}
}
