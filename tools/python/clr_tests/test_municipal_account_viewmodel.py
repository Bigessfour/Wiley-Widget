"""Tests for MunicipalAccountViewModel using pythonnet stubs."""

from __future__ import annotations

import pytest

# Check for pythonnet availability
try:
    import clr  # type: ignore[import-not-found]
    HAS_PYTHONNET = True
except (ImportError, RuntimeError, AttributeError):
    HAS_PYTHONNET = False

pytestmark = [
    pytest.mark.clr,
    pytest.mark.integration,
    pytest.mark.skipif(not HAS_PYTHONNET, reason="pythonnet required for CLR tests"),
]

# Import CLR types only if available
if HAS_PYTHONNET:
    clr.AddReference("System")
    clr.AddReference("System.Collections")
    clr.AddReference("WileyWidget")
    try:
        from System import (  # type: ignore[attr-defined, import-not-found]
            Activator,
            Array,
            Object,
        )
        from System import (
            Exception as NetException,
        )
        from System.Collections.Generic import (
            List,  # type: ignore[attr-defined, import-not-found]
        )
        from System.Threading.Tasks import (
            Task,  # type: ignore[attr-defined, import-not-found]
        )
    except Exception:
        Activator = None
        Array = None
        Object = None
        NetException = None
        List = None
        Task = None

from .helpers import dotnet_utils


def _await(task):
    return task.GetAwaiter().GetResult()


class MunicipalAccountRepositoryStub(
    dotnet_utils.get_type.__annotations__ if False else object
):
    # This is a stub type for static analysis and test readability.
    pass


def _create_account_list(assemblies_dir, count: int = 1):
    account_type = dotnet_utils.get_type(
        assemblies_dir, "WileyWidget.Models", "WileyWidget.Models.MunicipalAccount"
    )
    account_number_type = dotnet_utils.get_type(
        assemblies_dir, "WileyWidget.Models", "WileyWidget.Models.AccountNumber"
    )
    accounts = List[account_type]()
    for index in range(count):
        account = Activator.CreateInstance(account_type)
        account.AccountNumber = Activator.CreateInstance(
            account_number_type, Array[Object]([f"405.{index}"])
        )
        account.Name = f"Test Account {index}"
        account.DepartmentId = 1
        account.BudgetPeriodId = 1
        accounts.Add(account)
    return accounts


def _create_repo_interface(assemblies_dir, accounts, raise_error: bool = False):
    # Removed unused import of TaskCompletionSource

    class Repository(IMunicipalAccountRepository):
        def __init__(self, data, fail):
            self._data = data
            self._fail = fail

        def GetAllAsync(self):
            if self._fail:
                raise NetException("Load failed")
            return Task.FromResult(self._data)

        def GetByAccountNumberAsync(self, __):
            return Task.FromResult(None)

        def GetByDepartmentAsync(self, __):
            return Task.FromResult(self._data)

        def AddAsync(self, account):
            self._data.Add(account)
            return Task.FromResult(account)

        def UpdateAsync(self, account):
            return Task.FromResult(account)

        def DeleteAsync(self, _):
            return Task.FromResult(False)

        def SyncFromQuickBooksAsync(self, __):
            return Task.CompletedTask

        def GetBudgetAnalysisAsync(self, __):
            return Task.FromResult(object())

        def GetByFundAsync(self, __):
            return Task.FromResult(self._data)

        def GetByTypeAsync(self, __):
            return Task.FromResult(self._data)

    return Repository(accounts, raise_error)


def _create_grok_stub(assemblies_dir):
    from WileyWidget.Services import IGrokSupercomputer  # type: ignore[attr-defined]

    report_data_type = dotnet_utils.get_type(
        assemblies_dir, "WileyWidget.Models", "WileyWidget.Models.ReportData"
    )
    analytics_type = dotnet_utils.get_type(
        assemblies_dir, "WileyWidget.Models", "WileyWidget.Models.AnalyticsData"
    )
    budget_insights_type = dotnet_utils.get_type(
        assemblies_dir, "WileyWidget.Models", "WileyWidget.Models.BudgetInsights"
    )
    compliance_type = dotnet_utils.get_type(
        assemblies_dir, "WileyWidget.Models", "WileyWidget.Models.ComplianceReport"
    )

    class GrokStub(IGrokSupercomputer):
        def FetchEnterpriseDataAsync(self, *__):
            return Task.FromResult(Activator.CreateInstance(report_data_type))

        def RunReportCalcsAsync(self, *__):
            return Task.FromResult(Activator.CreateInstance(analytics_type))

        def AnalyzeBudgetDataAsync(self, *__):
            return Task.FromResult(Activator.CreateInstance(budget_insights_type))

        def GenerateComplianceReportAsync(self, *__):
            return Task.FromResult(Activator.CreateInstance(compliance_type))

        def AnalyzeMunicipalDataAsync(self, *__):
            return Task.FromResult("Analysis complete")

        def GenerateRecommendationsAsync(self, *__):
            return Task.FromResult("Recommendation")

    return GrokStub()


def _create_viewmodel(assemblies_dir, repo, grok):
    viewmodel_type = dotnet_utils.get_type(
        assemblies_dir,
        "WileyWidget",
        "WileyWidget.ViewModels.MunicipalAccountViewModel",
    )
    return Activator.CreateInstance(viewmodel_type, Array[Object]([repo, None, grok]))


@pytest.fixture()
def viewmodel_success(
    clr_loader, ensure_assemblies_present, load_wileywidget_core, system_runtime
):
    clr_loader("Microsoft.Extensions.DependencyInjection")
    accounts = _create_account_list(ensure_assemblies_present)
    repo = _create_repo_interface(ensure_assemblies_present, accounts)
    grok = _create_grok_stub(ensure_assemblies_present)
    vm = _create_viewmodel(ensure_assemblies_present, repo, grok)
    return vm, repo


@pytest.fixture()
def viewmodel_failure(
    clr_loader, ensure_assemblies_present, load_wileywidget_core, system_runtime
):
    repo = _create_repo_interface(
        ensure_assemblies_present,
        _create_account_list(ensure_assemblies_present, 0),
        True,
    )
    grok = _create_grok_stub(ensure_assemblies_present)
    vm = _create_viewmodel(ensure_assemblies_present, repo, grok)
    return vm


def test_viewmodel_initialization(viewmodel_success):
    vm, _ = viewmodel_success
    assert vm.StatusMessage == "Ready"
    assert vm.MunicipalAccounts.Count == 0


def test_load_accounts_success(viewmodel_success):
    vm, _ = viewmodel_success
    command = vm.LoadAccountsAsyncCommand
    _await(command.ExecuteAsync(None))
    assert vm.MunicipalAccounts.Count == 1
    assert vm.StatusMessage.startswith("Loaded")


def test_load_accounts_empty(
    clr_loader, ensure_assemblies_present, load_wileywidget_core
):
    repo = _create_repo_interface(
        ensure_assemblies_present, _create_account_list(ensure_assemblies_present, 0)
    )
    grok = _create_grok_stub(ensure_assemblies_present)
    vm = _create_viewmodel(ensure_assemblies_present, repo, grok)
    command = vm.LoadAccountsAsyncCommand
    _await(command.ExecuteAsync(None))
    assert vm.MunicipalAccounts.Count == 0
    assert "0 accounts" in vm.StatusMessage


def test_load_accounts_error(viewmodel_failure):
    vm = viewmodel_failure
    command = vm.LoadAccountsAsyncCommand
    _await(command.ExecuteAsync(None))
    assert vm.HasError
    assert "failed" in vm.ErrorMessage.lower()
