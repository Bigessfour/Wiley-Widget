# Docking Layout Persistence Test Coverage

## Overview

Comprehensive test suite covering edge cases for the Syncfusion DockingManager layout persistence implementation in MainForm.Docking.cs.

**Test Files:**

- `tests/WileyWidget.WinForms.Tests/Unit/Forms/DockingLayoutPersistenceEdgeCaseTests.cs`
- `tests/WileyWidget.WinForms.Tests/Unit/Forms/DockingLayoutConcurrencyStressTests.cs`

**Total Tests:** 21 (all passing)
**Test Categories:** `DockingPersistence`, `DockingConcurrency`, `Stress`

## Test Coverage Summary

### 1. Concurrent Save Protection (5 tests)

Tests verifying the debounce timer and locking mechanism prevent I/O spam and concurrent writes:

✅ **SaveDockingLayout_WhenConcurrentSavesAttempted_OnlyOneSaveSucceeds**

- Validates `_isSavingLayout` flag prevents concurrent saves
- Tests 10 parallel threads attempting concurrent saves
- Verifies locking mechanism maintains data integrity

✅ **DebouncedSave_WhenMultipleRapidChanges_ConsolidatesToSingleSave**

- Confirms debounce timer consolidates rapid UI changes (5 changes in 500ms)
- Verifies timer starts after changes and delays actual save
- Prevents I/O spam during rapid docking operations (drag/resize)

✅ **SaveDockingLayout_EnforcesMinimumSaveInterval**

- Tests 2-second minimum interval between saves (`MinimumSaveInterval`)
- Verifies timer doesn't start if within minimum window
- Prevents excessive I/O even with valid debounce triggers

✅ **DebouncedSave_UnderHighFrequencyChanges_ConsolidatesEffectively** (Stress)

- Simulates 100 rapid UI changes (10ms intervals)
- Verifies debounce mechanism processes many invocations without slowdown
- Tests elapsed time remains under 5 seconds

✅ **ConcurrentSaveAttempts_AcrossMultipleThreads_MaintainDataIntegrity** (Stress)

- 20 concurrent threads attempting saves
- Tracks blocked attempts due to concurrency control
- Verifies form stability after concurrent operations

### 2. File Corruption Recovery (4 tests)

Tests handling of corrupted, empty, or malformed layout files:

✅ **LoadDockingLayout_WithCorruptXml_DeletesFileAndUsesDefaults**

- Creates invalid XML: `<Invalid><XML>`
- Verifies corrupt file is detected and deleted
- Confirms default layout loads after corruption

✅ **LoadDockingLayout_WithEmptyFile_DeletesFileAndUsesDefaults**

- Creates zero-byte layout file
- Verifies empty file check and deletion
- Tests graceful fallback to defaults

✅ **SaveDockingLayout_WithTempFileStrategy_HandlesPartialWrites**

- Validates atomic write-replace pattern (`.tmp` → final)
- Confirms temporary file cleanup after successful save
- Prevents partial writes from corrupting layout

✅ **CorruptFileCreatedDuringSave_RecoveredOnNextLoad** (Stress)

- Simulates save operation creating corrupt data
- Tests recovery on subsequent load attempt
- Verifies form remains functional after corruption cycle

### 3. Disk I/O Failure Handling (3 tests)

Tests resilience to file system errors and permission issues:

✅ **SaveDockingLayout_WithReadOnlyDirectory_FallsBackToTempDirectory**

- Tests fallback to `Path.GetTempPath()` when AppData is read-only
- Verifies graceful handling of `UnauthorizedAccessException`
- Confirms form remains functional without write access

✅ **LoadDockingLayout_WithNullReferenceInSyncfusion_RecoversGracefully**

- Simulates internal Syncfusion NullReferenceException during load
- Tests exception catching and recovery
- Verifies no crash, form remains stable

✅ **MultipleSavesWithFileSystemDelay_MaintainConsistency** (Stress)

- Performs 5 sequential saves with 200ms I/O delays
- Tests file system race condition handling
- Verifies no corruption from slow I/O operations

### 4. Thread Safety (3 tests)

Tests cross-thread marshaling and UI thread requirements:

✅ **SaveDockingLayout_FromBackgroundThread_MarshalToUIThread**

- Attempts save from background thread
- Verifies `InvokeRequired` check and marshaling
- Tests cross-thread exception handling

✅ **LoadDockingLayout_BeforeHandleCreated_SkipsGracefully**

- Attempts load before window handle creation
- Verifies `IsHandleCreated` check skips operation
- Tests graceful handling of premature operations

✅ **SimultaneousLoadAndSave_DoNotCauseDeadlock** (Stress)

- 10 concurrent load operations + 10 concurrent save operations
- Verifies no deadlock between load/save locks
- Tests 5-second timeout for responsiveness

### 5. Cleanup and Disposal (2 tests)

Tests resource cleanup and final save during disposal:

✅ **ResetDockingLayout_DeletesLayoutFilesAndReloadsDefaults**

- Tests `ResetDockingLayout` method
- Verifies layout file deletion
- Confirms `_lastSaveTime` reset to `DateTime.MinValue`

✅ **DisposeSyncfusionDocking_PerformsFinalSaveAndCleanup**

- Tests `DisposeSyncfusionDockingResources` method
- Verifies final save attempt during disposal
- Confirms `_dockingManager` and `_dockingLayoutSaveTimer` are nulled

