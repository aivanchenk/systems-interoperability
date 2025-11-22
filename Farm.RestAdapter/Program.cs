namespace Servers;

using System.Net;
using System.Reflection;
using System.Text.Json.Serialization;
using Grpc.Net.Client;
using NLog;

public class Server
{
    /// <summary>
    /// Logger for this class.
    /// </summary>
    Logger log = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Configure logging subsystem.
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
        log.Info("Farm REST Adapter is about to start");

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

        //configure integrated server (port 5100 for adapter)
        builder.WebHost.ConfigureKestrel(opts =>
        {
            opts.Listen(IPAddress.Loopback, 5100);
        });

        //add and configure swagger documentation generator (http://127.0.0.1:5100/swagger/)
        builder.Services.AddSwaggerGen(opts =>
        {
            opts.SwaggerDoc("v1", new()
            {
                Title = "Farm REST Adapter API",
                Version = "v1",
                Description = "REST API adapter for gRPC Farm service"
            });

            //include code comments in swagger documentation
            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
            if (File.Exists(xmlPath))
            {
                opts.IncludeXmlComments(xmlPath);
            }
        });

        //turn on support for web api controllers
        builder.Services
            .AddControllers()
            .AddJsonOptions(opts =>
            {
                //this makes enumeration values to be strings instead of integers in openapi doc
                opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        //add CORS policies
        builder.Services.AddCors(cr =>
        {
            //allow everything from everywhere
            cr.AddPolicy("allowAll", cp =>
            {
                cp.AllowAnyOrigin();
                cp.AllowAnyMethod();
                cp.AllowAnyHeader();
            });
        });

        //configure gRPC client as singleton to communicate with Farm.Grpc server
        builder.Services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Server>>();

            // Create gRPC channel to connect to Farm.Grpc server on port 5000
            var channel = GrpcChannel.ForAddress("http://localhost:5000");
            logger.LogInformation("gRPC channel created to http://localhost:5000");

            return new Services.Farm.FarmClient(channel);
        });

        //build the server
        var app = builder.Build();

        //turn CORS policy on
        app.UseCors("allowAll");

        //turn on support for swagger doc web page
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Farm REST Adapter API v1");
        });

        //turn on request routing
        app.UseRouting();

        //configure routes - use MapControllers for API controllers
        app.MapControllers();

        log.Info("Farm REST Adapter started successfully on http://localhost:5100");
        log.Info("Swagger UI available at http://localhost:5100/swagger");

        //run the server
        app.Run();
    }
}