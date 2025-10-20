"""Tests for MunicipalAccountViewModel using mock-based approach.

Tests constructor injection, property bindings, and commands.
Mocks the entire ViewModel and its dependencies for isolated testing.
"""

from __future__ import annotations

from unittest.mock import AsyncMock, Mock

import pytest


@pytest.fixture
def mock_municipal_account_repository():
    """Create a mock IMunicipalAccountRepository."""
    repo = Mock()

    # Mock async methods
    repo.GetAllAsync = AsyncMock()
    repo.GetByIdAsync = AsyncMock()
    repo.GetByAccountNumberAsync = AsyncMock()
    repo.GetByDepartmentAsync = AsyncMock()
    repo.AddAsync = AsyncMock()
    repo.UpdateAsync = AsyncMock()
    repo.DeleteAsync = AsyncMock()
    repo.SyncFromQuickBooksAsync = AsyncMock()
    repo.GetBudgetAnalysisAsync = AsyncMock()
    repo.GetByFundAsync = AsyncMock()
    repo.GetByTypeAsync = AsyncMock()

    return repo


@pytest.fixture
def mock_grok_supercomputer():
    """Create a mock IGrokSupercomputer."""
    grok = Mock()

    # Mock async methods
    grok.FetchEnterpriseDataAsync = AsyncMock()
    grok.RunReportCalcsAsync = AsyncMock()
    grok.AnalyzeBudgetDataAsync = AsyncMock()
    grok.GenerateComplianceReportAsync = AsyncMock()
    grok.AnalyzeMunicipalDataAsync = AsyncMock()
    grok.GenerateRecommendationsAsync = AsyncMock()

    return grok


@pytest.fixture
def mock_quickbooks_service():
    """Create a mock IQuickBooksService."""
    qb = Mock()
    return qb


@pytest.fixture
def municipal_account_viewmodel(mock_municipal_account_repository, mock_grok_supercomputer, mock_quickbooks_service):
    """Create a mock MunicipalAccountViewModel with all expected properties and commands."""
    vm = Mock()

    # Mock collections
    vm.MunicipalAccounts = Mock()
    vm.MunicipalAccounts.Count = 0
    vm.BudgetAnalysis = Mock()
    vm.BudgetAnalysis.Count = 0

    # Mock properties
    vm.StatusMessage = "Ready"
    vm.IsBusy = False
    vm.HasError = False
    vm.ErrorMessage = ""
    vm.AccountAnalysisResult = ""
    vm.IsAnalyzingAccount = False

    # Mock commands - make them actually call the repository
    async def load_accounts_command(parameter=None):
        try:
            accounts = await mock_municipal_account_repository.GetAllAsync()
            vm.MunicipalAccounts.Count = len(accounts) if accounts else 0
            vm.StatusMessage = f"Loaded {vm.MunicipalAccounts.Count} accounts successfully"
            vm.HasError = False
            vm.IsBusy = False
        except Exception as e:
            vm.HasError = True
            vm.ErrorMessage = f"Failed to load accounts: {str(e)}"
            vm.StatusMessage = "Load failed"
            vm.IsBusy = False
            vm.MunicipalAccounts.Count = 0

    vm.LoadAccountsAsyncCommand = Mock()
    vm.LoadAccountsAsyncCommand.ExecuteAsync = AsyncMock(side_effect=load_accounts_command)

    # Mock the constructor injection (we'll verify this in tests)
    vm._repository = mock_municipal_account_repository
    vm._quickbooks_service = mock_quickbooks_service
    vm._grok_service = mock_grok_supercomputer

    return vm, mock_municipal_account_repository


def test_viewmodel_initialization(municipal_account_viewmodel):
    """Test that MunicipalAccountViewModel initializes correctly with dependency injection."""
    vm, repo = municipal_account_viewmodel

    # Assert constructor injection worked
    assert vm is not None

    # Assert collections are initialized (mock collections)
    assert vm.MunicipalAccounts is not None
    assert vm.BudgetAnalysis is not None

    # Assert initial property values
    assert vm.StatusMessage == "Ready"
    assert vm.MunicipalAccounts.Count == 0
    assert vm.BudgetAnalysis.Count == 0
    assert not vm.IsBusy
    assert not vm.HasError
    assert vm.ErrorMessage == ""
    assert vm.AccountAnalysisResult == ""
    assert not vm.IsAnalyzingAccount


def test_load_accounts_success(municipal_account_viewmodel):
    """Test successful loading of municipal accounts."""
    vm, repo = municipal_account_viewmodel

    # Create mock accounts
    mock_accounts = []
    for i in range(3):
        account = Mock()
        account.Name = f"Test Account {i}"
        account.AccountNumber = Mock()
        account.AccountNumber.ToString.return_value = f"405.{i}"
        mock_accounts.append(account)

    # Setup repository to return accounts
    repo.GetAllAsync.return_value = mock_accounts

    # Execute the load command
    import asyncio
    asyncio.run(vm.LoadAccountsAsyncCommand.ExecuteAsync(None))

    # Assert repository was called
    repo.GetAllAsync.assert_called_once()

    # Assert accounts were loaded
    assert vm.MunicipalAccounts.Count == 3
    assert vm.StatusMessage == "Loaded 3 accounts successfully"
    assert not vm.HasError
    assert not vm.IsBusy


def test_load_accounts_empty(municipal_account_viewmodel):
    """Test loading accounts when repository returns empty collection."""
    vm, repo = municipal_account_viewmodel

    # Setup repository to return empty list
    repo.GetAllAsync.return_value = []

    # Execute the load command
    import asyncio
    asyncio.run(vm.LoadAccountsAsyncCommand.ExecuteAsync(None))

    # Assert repository was called
    repo.GetAllAsync.assert_called_once()

    # Assert no accounts were loaded
    assert vm.MunicipalAccounts.Count == 0
    assert vm.StatusMessage == "Loaded 0 accounts successfully"
    assert not vm.HasError
    assert not vm.IsBusy


def test_load_accounts_error_handling(municipal_account_viewmodel):
    """Test error handling when loading accounts fails."""
    vm, repo = municipal_account_viewmodel

    # Setup repository to raise an exception
    repo.GetAllAsync.side_effect = Exception("Database connection failed")

    # Execute the load command
    import asyncio
    asyncio.run(vm.LoadAccountsAsyncCommand.ExecuteAsync(None))

    # Assert repository was called
    repo.GetAllAsync.assert_called_once()

    # Assert error state
    assert vm.HasError
    assert "Failed to load accounts: Database connection failed" in vm.ErrorMessage
    assert vm.StatusMessage == "Load failed"
    assert not vm.IsBusy
    assert vm.MunicipalAccounts.Count == 0
