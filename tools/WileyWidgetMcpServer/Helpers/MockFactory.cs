using Microsoft.Extensions.Logging;
using Moq;
using WileyWidget.WinForms.Forms;

namespace WileyWidget.McpServer.Helpers;

/// <summary>
/// Factory for creating mock objects needed for testing.
/// </summary>
public static class MockFactory
{
    /// <summary>
    /// Lightweight IServiceProvider used for tests. Returns a Mock.Of<T>() for requested interfaces.
    /// </summary>
    private class TestServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            try
            {
                if (serviceType == typeof(IServiceProvider)) return this;

                // Prefer Mock.Of<T>() to generate a lightweight mock instance
                var ofMethod = typeof(Moq.Mock).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Of" && m.IsGenericMethod && m.GetParameters().Length == 0);

                if (ofMethod != null)
                {
                    return ofMethod.MakeGenericMethod(serviceType).Invoke(null, null);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Creates a mock MainForm for isolated form testing using the parameterless constructor.
    /// The MainForm will have a TestServiceProvider injected so GetRequiredService returns mocks.
    /// </summary>
    public static MainForm CreateMockMainForm(bool enableMdi = false)
    {
        var mainForm = new MainForm();

        // Inject a simple test IServiceProvider (mock-backed) so calls to GetRequiredService don't throw
        try
        {
            var sp = new TestServiceProvider();
            var field = typeof(MainForm).GetField("_serviceProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(mainForm, sp);
        }
        catch
        {
            // If reflection fails, proceed without a ServiceProvider and rely on constructor-level mocks
        }

        return mainForm;
    }

    /// <summary>
    /// Returns a test IServiceProvider that will supply Mock.Of<T>() instances for requested services.
    /// </summary>
    public static IServiceProvider CreateTestServiceProvider() => new TestServiceProvider();
}
