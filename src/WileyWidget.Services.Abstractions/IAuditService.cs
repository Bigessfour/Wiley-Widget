using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions
{
    public interface IAuditService
    {
        Task AuditAsync(string eventName, object payload);
    }
}
