using System.Threading.Tasks;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Fakes;

public class FakeGrokSupercomputer : IGrokSupercomputer
{
    public Task<string> AnalyzeMunicipalDataAsync(object data, string prompt)
    {
        // Return a deterministic short response for tests
        return Task.FromResult("FAKE_ANALYSIS: No issues detected in seeded data.");
    }
}
