using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using ImageSharingWithCloud.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ImageSharingWithCloud.DAL
{
    public class LogContext : ILogContext
    {
        protected TableClient tableClient;

        protected ILogger<LogContext> logger;

        public LogContext(IConfiguration configuration, ILogger<LogContext> logger)
        {
            this.logger = logger;

            Uri logTableServiceUri = null;
            string logTableName = null;
            /*
             * TODO Get the table service URI and table name.
             */
            string uri = configuration[StorageConfig.LogEntryDbUri];
            logTableServiceUri = new Uri(uri);

            logTableName = configuration[StorageConfig.LogEntryDbTable];
            logger.LogInformation("Looking up Storage URI... ");


            logger.LogInformation("Using Table Storage URI: " + logTableServiceUri);
            logger.LogInformation("Using Table: " + logTableName);
            
            // Access key will have been loaded from Secrets (Development) or Key Vault (Production)
            TableSharedKeyCredential credential = new TableSharedKeyCredential(
                configuration[StorageConfig.LogEntryDbAccountName],
                configuration[StorageConfig.LogEntryDbAccessKey]);

            logger.LogInformation("Initializing table client....");
            // TODO Set the table client for interacting with the table service (see TableClient constructors)
            tableClient = new TableClient(logTableServiceUri, logTableName, credential);


            logger.LogInformation("....table client URI = " + tableClient.Uri);
        }


        public async Task AddLogEntryAsync(string userId, string userName, ImageView image)
        {
            LogEntry entry = new LogEntry(userId, image.Id)
            {
                Username = userName,
                Caption = image.Caption,
                ImageId = image.Id,
                Uri = image.Uri
            };

            logger.LogDebug("Adding log entry for image: {0}", image.Id);

            Response response = null;
            // TODO add a log entry for this image view
            response = await tableClient.AddEntityAsync(entry);
            if (response.IsError)
            {
                logger.LogError("Failed to add log entry, HTTP response {0}", response.Status);
            } 
            else
            {
                logger.LogDebug("Added log entry with HTTP response {0}", response.Status);
            }

        }

        public AsyncPageable<LogEntry> Logs(bool todayOnly = false)
        {
            if (todayOnly)
            {
                var today = DateTime.UtcNow.Date;
                return tableClient.QueryAsync<LogEntry>(logEntry =>
                    logEntry.Timestamp.HasValue &&
                    logEntry.Timestamp.Value.UtcDateTime.Date == today);
            
            // TODO just return logs for today

        }
            else
            {
                return tableClient.QueryAsync<LogEntry>(logEntry => true);
            }
        }

    }
}