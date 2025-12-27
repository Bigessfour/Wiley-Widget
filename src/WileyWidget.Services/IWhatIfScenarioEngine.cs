using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services;
/// <summary>
/// Represents a interface for iwhatifscenarioengine.
/// </summary>

public interface IWhatIfScenarioEngine
{
    Task<ComprehensiveScenario> GenerateComprehensiveScenarioAsync(int enterpriseId, ScenarioParameters parameters);
}
