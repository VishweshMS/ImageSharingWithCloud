using Azure;
using ImageSharingWithCloud.Models;
using System.Threading.Tasks;

namespace ImageSharingWithCloud.DAL
{
    /**
    * Interface for logging image views in the application.
    */
    public interface ILogContext
    {
        public Task AddLogEntryAsync(string userId, string userName, ImageView imageView);

        public AsyncPageable<LogEntry> Logs(bool todayOnly);
    }
}
