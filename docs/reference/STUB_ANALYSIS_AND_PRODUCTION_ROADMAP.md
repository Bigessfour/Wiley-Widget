# Stub Analysis and Production Development Roadmap

**Generated**: November 4, 2025
**Purpose**: Identify all stub/incomplete implementations requiring production development

---

## 🔍 Executive Summary

Analysis reveals **3 major stub categories** requiring production implementation:

1. **WileyWidget.UI Project** - Incomplete skeleton with 43 build errors
2. **src/App.xaml.cs** - Stub bootstrapper with missing implementations
3. **ReportsView** - Disabled BoldReports controls pending proper setup

---

## 1️⃣ WileyWidget.UI Project - MAJOR STUB

### Status: ⚠️ **INCOMPLETE SKELETON PROJECT**

### Current State

- **Purpose**: Intended as a modular UI library for WPF views/ViewModels
- **Issue**: Has structure but lacks full implementation
- **Build Status**: ❌ **43+ compilation errors**

### Files Found

#### Stub Files to Delete:

```
WileyWidget.UI/Class1.cs  (empty class)
```

#### Incomplete Implementations:

**1. App.xaml.cs (src/App.xaml.cs)** - Line 21

```csharp
public partial class App : PrismApplication
{
    // Missing:
    // - CreateShell() implementation
    // - CreateContainerExtension() implementation
    // - Full module catalog initialization
```

**Issues**:

- Missing `InitializeComponent()` (no App.xaml backing)
- Missing abstract Prism method implementations
- IHttpClientFactory, IQuickBooksService, IAIService namespace issues
- DbContextFactory resolution errors

**2. ViewModels (Incomplete Constructors)**

**SettingsViewModel.cs** - Has fallback parameterless constructor ✅

```csharp
// Line 18-25: Already has proper fallback
protected SettingsViewModel() { ... }
```

**ReportsViewModel.cs** - Has parameterless constructor ✅

```csharp
// Line 275: Already implemented
protected ReportsViewModel() { ... }
```

**SettingsPanelViewModel.cs** - Has fallback constructor ✅

```csharp
// Line 1720: Already has fallback
protected SettingsPanelViewModel() { ... }
```

**3. Views with Disabled Controls**

**ReportsView.xaml** - Line 146

```xml
<!-- TODO: ReportViewer control temporarily disabled due to namespace issues -->
```

**Issue**: Using deprecated `Syncfusion.ReportViewer.WPF` instead of `BoldReports.WPF`

**MunicipalAccountView.xaml** - Line 168-172

```xml
<syncfusion:RibbonButton Label="Export (Stub)" ... />
```

**Issue**: Export stub button for testing, not production implementation

---

## 2️⃣ src/App.xaml.cs - PRODUCTION BOOTSTRAP STUB

### Current State

Lines 1-207: Hybrid stub with some production code

### Missing Implementations

#### ❌ CreateShell() - REQUIRED

```csharp
protected override void CreateWindow()
{
    // STUB: Not implemented
    // Should return main Shell window
    return Container.Resolve<Shell>();
}
```

#### ❌ CreateContainerExtension() - REQUIRED

```csharp
protected override IContainerExtension CreateContainerExtension()
{
    // STUB: Not implemented
    // Should return DryIoc container
    return new DryIocContainerExtension(new Container());
}
```

#### ❌ ConfigureModuleCatalog() - REQUIRED

```csharp
protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
{
    // STUB: Incomplete
    // Should register all Prism modules
    moduleCatalog.AddModule<CoreModule>();
    moduleCatalog.AddModule<UIModule>();
}
```

#### ⚠️ CreateResilientHttpClient() - Line 178

```csharp
private IHttpClientFactory CreateResilientHttpClient(IServiceProvider provider)
{
    // STUB: Type not found
    // Namespace issue - missing using directive or assembly reference
}
```

---

## 3️⃣ ReportsView - DISABLED BOLD REPORTS INTEGRATION

### Current State

**File**: `WileyWidget.UI/Views/Main/ReportsView.xaml`

### Issue

Using old deprecated namespace:

```xml
xmlns:reports="clr-namespace:Syncfusion.ReportViewer.WPF;assembly=Syncfusion.ReportViewer.WPF"
```

### Required Changes

1. **Update Package Reference**

   ```xml
   <!-- WileyWidget.UI.csproj -->
   <PackageReference Include="BoldReports.WPF" />  <!-- ✅ Already added -->
   ```

2. **Update XAML Namespace**

   ```xml
   <!-- Change from: -->
   xmlns:reports="clr-namespace:Syncfusion.ReportViewer.WPF;assembly=Syncfusion.ReportViewer.WPF"

   <!-- To: -->
   xmlns:boldreports="clr-namespace:BoldReports.WPF;assembly=BoldReports.WPF"
   ```

3. **Update Control Usage**

   ```xml
   <!-- Change from: -->
   <reports:ReportViewer ... />

   <!-- To: -->
   <boldreports:ReportViewer ... />
   ```

---

## 4️⃣ Service Stubs - MOCK IMPLEMENTATIONS

