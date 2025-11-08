using System.Threading.Tasks;

namespace WileyWidget.Services
{
    public interface IAuditService
    {
        Task AuditAsync(string eventName, object payload);
    }
}
