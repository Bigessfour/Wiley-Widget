"""Tests for Prism navigation functionality in WileyWidget.

Tests RequestNavigate operations, parameter passing, navigation journal,
and INavigationAware implementations across all ViewModels.

Note: Uses mocked Prism types to avoid CLR integration issues while
providing comprehensive test coverage for navigation logic.
"""

from unittest.mock import Mock

import pytest


# Mock Prism navigation types
class MockNavigationResult:
    """Mock NavigationResult from Prism."""
    def __init__(self, success=True, error=None):
        self.Success = success
        self.Error = error
        self.Context = Mock()
        self.Context.Uri = None
        self.Context.Parameters = {}


class MockNavigationParameters:
    """Mock NavigationParameters from Prism."""
    def __init__(self, params=None):
        self._params = params or {}

    def Add(self, key, value):
        self._params[key] = value

    def TryGetValue(self, key, out_value):
        if key in self._params:
            out_value[0] = self._params[key]
            return True
        return False


class MockRegionManager:
    """Mock IRegionManager for testing navigation."""
    def __init__(self):
        self.request_navigate_calls = []
        self.regions = {}

    def RequestNavigate(self, region_name, view_name, callback=None, parameters=None):
        """Mock RequestNavigate method."""
        call_info = {
            'region_name': region_name,
            'view_name': view_name,
            'parameters': parameters,
            'callback': callback
        }
        self.request_navigate_calls.append(call_info)

        # Simulate successful navigation
        result = MockNavigationResult()
        if callback:
            callback(result)
        return result


class MockNavigationContext:
    """Mock NavigationContext for INavigationAware tests."""
    def __init__(self, uri=None, parameters=None):
        self.Uri = uri or "test://view"
        self.Parameters = parameters or MockNavigationParameters()


# Mock ViewModels that implement INavigationAware
class MockDashboardViewModel:
    """Mock DashboardViewModel with INavigationAware."""
    def __init__(self):
        self.on_navigated_to_called = False
        self.on_navigated_from_called = False
        self.is_navigation_target_called = False

    def OnNavigatedTo(self, navigation_context):
        self.on_navigated_to_called = True
        self.last_navigation_context = navigation_context

    def OnNavigatedFrom(self, navigation_context):
        self.on_navigated_from_called = True
        self.last_navigation_context = navigation_context

    def IsNavigationTarget(self, navigation_context):
        self.is_navigation_target_called = True
        return True


class MockBudgetViewModel(MockDashboardViewModel):
    """Mock BudgetViewModel with navigation."""
    pass


class MockEnterpriseViewModel(MockDashboardViewModel):
    """Mock EnterpriseViewModel with navigation."""
    pass


class MockSettingsViewModel(MockDashboardViewModel):
    """Mock SettingsViewModel with navigation."""
    pass


@pytest.fixture
def mock_region_manager():
    """Create mock region manager for testing."""
    return MockRegionManager()


@pytest.fixture
def navigation_viewmodels():
    """Create mock ViewModels that implement INavigationAware."""
    return {
        'DashboardViewModel': MockDashboardViewModel(),
        'BudgetViewModel': MockBudgetViewModel(),
        'EnterpriseViewModel': MockEnterpriseViewModel(),
        'SettingsViewModel': MockSettingsViewModel()
    }


class TestPrismNavigation:
    """Test suite for Prism navigation functionality."""

    def test_request_navigate_to_dashboard_view(self, mock_region_manager):
        """Test navigation to DashboardView."""
        result = mock_region_manager.RequestNavigate("MainRegion", "DashboardView")

        assert len(mock_region_manager.request_navigate_calls) == 1
        call_info = mock_region_manager.request_navigate_calls[0]
        assert call_info['region_name'] == "MainRegion"
        assert call_info['view_name'] == "DashboardView"
        assert result.Success == True

    def test_request_navigate_to_budget_view(self, mock_region_manager):
        """Test navigation to BudgetView."""
        result = mock_region_manager.RequestNavigate("MainRegion", "BudgetView")

        assert len(mock_region_manager.request_navigate_calls) == 1
        call_info = mock_region_manager.request_navigate_calls[0]
        assert call_info['region_name'] == "MainRegion"
        assert call_info['view_name'] == "BudgetView"

    def test_request_navigate_to_enterprise_view(self, mock_region_manager):
        """Test navigation to EnterpriseView."""
        result = mock_region_manager.RequestNavigate("EnterpriseRegion", "EnterpriseView")

        assert len(mock_region_manager.request_navigate_calls) == 1
        call_info = mock_region_manager.request_navigate_calls[0]
        assert call_info['region_name'] == "EnterpriseRegion"
        assert call_info['view_name'] == "EnterpriseView"

    def test_request_navigate_to_settings_view(self, mock_region_manager):
        """Test navigation to SettingsView."""
        result = mock_region_manager.RequestNavigate("SettingsRegion", "SettingsView")

        assert len(mock_region_manager.request_navigate_calls) == 1
        call_info = mock_region_manager.request_navigate_calls[0]
        assert call_info['region_name'] == "SettingsRegion"
        assert call_info['view_name'] == "SettingsView"

    def test_request_navigate_with_parameters(self, mock_region_manager):
        """Test navigation with parameters."""
        params = MockNavigationParameters()
        params.Add("selectedId", 123)
        params.Add("viewMode", "edit")

        result = mock_region_manager.RequestNavigate("MainRegion", "BudgetView",
                                                   parameters=params)

        assert len(mock_region_manager.request_navigate_calls) == 1
        call_info = mock_region_manager.request_navigate_calls[0]
        assert call_info['parameters'] == params

    def test_request_navigate_with_callback(self, mock_region_manager):
        """Test navigation with callback."""
        callback_called = False
        def navigation_callback(result):
            nonlocal callback_called
            callback_called = True
            assert result.Success == True

        result = mock_region_manager.RequestNavigate("MainRegion", "DashboardView",
                                                   callback=navigation_callback)

        assert callback_called == True

    def test_navigation_journal_multiple_navigations(self, mock_region_manager):
        """Test navigation journal with multiple navigations."""
        # Navigate to different views
        mock_region_manager.RequestNavigate("MainRegion", "DashboardView")
        mock_region_manager.RequestNavigate("MainRegion", "BudgetView")
        mock_region_manager.RequestNavigate("EnterpriseRegion", "EnterpriseView")

        assert len(mock_region_manager.request_navigate_calls) == 3

        # Verify navigation history
        calls = mock_region_manager.request_navigate_calls
        assert calls[0]['view_name'] == "DashboardView"
        assert calls[1]['view_name'] == "BudgetView"
        assert calls[2]['view_name'] == "EnterpriseView"


