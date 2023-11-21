using Azure;
using Azure.Data.Tables;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;

namespace ImageSharingWithCloud.Models
{
    public class LogEntry : ITableEntity
    {
        public DateTime EntryDate { get; set; }

        public string PartitionKey { get; set; }

        public string RowKey { get; set; }

        public string UserId { get; set; }

        public string Username { get; set; }

        public string Caption { get; set; }

        public string Uri { get; set; }

        public string ImageId { get; set; }
  
        public DateTimeOffset? Timestamp { get; set; }
 
        public ETag ETag { get; set; }

        public LogEntry() 
        {
            EntryDate = DateTime.UtcNow;

            PartitionKey = EntryDate.ToString("MMddyyyy");
        }

        public LogEntry(string userId, string imageId) : this()
        {
            UserId = userId;

            ImageId = imageId;

            RowKey = string.Format("{0}:{1}:{2}",
                                 imageId,
                                 DateTime.MaxValue.Ticks - EntryDate.Ticks,
                                 Guid.NewGuid());
        }
    }
}