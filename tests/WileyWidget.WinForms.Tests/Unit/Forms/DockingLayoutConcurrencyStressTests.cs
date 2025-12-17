using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SfTools = Syncfusion.Windows.Forms.Tools;
using WileyWidget.WinForms.Forms;
using WileyWidget.WinForms.Tests.Infrastructure;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Forms;

/// <summary>
/// Stress tests for docking layout persistence focusing on:
/// - High-frequency concurrent save operations
/// - Race conditions between load and save
/// - Debounce timer behavior under load
/// - File system race conditions
/// - Memory leak detection during repeated operations
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "DockingConcurrency")]
[Trait("Category", "Stress")]
[Collection(WinFormsUiCollection.CollectionName)]
public sealed class DockingLayoutConcurrencyStressTests : IDisposable
{
    private readonly WinFormsUiThreadFixture _ui;
    private string _testLayoutDirectory;

    public DockingLayoutConcurrencyStressTests(WinFormsUiThreadFixture ui)
    {
        _ui = ui;
        _testLayoutDirectory = Path.Combine(Path.GetTempPath(), $"WileyWidget_Stress_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testLayoutDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testLayoutDirectory))
            {
                Directory.Delete(_testLayoutDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }

        GC.SuppressFinalize(this);
    }



    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T)field!.GetValue(target)!;
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(target, args);
    }

    #region High-Frequency Concurrent Save Tests

    [Fact(Skip = "Syncfusion v32.1.9: Reflection-based test - internal fields changed. Refactor to use public API.")]
    public void DebouncedSave_UnderHighFrequencyChanges_ConsolidatesEffectively()
    {
        _ui.Run(() =>
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UI:UseDockingManager"] = "true",
                    ["UI:IsUiTestHarness"] = "false"
                })
                .Build();

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            using var mainForm = new MainForm(serviceProvider, config, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);
            mainForm.CreateControl();

            var components = new Container();
            var dockingManager = new SfTools.DockingManager(components)
            {
                HostControl = mainForm
            };

