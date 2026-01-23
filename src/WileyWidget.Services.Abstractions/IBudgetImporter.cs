using System.Threading.Tasks;

namespace WileyWidget.Services.Abstractions
{
    public interface IBudgetImporter
    {
        Task ImportAsync(string sourcePath);
    }
}