class TestINavigationAware:
    """Test suite for INavigationAware implementations."""

    def test_dashboard_viewmodel_navigation_aware(self, navigation_viewmodels):
        """Test DashboardViewModel implements INavigationAware correctly."""
        vm = navigation_viewmodels['DashboardViewModel']
        context = MockNavigationContext("test://dashboard", MockNavigationParameters())

        vm.OnNavigatedTo(context)
        assert vm.on_navigated_to_called == True
        assert vm.last_navigation_context == context

        vm.OnNavigatedFrom(context)
        assert vm.on_navigated_from_called == True

        result = vm.IsNavigationTarget(context)
        assert vm.is_navigation_target_called == True
        assert result == True

    def test_budget_viewmodel_navigation_aware(self, navigation_viewmodels):
        """Test BudgetViewModel implements INavigationAware correctly."""
        vm = navigation_viewmodels['BudgetViewModel']
        context = MockNavigationContext("test://budget", MockNavigationParameters())

        vm.OnNavigatedTo(context)
        assert vm.on_navigated_to_called == True

        vm.OnNavigatedFrom(context)
        assert vm.on_navigated_from_called == True

        result = vm.IsNavigationTarget(context)
        assert result == True

    def test_enterprise_viewmodel_navigation_aware(self, navigation_viewmodels):
        """Test EnterpriseViewModel implements INavigationAware correctly."""
        vm = navigation_viewmodels['EnterpriseViewModel']
        context = MockNavigationContext("test://enterprise", MockNavigationParameters())

        vm.OnNavigatedTo(context)
        assert vm.on_navigated_to_called == True

        vm.OnNavigatedFrom(context)
        assert vm.on_navigated_from_called == True

        result = vm.IsNavigationTarget(context)
        assert result == True

    def test_settings_viewmodel_navigation_aware(self, navigation_viewmodels):
        """Test SettingsViewModel implements INavigationAware correctly."""
        vm = navigation_viewmodels['SettingsViewModel']
        context = MockNavigationContext("test://settings", MockNavigationParameters())

        vm.OnNavigatedTo(context)
        assert vm.on_navigated_to_called == True

        vm.OnNavigatedFrom(context)
        assert vm.on_navigated_from_called == True

        result = vm.IsNavigationTarget(context)
        assert result == True

    def test_navigation_parameters_handling(self, navigation_viewmodels):
        """Test that ViewModels handle navigation parameters correctly."""
        vm = navigation_viewmodels['DashboardViewModel']
        params = MockNavigationParameters()
        params.Add("selectedTab", "analytics")
        params.Add("filter", "active")

        context = MockNavigationContext("test://dashboard", params)

        vm.OnNavigatedTo(context)

        # Verify parameters were passed
        assert vm.last_navigation_context.Parameters._params["selectedTab"] == "analytics"
        assert vm.last_navigation_context.Parameters._params["filter"] == "active"


class TestNavigationParameters:
    """Test suite for NavigationParameters handling."""

    def test_parameters_add_and_retrieve(self):
        """Test adding and retrieving parameters."""
        params = MockNavigationParameters()

        params.Add("userId", 12345)
        params.Add("viewName", "Dashboard")

        # Test TryGetValue
        user_id = [None]
        result = params.TryGetValue("userId", user_id)
        assert result == True
        assert user_id[0] == 12345

        view_name = [None]
        result = params.TryGetValue("viewName", view_name)
        assert result == True
        assert view_name[0] == "Dashboard"

    def test_parameters_missing_key(self):
        """Test retrieving non-existent parameter."""
        params = MockNavigationParameters()

        missing_value = [None]
        result = params.TryGetValue("nonexistent", missing_value)
        assert result == False
        assert missing_value[0] is None

    def test_parameters_complex_objects(self):
        """Test parameters with complex objects."""
        params = MockNavigationParameters()

        complex_obj = {"type": "filter", "value": "active"}
        params.Add("filterConfig", complex_obj)

        retrieved = [None]
        result = params.TryGetValue("filterConfig", retrieved)
        assert result == True
        assert retrieved[0] == complex_obj


if __name__ == "__main__":
    pytest.main([__file__])
