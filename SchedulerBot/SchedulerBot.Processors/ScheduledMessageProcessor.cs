using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Rest.Serialization;
using Newtonsoft.Json;
using SchedulerBot.Database.Entities;
using SchedulerBot.Database.Entities.Enums;
using SchedulerBot.Database.Interfaces;
using SchedulerBot.Infrastructure.Interfaces.BotConnector;
using SchedulerBot.Infrastructure.Interfaces.Configuration;
using SchedulerBot.Infrastructure.Interfaces.Schedule;

namespace SchedulerBot.Processors
{
	/// <summary>
	/// A service supposed to send out the scheduled messages to their recipients.
	/// </summary>
	/// <seealso cref="IHostedService" />
	/// <seealso cref="IDisposable" />
	public sealed class ScheduledMessageProcessor : BackgroundService
	{
		#region Private Fields

		private readonly IServiceScopeFactory scopeFactory;
		private readonly ILogger<ScheduledMessageProcessor> logger;
		private readonly IMessageProcessor messageProcessor;
		private readonly IScheduleParser scheduleParser;
		private readonly TimeSpan pollingInterval;

		#endregion

		#region Constructor

		/// <summary>
		/// Initializes a new instance of the <see cref="ScheduledMessageProcessor"/> class.
		/// </summary>
		/// <param name="scheduleParser">The schedule parser.</param>
		/// <param name="scopeFactory">The scope factory.</param>
		/// <param name="configuration">The configuration.</param>
		/// <param name="logger">The logger.</param>
		/// <param name="messageProcessor">The message processor.</param>
		public ScheduledMessageProcessor(
			IScheduleParser scheduleParser,
			IServiceScopeFactory scopeFactory,
			IApplicationConfiguration configuration,
			ILogger<ScheduledMessageProcessor> logger,
			IMessageProcessor messageProcessor)
		{
			this.scheduleParser = scheduleParser;
			this.scopeFactory = scopeFactory;
			this.logger = logger;
			this.messageProcessor = messageProcessor;

			pollingInterval = configuration.MessageProcessingInterval;
		}

		#endregion

		#region IHostedService Implementation

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					await ProcessScheduledMessagesAsync(stoppingToken);
				}
				catch (Exception exception)
				{
					logger.LogCritical(exception, $"ProcessScheduledMessagesAsync method execution failed due Exception {exception.Message}. StackTrace: {exception.StackTrace}.");
				}
				finally
				{
					await WaitAsync(stoppingToken);
				}
			}
		}

		/// <inheritdoc />
		public override Task StartAsync(CancellationToken cancellationToken)
		{
			logger.LogInformation("Starting polling");

			return base.StartAsync(cancellationToken);
		}

		/// <inheritdoc />
		public override Task StopAsync(CancellationToken cancellationToken)
		{
			logger.LogInformation("Stopping polling");

			return base.StopAsync(cancellationToken);
		}

		#endregion

		#region Private Methods

		private async Task ProcessScheduledMessagesAsync(CancellationToken stoppingToken)
		{
			logger.LogInformation("Starting to process scheduled messages queue");

			using (IServiceScope scope = scopeFactory.CreateScope())
			{
				IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

				// TODO: there can be used parallel foreach
				IList<ScheduledMessageEvent> scheduledMessageEvents =
					await unitOfWork.ScheduledMessageEvents.GetAllPendingWithScheduledMessages(DateTime.UtcNow);
				foreach (ScheduledMessageEvent scheduledMessageEvent in scheduledMessageEvents)
				{
					try
					{
						ScheduledMessage scheduledMessage = scheduledMessageEvent.ScheduledMessage;
						string scheduledMessageId = scheduledMessage.Id.ToString();

						logger.LogInformation("Processing scheduled message '{0}'", scheduledMessageId);

						await messageProcessor.SendMessageAsync(scheduledMessage, stoppingToken);

						scheduledMessageEvent.State = ScheduledMessageEventState.Completed;
						AddPendingEvent(scheduledMessage);
						logger.LogInformation("Scheduled message '{0}' has been processed", scheduledMessageId);
					}
					// TODO: Added for debugging purposes, should be revisited.
					catch (ErrorResponseException exception)
					{
						string serializedBody = SafeJsonConvert.SerializeObject(exception.Body, new JsonSerializerSettings { Formatting = Formatting.Indented });
						string message = $"Sending message was failed due Exception Message: '{exception.Message}', StackTrace: '{exception.StackTrace}', Body: '{serializedBody}'";

						logger.LogError(message);
					}
					catch (Exception exception)
					{
						logger.LogError(exception, $"Sending message was failed due Exception {exception.Message}. StackTrace: {exception.StackTrace}.");
					}
				}

				await unitOfWork.SaveChangesAsync(stoppingToken);
			}

			logger.LogInformation("Finished processing scheduled messages queue");
		}

		private async Task WaitAsync(CancellationToken stoppingToken)
		{
			try
			{
				logger.LogInformation("Waiting for the next poll");

				await Task.Delay(pollingInterval, stoppingToken);
			}
			catch (TaskCanceledException)
			{
				// Just swallow it.
			}
		}

		private void AddPendingEvent(ScheduledMessage scheduledMessage)
		{
			ISchedule schedule = scheduleParser.Parse(scheduledMessage.Schedule, scheduledMessage.Details.TimeZoneOffset);

			scheduledMessage.Events.Add(new ScheduledMessageEvent
			{
				NextOccurrence = schedule.GetNextOccurrence(),
				State = ScheduledMessageEventState.Pending
			});
		}

		#endregion
	}
}
