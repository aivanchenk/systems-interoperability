using System.Text;
using Grpc.Net.Client;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Newtonsoft.Json;
using NLog;

namespace Servers;

/// <summary>
/// RabbitMQ to gRPC adapter for Farm service
/// </summary>
public class Adapter
{
    /// <summary>
    /// Name of the message exchange
    /// </summary>
    private static readonly string ExchangeName = "T120B180.Farm.Exchange";

    /// <summary>
    /// Name of the server queue
    /// </summary>
    private static readonly string ServerQueueName = "T120B180.Farm.FarmService";

    /// <summary>
    /// Logger for this class
    /// </summary>
    private Logger log = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// gRPC client to communicate with Farm.Grpc server
    /// </summary>
    private Services.Farm.FarmClient grpcClient;

    /// <summary>
    /// RabbitMQ connection
    /// </summary>
    private IConnection rmqConn;

    /// <summary>
    /// RabbitMQ channel
    /// </summary>
    private IModel rmqChann;

    /// <summary>
    /// Configure logging subsystem
    /// </summary>
    private void ConfigureLogging()
    {
        var config = new NLog.Config.LoggingConfiguration();

        var console = new NLog.Targets.ConsoleTarget("console")
        {
            Layout = @"${date:format=HH\:mm\:ss}|${level}| ${message} ${exception}"
        };
        config.AddTarget(console);
        config.AddRuleForAllLevels(console);

        LogManager.Configuration = config;
    }

    /// <summary>
    /// Initialize gRPC client
    /// </summary>
    private void InitializeGrpcClient()
    {
        log.Info("Connecting to Farm.Grpc server at http://localhost:5000");
        var channel = GrpcChannel.ForAddress("http://localhost:5000");
        grpcClient = new Services.Farm.FarmClient(channel);
        log.Info("gRPC client initialized");
    }

    /// <summary>
    /// Initialize RabbitMQ connection and start listening
    /// </summary>
    private void InitializeRabbitMQ()
    {
        log.Info("Connecting to RabbitMQ message broker");

        // Connect to RabbitMQ
        var rmqConnFact = new ConnectionFactory();
        rmqConn = rmqConnFact.CreateConnection();
        rmqChann = rmqConn.CreateModel();

        // Declare exchange and queue
        rmqChann.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Direct);
        rmqChann.QueueDeclare(
            queue: ServerQueueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );
        rmqChann.QueueBind(
            queue: ServerQueueName,
            exchange: ExchangeName,
            routingKey: ServerQueueName,
            arguments: null
        );

        log.Info($"RabbitMQ adapter listening on queue '{ServerQueueName}'");

        // Set up consumer
        var consumer = new EventingBasicConsumer(rmqChann);
        consumer.Received += async (sender, delivery) =>
        {
            try
            {
                await HandleMessage(delivery);
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error handling RabbitMQ message");
            }
        };

        // Start consuming
        rmqChann.BasicConsume(ServerQueueName, true, consumer);
    }

    /// <summary>
    /// Handle incoming RabbitMQ message
    /// </summary>
    private async Task HandleMessage(BasicDeliverEventArgs delivery)
    {
        // Deserialize request
        var messageBody = Encoding.UTF8.GetString(delivery.Body.ToArray());
        var request = JsonConvert.DeserializeObject<Services.RPCMessage>(messageBody);

        if (request == null)
        {
            log.Warn("Received null message, ignoring");
            return;
        }

        log.Info($"RabbitMQ → Adapter: Received '{request.Action}' request");

        Services.SubmissionResult result;

        try
        {
            // Route to appropriate gRPC method
            switch (request.Action)
            {
                case "SubmitFood":
                    result = await HandleSubmitFood(request.Data);
                    break;

                case "SubmitWater":
                    result = await HandleSubmitWater(request.Data);
                    break;

                default:
                    log.Warn($"Unknown action: {request.Action}");
                    result = new Services.SubmissionResult
                    {
                        IsAccepted = false,
                        FailReason = $"Unknown action: {request.Action}"
                    };
                    break;
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Error processing {request.Action}");
            result = new Services.SubmissionResult
            {
                IsAccepted = false,
                FailReason = $"Adapter error: {ex.Message}"
            };
        }

        // Send response back via RabbitMQ
        if (!string.IsNullOrEmpty(delivery.BasicProperties.ReplyTo))
        {
            SendResponse(delivery.BasicProperties, request.Action, result);
        }
    }

    /// <summary>
    /// Handle SubmitFood request
    /// </summary>
    private async Task<Services.SubmissionResult> HandleSubmitFood(string data)
    {
        var requestData = JsonConvert.DeserializeObject<dynamic>(data);
        double amount = (double)requestData.amount;

        log.Info($"Adapter → gRPC: Submitting {amount} food");

        var grpcRequest = new Services.SubmitRequest { Amount = amount };
        var grpcResponse = await grpcClient.SubmitFoodAsync(grpcRequest);

        var result = new Services.SubmissionResult
        {
            IsAccepted = grpcResponse.IsAccepted,
            FailReason = grpcResponse.FailReason
        };

        log.Info($"gRPC → Adapter: Food submission result: Accepted={result.IsAccepted}");

        return result;
    }

    /// <summary>
    /// Handle SubmitWater request
    /// </summary>
    private async Task<Services.SubmissionResult> HandleSubmitWater(string data)
    {
        var requestData = JsonConvert.DeserializeObject<dynamic>(data);
        double amount = (double)requestData.amount;

        log.Info($"Adapter → gRPC: Submitting {amount} water");

        var grpcRequest = new Services.SubmitRequest { Amount = amount };
        var grpcResponse = await grpcClient.SubmitWaterAsync(grpcRequest);

        var result = new Services.SubmissionResult
        {
            IsAccepted = grpcResponse.IsAccepted,
            FailReason = grpcResponse.FailReason
        };

        log.Info($"gRPC → Adapter: Water submission result: Accepted={result.IsAccepted}");

        return result;
    }

    /// <summary>
    /// Send response back via RabbitMQ
    /// </summary>
    private void SendResponse(IBasicProperties requestProps, string action, Services.SubmissionResult result)
    {
        var response = new Services.RPCMessage
        {
            Action = $"{action}Response",
            Data = JsonConvert.SerializeObject(result)
        };

        var responseProps = rmqChann.CreateBasicProperties();
        responseProps.CorrelationId = requestProps.CorrelationId;

        var responseBody = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response));

        rmqChann.BasicPublish(
            exchange: ExchangeName,
            routingKey: requestProps.ReplyTo,
            basicProperties: responseProps,
            body: responseBody
        );

        log.Info($"Adapter → RabbitMQ: Sent '{response.Action}' response");
    }

    /// <summary>
    /// Run the adapter
    /// </summary>
    private void Run()
    {
        ConfigureLogging();

        log.Info("Farm RabbitMQ Adapter starting...");

        InitializeGrpcClient();
        InitializeRabbitMQ();

        log.Info("Adapter is running. Press Ctrl+C to stop.");

        // Keep the application running
        while (true)
        {
            Thread.Sleep(1000);
        }
    }

    /// <summary>
    /// Program entry point
    /// </summary>
    static void Main(string[] args)
    {
        var adapter = new Adapter();
        adapter.Run();
    }
}