✅ **SaveWhileDisposing_HandlesGracefully** (Stress)

- Concurrent save and dispose operations
- Tests race between save-in-progress and disposal
- Verifies no crashes during disposal race condition

### 6. Exception Handling (1 test)

Tests specific Syncfusion exception scenarios:

✅ **SaveDockingLayout_OnArgumentException_DeletesCorruptLayout**

- Simulates `ArgumentException` from Syncfusion.SaveDockState
- Verifies layout deletion to prevent recurring corruption
- Tests form stability after internal exception

### 7. Memory Leak Detection (2 tests)

Stress tests for memory leaks during repeated operations:

✅ **RepeatedSaveLoadCycles_DoNotLeakMemory** (Stress)

- Performs 50 save/load cycles with GC monitoring
- Measures memory growth before/after cycles
- Asserts memory growth < 10MB threshold

✅ **DebounceTimer_UnderRapidFireChanges_BehavesCorrectly** (Stress)

- 200 rapid debounce calls (5ms intervals)
- Tests timer management under extreme load
- Verifies form stability after high-frequency operations

## Implementation Details Validated

### Debounce Timer Configuration

- **Interval:** 1500ms (increased from 500ms for better consolidation)
- **Minimum Save Interval:** 2000ms between actual saves
- **Lock Object:** `_dockingSaveLock` for thread safety
- **Flag:** `_isSavingLayout` prevents concurrent I/O

### File Handling Strategy

1. **Atomic Writes:** Save to `.tmp` file, then replace final file
2. **Corruption Detection:** Empty file check, XML validation, NullReferenceException catch
3. **Cleanup:** Automatic `.tmp` file removal after success or failure
4. **Fallback:** Use temp directory if AppData unavailable

### Thread Safety Mechanisms

1. **UI Thread Marshaling:** `InvokeRequired` check with `Invoke()`
2. **Handle Creation Check:** `IsHandleCreated` before Syncfusion calls
3. **Message Loop Check:** `Application.MessageLoop` validation
4. **Disposal Check:** `IsDisposed` and `Disposing` guards

### Edge Cases Covered

| Scenario              | Protection                        | Test Coverage |
| --------------------- | --------------------------------- | ------------- |
| Concurrent saves      | `_isSavingLayout` flag + lock     | ✅ 5 tests    |
| Rapid UI changes      | 1500ms debounce + 2s min interval | ✅ 3 tests    |
| Corrupt XML           | Delete + reload defaults          | ✅ 2 tests    |
| Empty file            | Size check + delete               | ✅ 1 test     |
| Read-only directory   | Fallback to temp path             | ✅ 1 test     |
| Partial writes        | Atomic .tmp → final rename        | ✅ 2 tests    |
| Cross-thread calls    | InvokeRequired + marshaling       | ✅ 2 tests    |
| Premature operations  | Handle + message loop checks      | ✅ 2 tests    |
| Disposal race         | Try-catch + cleanup verification  | ✅ 2 tests    |
| Syncfusion exceptions | NullRef + ArgumentException catch | ✅ 2 tests    |
| Memory leaks          | 50-cycle stress test              | ✅ 1 test     |

## Running the Tests

### Run all persistence tests

```powershell
dotnet test tests/WileyWidget.WinForms.Tests/WileyWidget.WinForms.Tests.csproj `
    --filter "Category=DockingPersistence|Category=DockingConcurrency" `
    --verbosity minimal
```

### Run stress tests only

```powershell
dotnet test tests/WileyWidget.WinForms.Tests/WileyWidget.WinForms.Tests.csproj `
    --filter "Category=Stress" `
    --verbosity minimal
```

### Run individual test

```powershell
dotnet test tests/WileyWidget.WinForms.Tests/WileyWidget.WinForms.Tests.csproj `
    --filter "FullyQualifiedName~SaveDockingLayout_WhenConcurrentSavesAttempted"
```

## Test Execution Environment

- **Threading Model:** STA (Single Threaded Apartment) required for WinForms
- **Isolation:** Each test uses unique temp directory to avoid conflicts
- **Cleanup:** `IDisposable` pattern ensures test file cleanup
- **Reflection:** Private method/field access for internal state validation

## Known Limitations

1. **OS Dependencies:** Read-only directory tests are best-effort (OS-specific)
2. **Timing Sensitivity:** Debounce tests use sleep intervals (may vary under load)
3. **Memory Threshold:** 10MB growth threshold is heuristic (runtime-dependent)
4. **Syncfusion Internals:** Cannot fully simulate internal Syncfusion exceptions

## Future Enhancements

- [ ] Add tests for network share scenarios (UNC paths)
- [ ] Test behavior with extremely large layout files (>10MB)
- [ ] Add performance benchmarks for save/load operations
- [ ] Test interaction with Windows roaming profiles
- [ ] Add code coverage analysis integration

## References

- **Implementation:** [MainForm.UI.cs](../../src/WileyWidget.WinForms/Forms/MainForm.UI.cs)
- **Syncfusion Docs:** [DockingManager Layouts](https://help.syncfusion.com/windowsforms/docking-manager/layouts)

---

**Last Updated:** December 13, 2025
**Test Pass Rate:** 21/21 (100%)
**Coverage Status:** ✅ All identified edge cases covered
