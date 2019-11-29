using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using SchedulerBot.Business.Entities;
using SchedulerBot.Business.Interfaces.Commands;
using SchedulerBot.Infrastructure.Interfaces.BotConnector;

namespace SchedulerBot.Controllers
{
	/// <summary>
	/// The API controller for handling user messages.
	/// </summary>
	/// <seealso cref="Controller" />
	[Route("api/[controller]")]
	public class MessagesController : Controller
	{
		private readonly ICommandRequestParser commandRequestParser;
		private readonly ICommandSelector commandSelector;
		private readonly IMessageProcessor messageProcessor;
		private readonly ILogger<MessagesController> logger;

		/// <summary>
		/// Initializes a new instance of the <see cref="MessagesController"/> class.
		/// </summary>
		/// <param name="commandRequestParser">The command request parser.</param>
		/// <param name="commandSelector">The command selector.</param>
		/// <param name="messageProcessor">The message processor.</param>
		/// <param name="logger">The logger.</param>
		public MessagesController(
			ICommandRequestParser commandRequestParser,
			ICommandSelector commandSelector,
			IMessageProcessor messageProcessor,
			ILogger<MessagesController> logger)
		{
			this.commandRequestParser = commandRequestParser;
			this.commandSelector = commandSelector;
			this.messageProcessor = messageProcessor;
			this.logger = logger;
		}

		/// <summary>
		/// Handles HTTP GET requests.
		/// </summary>
		/// <returns>The result of the method processing.</returns>
		[HttpGet]
		public IActionResult Get()
		{
			return Ok("MessagesController");
		}

		/// <summary>
		/// Handles HTTP POST requests - user messages to the bot in particular.
		/// </summary>
		/// <param name="activity">The activity.</param>
		/// <returns>The action result.</returns>
		[HttpPost]
		//[Authorize(Roles = "Bot", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
		public async Task<OkResult> Post([FromBody] Activity activity)
		{
			if (activity.Type == ActivityTypes.Message)
			{
				DecodeActivityText(activity);

				string replyText = null;
				ParsedCommandRequest parsedCommandRequest = commandRequestParser.Parse(activity);

				if (parsedCommandRequest != null)
				{
					IBotCommand command = commandSelector.GetCommand(parsedCommandRequest.Name);

					if (command != null)
					{
						ExecutionResult<string> executionResult =
							await command.ExecuteAsync(activity, parsedCommandRequest.Arguments);
						replyText = executionResult.IsSuccess
							? executionResult.Entity
							: executionResult.ErrorMessage;
					}
				}
				
				if (replyText == null)
				{
					replyText = "Sorry, I don't understand you :(";
				}

				logger.LogInformation("Replying with '{0}'", replyText);

				await messageProcessor.ReplyAsync(activity, replyText, CancellationToken.None);
			}

			return Ok();
		}

		private void DecodeActivityText(Activity activity)
		{
			logger.LogInformation("Received the message with the following text: '{0}'", activity.Text);
			activity.Text = WebUtility.HtmlDecode(activity.Text);
			logger.LogInformation("Decoded the text to '{0}'", activity.Text);
		}
	}
}
