using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace WorkerService1
{
    public class Worker : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly WorkerOptions _options;
        private IConnection connection;
        private IModel channel;
        private bool _isShutdown = false;
        private static System.Timers.Timer timer = new System.Timers.Timer();
        private int _runSec = 10;

        public Worker(ILogger<Worker> logger, WorkerOptions options)
        {
            _logger = logger;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = "localhost",
                    UserName = "root",
                    Password = "1234"
                };

                this.connection = factory.CreateConnection();
                this.channel = connection.CreateModel();

                bool durable = true;
                channel.QueueDeclare("task_queue", durable, false, false, null);
                channel.BasicQos(0, 1, false);

                var consumer = new EventingBasicConsumer(channel);
                int queneCount = 0;
                consumer.Received += (model, ea) =>
                {
                    queneCount++;
                    if (this._isShutdown)
                    {
                        Thread.Sleep(600000);
                        return;
                    }
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    _logger.LogInformation("Received {0}({1})", message, queneCount);
                    Thread.Sleep(5000);

                    if (!this._isShutdown)
                        channel.BasicAck(ea.DeliveryTag, false);
                };

                consumer.Shutdown += (sender, args) =>
                {
                    if (args.ToString().Contains("Closed via management plugin"))
                    {
                        _logger.LogInformation("已收到Shutdown請求");
                        this._isShutdown = true;
                        this.TimerStart();
                    }
                };

                channel.BasicConsume(queue: "task_queue", autoAck: false, consumer: consumer);
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.Message);
            }
        }

        private void TimerStart()
        {
            timer.Interval = 1000;
            timer.Elapsed += TimerHandler;
            timer.Start();
        }

        private void TimerHandler(object sender, EventArgs e)
        {
            _logger.LogInformation($"剩餘{this._runSec}秒關閉連線");
            this._runSec--;
            if (_runSec <= 0)
            {
                timer.Stop();
                Environment.Exit(0);
            }                
        }
    }
}