### Found in Tests (✅ Appropriate)

**IAIServiceTests.cs** - Lines 11-23

```csharp
[Fact]
public async Task NullAIService_GetInsights_ReturnsStubText()
{
    res.Should().Contain("Dev Stub");  // ✅ Test stub - correct usage
}
```

**Status**: ✅ These are test mocks/stubs - **do not change**

---

## 5️⃣ EnterpriseViewModel Navigation - PARTIAL STUB

**File**: `WileyWidget.UI/ViewModels/Main/EnterpriseViewModel.cs`
**Line**: 362

```csharp
// For now, this is a stub that can be implemented based on the app's navigation pattern
```

### Issue

Navigation logic incomplete

### Required Implementation

```csharp
// Current stub:
private void NavigateToModule(string moduleName)
{
    // TODO: Implement navigation
}

// Production code needed:
private void NavigateToModule(string moduleName)
{
    _regionManager.RequestNavigate("ContentRegion", moduleName);
}
```

---

## 📋 PRODUCTION DEVELOPMENT PRIORITY

### ⚠️ **HIGH PRIORITY** (Blocks Application)

1. **Complete src/App.xaml.cs** (Blocking)
   - [ ] Implement `CreateShell()`
   - [ ] Implement `CreateContainerExtension()`
   - [ ] Implement `ConfigureModuleCatalog()`
   - [ ] Fix `CreateResilientHttpClient()` namespace
   - [ ] Add `App.xaml` file (missing)

2. **Decision: WileyWidget.UI Project** (Architecture)
   - **Option A**: Complete full implementation (60+ hours)
   - **Option B**: Merge into main WileyWidget.csproj (recommended)
   - **Option C**: Exclude from build until needed

### 🔧 **MEDIUM PRIORITY** (Feature Incomplete)

3. **Fix ReportsView BoldReports Integration**
   - [ ] Update XAML namespace from Syncfusion.ReportViewer to BoldReports
   - [ ] Update control bindings
   - [ ] Remove TODO comment (line 146)
   - [ ] Test report rendering

4. **Complete EnterpriseViewModel Navigation**
   - [ ] Implement `NavigateToModule()` method
   - [ ] Wire up region navigation
   - [ ] Add navigation validation

### 📊 **LOW PRIORITY** (UI Polish)

5. **MunicipalAccountView Export Stub**
   - [ ] Replace "Export (Stub)" button with real export
   - [ ] Implement actual file dialog
   - [ ] Add Excel/PDF export functionality

---

## 🎯 RECOMMENDED ACTION PLAN

### Phase 1: Unblock Application (Week 1)

**Goal**: Get WileyWidget.UI building successfully

```powershell
# Option 1: Exclude WileyWidget.UI from solution
dotnet sln remove WileyWidget.UI/WileyWidget.UI.csproj

# Option 2: Fix critical errors
# - Implement missing Prism methods in App.xaml.cs
# - Add using directives for missing namespaces
# - Create App.xaml backing file
```

### Phase 2: BoldReports Integration (Week 2)

1. Update ReportsView.xaml namespace
2. Test BoldReports license registration
3. Verify report rendering works

### Phase 3: Complete Navigation (Week 3)

1. Implement EnterpriseViewModel navigation
2. Test module switching
3. Add error handling

### Phase 4: UI Polish (Week 4)

1. Replace stub export button
2. Implement real export functionality
3. Add progress indicators

---

## 📦 FILES REQUIRING PRODUCTION CODE

### Must Implement:

```
✅ src/App.xaml.cs                     (add CreateShell, CreateContainerExtension)
✅ src/App.xaml                        (create file - missing)
⚠️ WileyWidget.UI/App.xaml.cs         (needs namespaces + implementations)
⚠️ WileyWidget.UI/Views/Main/ReportsView.xaml (update to BoldReports)
```

### Can Delete:

```
❌ WileyWidget.UI/Class1.cs            (empty stub)
```

### Keep As-Is (Test Stubs):

```
✅ WileyWidget.Tests/IAIServiceTests.cs
✅ WileyWidget.Tests/ModuleInitializationTests.cs
✅ WileyWidget.Tests/WileyWidget.ViewModels.Tests/DISmokeTests.cs
```

---

## 🔧 QUICK FIX: Minimum Viable Implementation

To unblock development immediately:

```csharp
// Add to src/App.xaml.cs:

protected override void CreateWindow()
{
    return Container.Resolve<Shell>();
}

protected override IContainerExtension CreateContainerExtension()
{
    return new DryIocContainerExtension(new Container());
}

protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
{
    moduleCatalog.AddModule<CoreModule>();
}
```

---

## ✅ VERIFICATION

After completing Phase 1:

```powershell
# 1. Verify build succeeds
dotnet build WileyWidget.sln --no-restore

# 2. Verify licenses registered
.\scripts\verify-licenses.ps1

# 3. Run application
dotnet run --project WileyWidget.csproj
```

---

**Next Steps**: Review this analysis and confirm the approach (Option A/B/C) for WileyWidget.UI project.