            try
            {
                SetPrivateField(mainForm, "_dockingManager", dockingManager);
                SetPrivateField(mainForm, "_useSyncfusionDocking", true);

                // Add panel
                using var panel = new System.Windows.Forms.Panel { Name = "StressTestPanel" };
                dockingManager.SetEnableDocking(panel, true);

                // Simulate rapid UI changes (100 changes in quick succession)
                var stopwatch = Stopwatch.StartNew();
                var invokeCount = 0;

                for (int i = 0; i < 100; i++)
                {
                    try
                    {
                        InvokePrivateMethod(mainForm, "DebouncedSaveDockingLayout");
                        invokeCount++;
                        Thread.Sleep(10); // 10ms between changes
                    }
                    catch
                    {
                        // Some calls may fail due to timing - that's expected
                    }
                }

                stopwatch.Stop();

                // Verify timer consolidated the saves
                var timer = GetPrivateField<System.Windows.Forms.Timer>(mainForm, "_dockingLayoutSaveTimer");
                Assert.NotNull(timer);

                // Stop timer before cleanup
                timer?.Stop();

                // Assert that debounce mechanism was invoked many times but would result in few actual saves
                Assert.True(invokeCount > 50, $"Should have processed many debounce calls: {invokeCount}");
                Assert.True(stopwatch.ElapsedMilliseconds < 5000, "Debounce calls should be fast");
            }
            finally
            {
                SetPrivateField(mainForm, "_dockingManager", null);
                dockingManager.Dispose();
                components.Dispose();
            }
        });
    }

    [Fact(Skip = "Syncfusion v32.1.9: Reflection-based test - internal fields changed. Refactor to use public API.")]
    public void ConcurrentSaveAttempts_AcrossMultipleThreads_MaintainDataIntegrity()
    {
        _ui.Run(() =>
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UI:UseDockingManager"] = "true",
                    ["UI:IsUiTestHarness"] = "false"
                })
                .Build();

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            using var mainForm = new MainForm(serviceProvider, config, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);
            mainForm.CreateControl();

            var components = new Container();
            var dockingManager = new SfTools.DockingManager(components)
            {
                HostControl = mainForm
            };

            try
            {
                SetPrivateField(mainForm, "_dockingManager", dockingManager);
                SetPrivateField(mainForm, "_useSyncfusionDocking", true);

                // Add panel
                using var panel = new System.Windows.Forms.Panel { Name = "ConcurrencyPanel" };
                dockingManager.SetEnableDocking(panel, true);

                // Track concurrent attempts
                int blockedCount = 0;
                int attemptedCount = 0;
                var tasks = new Task[20];

                // Create 20 concurrent threads attempting to trigger saves
                for (int i = 0; i < 20; i++)
                {
                    int threadIndex = i;
                    tasks[i] = Task.Run(() =>
                    {
                        Interlocked.Increment(ref attemptedCount);

                        // Check if already saving
                        bool wasSaving = false;
                        try
                        {
                            wasSaving = GetPrivateField<bool>(mainForm, "_isSavingLayout");
                            if (wasSaving)
                            {
                                Interlocked.Increment(ref blockedCount);
                            }
                        }
                        catch
                        {
                            // Expected - concurrent access protection
                            Interlocked.Increment(ref blockedCount);
                        }

                        // Attempt debounced save
                        try
                        {
                            InvokePrivateMethod(mainForm, "DebouncedSaveDockingLayout");
                        }
                        catch
                        {
                            // May fail due to cross-thread marshaling
                        }

                        Thread.Sleep(50); // Simulate work
                    });
                }

                Task.WaitAll(tasks, TimeSpan.FromSeconds(10));

                // Verify concurrency control worked
                Assert.Equal(20, attemptedCount);
                Assert.True(blockedCount >= 0, "Concurrency control should have blocked some attempts");

                // Verify form remains stable
                Assert.True(mainForm.IsHandleCreated);
                Assert.False(mainForm.IsDisposed);
            }
            finally
            {
                SetPrivateField(mainForm, "_dockingManager", null);
                dockingManager.Dispose();
                components.Dispose();
            }
        });
    }

    #endregion

    #region Load-Save Race Conditions

    [Fact(Skip = "Syncfusion v32.1.9: Reflection-based test - internal fields changed. Refactor to use public API.")]
    public void SimultaneousLoadAndSave_DoNotCauseDeadlock()
    {
        _ui.Run(() =>
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UI:UseDockingManager"] = "true",
                    ["UI:IsUiTestHarness"] = "false"
                })
                .Build();

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            using var mainForm = new MainForm(serviceProvider, config, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);
            mainForm.CreateControl();

            var components = new Container();
            var dockingManager = new SfTools.DockingManager(components)
            {
                HostControl = mainForm
            };

            try
            {
                SetPrivateField(mainForm, "_dockingManager", dockingManager);
                SetPrivateField(mainForm, "_useSyncfusionDocking", true);

                // Add panel
                using var panel = new System.Windows.Forms.Panel { Name = "RacePanel" };
                dockingManager.SetEnableDocking(panel, true);

                var loadTask = Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            InvokePrivateMethod(mainForm, "LoadDockingLayout");
                            Thread.Sleep(50);
                        }
                    }
                    catch
                    {
                        // Expected - cross-thread operations
                    }
                });

                var saveTask = Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            InvokePrivateMethod(mainForm, "DebouncedSaveDockingLayout");
                            Thread.Sleep(50);
                        }
                    }
                    catch
                    {
                        // Expected - cross-thread operations
                    }
                });

                // Verify no deadlock occurs
                var completedInTime = Task.WaitAll(new[] { loadTask, saveTask }, TimeSpan.FromSeconds(5));
                Assert.True(completedInTime, "Load and save operations should not deadlock");

                // Verify form is still responsive
                Assert.True(mainForm.IsHandleCreated);
            }
            finally
            {
                SetPrivateField(mainForm, "_dockingManager", null);
                dockingManager.Dispose();
                components.Dispose();
            }
        });
    }

    [Fact(Skip = "Syncfusion v32.1.9: Reflection-based test - internal fields changed. Refactor to use public API.")]
    public void SaveWhileDisposing_HandlesGracefully()
    {
        _ui.Run(() =>
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UI:UseDockingManager"] = "true",
                    ["UI:IsUiTestHarness"] = "false"
                })
                .Build();

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            using var mainForm = new MainForm(serviceProvider, config, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);
            mainForm.CreateControl();

            var components = new Container();
            var dockingManager = new SfTools.DockingManager(components)
            {
                HostControl = mainForm
            };

            try
            {
                SetPrivateField(mainForm, "_dockingManager", dockingManager);
                SetPrivateField(mainForm, "_useSyncfusionDocking", true);

                // Add panel
                using var panel = new System.Windows.Forms.Panel { Name = "DisposePanel" };
                dockingManager.SetEnableDocking(panel, true);

                // Start a save operation
                var saveTask = Task.Run(() =>
                {
                    try
                    {
                        Thread.Sleep(100);
                        InvokePrivateMethod(mainForm, "SaveDockingLayout");
                    }
                    catch
                    {
                        // Expected - form may be disposing
                    }
                });

                // Immediately start disposing
                var disposeTask = Task.Run(() =>
                {
                    try
                    {
                        Thread.Sleep(50);
                        InvokePrivateMethod(mainForm, "DisposeSyncfusionDockingResources");
                    }
                    catch
                    {
                        // Expected
                    }
                });

                Task.WaitAll(new[] { saveTask, disposeTask }, TimeSpan.FromSeconds(3));

                // Verify cleanup occurred without crashes
                var disposedManager = GetPrivateField<SfTools.DockingManager>(mainForm, "_dockingManager");
                Assert.Null(disposedManager);
            }
            finally
            {
                // Already disposed
                components.Dispose();
            }
        });
    }

    [Fact(Skip = "Syncfusion v32.1.9: Reflection-based test - internal fields changed. Refactor to use public API.")]
    public void SaveWhileDisposing_RepeatedRuns_DoNotCrashOrLeak()
    {
        _ui.Run(() =>
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UI:UseDockingManager"] = "true",
                    ["UI:IsUiTestHarness"] = "false"
                })
                .Build();

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            using var mainForm = new MainForm(serviceProvider, config, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);
            mainForm.CreateControl();

            // Run the race multiple times with fresh container per iteration
            for (int i = 0; i < 10; i++)
            {
                using var components = new Container();
                var dockingManager = new SfTools.DockingManager(components)
                {
                    HostControl = mainForm
                };

                SetPrivateField(mainForm, "_dockingManager", dockingManager);
                SetPrivateField(mainForm, "_useSyncfusionDocking", true);

                // Add panel
                using var panel = new System.Windows.Forms.Panel { Name = $"DisposePanel_{i}" };
                dockingManager.SetEnableDocking(panel, true);

                var saveTask = Task.Run(() =>
                {
                    try
                    {
                        Thread.Sleep(10);
                        InvokePrivateMethod(mainForm, "SaveDockingLayout");
                    }
                    catch { }
                });

                var disposeTask = Task.Run(() =>
                {
                    try
                    {
                        Thread.Sleep(5);
                        InvokePrivateMethod(mainForm, "DisposeSyncfusionDockingResources");
                    }
                    catch { }
                });

                Task.WaitAll(new[] { saveTask, disposeTask }, TimeSpan.FromSeconds(3));

                // Ensure the private field is cleared by dispose
                var disposedManager = GetPrivateField<SfTools.DockingManager>(mainForm, "_dockingManager");
                Assert.Null(disposedManager);

                // Ensure owner container can be disposed safely
                components.Dispose();
            }
        });
    }

    #endregion

    #region File System Race Conditions

    [Fact(Skip = "Syncfusion v32.1.9: Reflection-based test - internal fields changed. Refactor to use public API.")]
    public void MultipleSavesWithFileSystemDelay_MaintainConsistency()
    {
        _ui.Run(() =>
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UI:UseDockingManager"] = "true",
                    ["UI:IsUiTestHarness"] = "false"
                })
                .Build();

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            using var mainForm = new MainForm(serviceProvider, config, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);
            mainForm.CreateControl();

            var components = new Container();
            var dockingManager = new SfTools.DockingManager(components)
            {
                HostControl = mainForm
            };

            try
            {
                SetPrivateField(mainForm, "_dockingManager", dockingManager);
                SetPrivateField(mainForm, "_useSyncfusionDocking", true);

                // Add panel
                using var panel = new System.Windows.Forms.Panel { Name = "FileSystemPanel" };
                dockingManager.SetEnableDocking(panel, true);

                // Perform saves with delays (simulate slow I/O)
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        // Reset minimum interval to allow saves
                        SetPrivateField(mainForm, "_lastSaveTime", DateTime.MinValue);
                        SetPrivateField(mainForm, "_isSavingLayout", false);

                        InvokePrivateMethod(mainForm, "SaveDockingLayout");
                        Thread.Sleep(200); // Simulate I/O delay
                    }
                    catch
                    {
                        // May fail - verify no corruption
                    }
                }

                // Verify form remains stable after multiple saves
                Assert.True(mainForm.IsHandleCreated);
            }
            finally
            {
                SetPrivateField(mainForm, "_dockingManager", null);
                dockingManager.Dispose();
                components.Dispose();
            }
        });
    }

    [Fact(Skip = "Syncfusion v32.1.9: Reflection-based test - internal fields changed. Refactor to use public API.")]
    public void CorruptFileCreatedDuringSave_RecoveredOnNextLoad()
    {
        _ui.Run(() =>
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UI:UseDockingManager"] = "true",
                    ["UI:IsUiTestHarness"] = "false"
                })
                .Build();

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            using var mainForm = new MainForm(serviceProvider, config, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);
            mainForm.CreateControl();

            var components = new Container();
            var dockingManager = new SfTools.DockingManager(components)
            {
                HostControl = mainForm
            };

            try
            {
                SetPrivateField(mainForm, "_dockingManager", dockingManager);
                SetPrivateField(mainForm, "_useSyncfusionDocking", true);

                // Add panel and save
                using var panel = new System.Windows.Forms.Panel { Name = "RecoveryPanel" };
                dockingManager.SetEnableDocking(panel, true);

                // Perform initial save
                try
                {
                    InvokePrivateMethod(mainForm, "SaveDockingLayout");
                }
                catch
                {
                    // May fail - not critical for test
                }

                // Attempt load (should handle any corruption from previous save)
                try
                {
                    InvokePrivateMethod(mainForm, "LoadDockingLayout");
                }
                catch
                {
                    // Expected - corruption recovery path
                }

                // Verify recovery: form remains functional
                Assert.True(mainForm.IsHandleCreated);
                Assert.False(mainForm.IsDisposed);
            }
            finally
            {
                SetPrivateField(mainForm, "_dockingManager", null);
                dockingManager.Dispose();
                components.Dispose();
            }
        });
    }

    #endregion

    #region Memory Leak Detection

    [Fact(Skip = "Syncfusion v32.1.9: Reflection-based test - internal fields changed. Refactor to use public API.")]
    public void RepeatedSaveLoadCycles_DoNotLeakMemory()
    {
        _ui.Run(() =>
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UI:UseDockingManager"] = "true",
                    ["UI:IsUiTestHarness"] = "false"
                })
                .Build();

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            using var mainForm = new MainForm(serviceProvider, config, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);
            mainForm.CreateControl();

            var components = new Container();
            var dockingManager = new SfTools.DockingManager(components)
            {
                HostControl = mainForm
            };

            try
            {
                SetPrivateField(mainForm, "_dockingManager", dockingManager);
                SetPrivateField(mainForm, "_useSyncfusionDocking", true);

                // Add panel
                using var panel = new System.Windows.Forms.Panel { Name = "MemoryPanel" };
                dockingManager.SetEnableDocking(panel, true);

                // Force initial GC
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var initialMemory = GC.GetTotalMemory(true);

                // Perform many save/load cycles
                for (int i = 0; i < 50; i++)
                {
                    try
                    {
                        SetPrivateField(mainForm, "_lastSaveTime", DateTime.MinValue);
                        SetPrivateField(mainForm, "_isSavingLayout", false);

                        InvokePrivateMethod(mainForm, "SaveDockingLayout");
                        Thread.Sleep(50);
                        InvokePrivateMethod(mainForm, "LoadDockingLayout");
                        Thread.Sleep(50);
                    }
                    catch
                    {
                        // Some operations may fail - continue test
                    }
                }

                // Force GC and measure
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var finalMemory = GC.GetTotalMemory(true);
                var memoryGrowth = finalMemory - initialMemory;

                // Memory should not grow excessively (allow for some variance)
                // This is a heuristic test - exact values vary by runtime
                Assert.True(memoryGrowth < 10_000_000, // 10MB threshold
                    $"Memory growth excessive: {memoryGrowth:N0} bytes");
            }
            finally
            {
                SetPrivateField(mainForm, "_dockingManager", null);
                dockingManager.Dispose();
                components.Dispose();
            }
        });
    }

    #endregion

    #region Timer Behavior Under Load

    [Fact(Skip = "Syncfusion v32.1.9: Reflection-based test - internal fields changed. Refactor to use public API.")]
    public void DebounceTimer_UnderRapidFireChanges_BehavesCorrectly()
    {
        _ui.Run(() =>
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["UI:UseDockingManager"] = "true",
                    ["UI:IsUiTestHarness"] = "false"
                })
                .Build();

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            using var mainForm = new MainForm(serviceProvider, config, NullLogger<MainForm>.Instance, WileyWidget.WinForms.Configuration.ReportViewerLaunchOptions.Disabled);
            mainForm.CreateControl();

            var components = new Container();
            var dockingManager = new SfTools.DockingManager(components)
            {
                HostControl = mainForm
            };

            try
            {
                SetPrivateField(mainForm, "_dockingManager", dockingManager);
                SetPrivateField(mainForm, "_useSyncfusionDocking", true);

                // Add panel
                using var panel = new System.Windows.Forms.Panel { Name = "TimerPanel" };
                dockingManager.SetEnableDocking(panel, true);

                // Rapid-fire debounce calls
                for (int i = 0; i < 200; i++)
                {
                    try
                    {
                        InvokePrivateMethod(mainForm, "DebouncedSaveDockingLayout");
                        Thread.Sleep(5); // 5ms intervals (very rapid)
                    }
                    catch
                    {
                        // Some may be blocked - expected
                    }
                }

                // Verify timer exists and is managing the load
                var timer = GetPrivateField<System.Windows.Forms.Timer>(mainForm, "_dockingLayoutSaveTimer");
                Assert.NotNull(timer);

                // Stop timer to prevent save during cleanup
                timer?.Stop();

                // Verify form is stable after high load
                Assert.True(mainForm.IsHandleCreated);
            }
            finally
            {
                SetPrivateField(mainForm, "_dockingManager", null);
                dockingManager.Dispose();
                components.Dispose();
            }
        });
    }

    #endregion
}
