using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Common;

namespace HealthMonitoringService
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);
        private CloudTable healthCheckTable;
        private CloudTable alertEmailsTable;
 // Update URLs to actual deployed endpoints or local if testing
        private readonly string movieDiscussionUrl = "http://localhost:5000/health-monitoring";
        private readonly string notificationUrl = "http://localhost:5001/health-monitoring";
       

        public override bool OnStart()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.DefaultConnectionLimit = 12;
            InitializeAzureTables();
            bool result = base.OnStart();
            Trace.TraceInformation("HealthMonitoringService has been started");
            return result;
        }

        private void InitializeAzureTables()
        {
            string connectionString = "UseDevelopmentStorage=true"; // Adjust for real storage
            var storageAccount = CloudStorageAccount.Parse(connectionString);
            var tableClient = storageAccount.CreateCloudTableClient();
            healthCheckTable = tableClient.GetTableReference("HealthCheck");
            alertEmailsTable = tableClient.GetTableReference("AlertEmails");
            healthCheckTable.CreateIfNotExistsAsync().Wait();
            alertEmailsTable.CreateIfNotExistsAsync().Wait();
        }

        public override void Run()
        {
            Trace.TraceInformation("HealthMonitoringService is running");
            try
            {
                RunAsync(cancellationTokenSource.Token).Wait();
            }
            finally
            {
                runCompleteEvent.Set();
            }
        }

        public override void OnStop()
        {
            Trace.TraceInformation("HealthMonitoringService is stopping");
            cancellationTokenSource.Cancel();
            runCompleteEvent.WaitOne();
            base.OnStop();
            Trace.TraceInformation("HealthMonitoringService has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            var services = new Dictionary<string, string>
            {
                { "MovieDiscussionService", movieDiscussionUrl },
                { "NotificationService", notificationUrl }
            };

            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var service in services)
                {
                    bool isOk = await CheckService(service.Value);
                    await LogHealthStatus(service.Key, isOk);

                    if (!isOk)
                        await SendAlertEmails(service.Key);
                }

                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }

        private async Task<bool> CheckService(string url)
        {
            try
            {
                using (var client = new WebClient())
                {
                    await client.DownloadStringTaskAsync(new Uri(url));
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task LogHealthStatus(string serviceName, bool isOk)
        {
            // Kreiramo entitet koristeći HealthCheckEntity klasu
            var entity = new HealthCheckEntity(serviceName, DateTime.UtcNow)
            {
                Status = isOk ? "OK" : "NOT_OK"
            };

            var insertOperation = TableOperation.Insert(entity);
            await healthCheckTable.ExecuteAsync(insertOperation);

            Trace.TraceInformation($"{serviceName}: {(isOk ? "OK" : "NOT_OK")}");
        }


        private async Task SendAlertEmails(string serviceName)
        {
            var query = new TableQuery<DynamicTableEntity>();
            var results = await alertEmailsTable.ExecuteQuerySegmentedAsync(query, null);
            var emails = results.Results.Select(e => e.Properties["Email"].StringValue).ToList();

            foreach (var email in emails)
            {
                try
                {
                    using (var smtp = new SmtpClient("smtp.gmail.com", 587))
                    {
                        smtp.Credentials = new NetworkCredential("your_email@gmail.com", "your_password");
                        smtp.EnableSsl = true;

                        var mail = new MailMessage("your_email@gmail.com", email)
                        {
                            Subject = $"[ALERT] Service {serviceName} is down",
                            Body = $"Service {serviceName} failed health-check."
                        };

                        await smtp.SendMailAsync(mail);
                        Trace.TraceInformation($"Alert sent to: {email}");
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"Error sending alert: {ex.Message}");
                }
            }
        }
    }
}
