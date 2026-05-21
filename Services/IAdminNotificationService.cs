using System.Threading.Tasks;
using MLM_Level.Models;

namespace MLM_Level.Services
{
    public interface IAdminNotificationService
    {
        Task<AdminNotificationsViewModel> GetNotificationsAsync();
    }
}
