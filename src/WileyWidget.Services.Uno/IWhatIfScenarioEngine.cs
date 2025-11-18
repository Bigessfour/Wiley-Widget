using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services;

public interface IWhatIfScenarioEngine
{
    Task<ComprehensiveScenario> GenerateComprehensiveScenarioAsync(int enterpriseId, ScenarioParameters parameters);
}
