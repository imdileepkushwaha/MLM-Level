using System.Threading.Tasks;
using MLM_Level.Models;

namespace MLM_Level.Services
{
    public interface IMemberNotificationService
    {
        Task<MemberNotificationsViewModel> GetNotificationsAsync(int userId);
    }
}
