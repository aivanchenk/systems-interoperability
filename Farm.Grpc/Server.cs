namespace Servers;

using System.Net;

using NLog;


/// <summary>
/// Application entry point.
/// </summary>
public class Server
{
	/// <summary>
	/// Logger for this class.
	/// </summary>
	Logger log = LogManager.GetCurrentClassLogger();

	/// <summary>
	/// Configure loggin subsystem.
	/// </summary>
	private void ConfigureLogging()
	{
		var config = new NLog.Config.LoggingConfiguration();

		var console =
			new NLog.Targets.ConsoleTarget("console")
			{
				Layout = @"${date:format=HH\:mm\:ss}|${level}| ${message} ${exception}"
			};
		config.AddTarget(console);
		config.AddRuleForAllLevels(console);

		LogManager.Configuration = config;
	}

	/// <summary>
	/// Program entry point.
	/// </summary>
	/// <param name="args">Command line arguments.</param>
	public static void Main(string[] args)
	{
		var self = new Server();
		self.Run(args);
	}

	/// <summary>
	/// Program body.
	/// </summary>
	/// <param name="args">Command line arguments.</param>
	private void Run(string[] args) 
	{
		//configure logging
		ConfigureLogging();

		//indicate server is about to start
		log.Info("Server is about to start");

		//start the server
		StartServer(args);
	}

	/// <summary>
	/// Starts integrated server.
	/// </summary>
	/// <param name="args">Command line arguments.</param>
	private void StartServer(string[] args)
	{
		//create web app builder
		var builder = WebApplication.CreateBuilder(args);

		//configure integrated server
		builder.WebHost.ConfigureKestrel(opts => {
			opts.Listen(IPAddress.Loopback, 5000);
		});

		//add support for GRPC services
		builder.Services.AddGrpc();

		//add the actual services
		builder.Services.AddSingleton(new FarmService());

		//build the server
		var app = builder.Build();

		//turn on request routing
		app.UseRouting();

		//configure routes
		app.MapGrpcService<FarmService>();
		
		//run the server
		app.Run();
		// app.RunAsync(); //use this if you need to implement background processing in the main thread
	}
}