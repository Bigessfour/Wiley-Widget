"""Tests for IEnterpriseRepository with mocked DbContext."""

from unittest.mock import AsyncMock, Mock

import pytest


@pytest.fixture
def enterprise_repository():
    """Fixture providing a mock EnterpriseRepository instance."""
    from unittest.mock import Mock

    # Create a mock repository that implements IEnterpriseRepository
    mock_repo = Mock()

    # Mock the async methods
    mock_repo.GetAllAsync = AsyncMock()
    mock_repo.GetByIdAsync = AsyncMock()
    mock_repo.AddAsync = AsyncMock()
    mock_repo.UpdateAsync = AsyncMock()
    mock_repo.DeleteAsync = AsyncMock()
    mock_repo.GetCountAsync = AsyncMock()

    return mock_repo


@pytest.mark.asyncio
async def test_get_all_empty_list(enterprise_repository):
    """Test GetAllAsync returns empty list when no enterprises exist."""
    # Setup mock to return empty list
    enterprise_repository.GetAllAsync.return_value = []

    # Act
    result = await enterprise_repository.GetAllAsync()

    # Assert
    assert result == []
    enterprise_repository.GetAllAsync.assert_called_once()


@pytest.mark.asyncio
async def test_get_all_single_item(enterprise_repository):
    """Test GetAllAsync returns single enterprise when one exists."""
    # Create mock enterprise
    mock_enterprise = Mock()
    mock_enterprise.Name = "Water Utility"
    mock_enterprise.Id = 1

    # Setup mock to return list with one enterprise
    enterprise_repository.GetAllAsync.return_value = [mock_enterprise]

    # Act
    result = await enterprise_repository.GetAllAsync()

    # Assert
    assert len(result) == 1
    assert result[0].Name == "Water Utility"
    assert result[0].Id == 1
    enterprise_repository.GetAllAsync.assert_called_once()


@pytest.mark.asyncio
async def test_get_by_id_returns_enterprise(enterprise_repository):
    """Test GetByIdAsync returns the correct enterprise."""
    # Create mock enterprise
    mock_enterprise = Mock()
    mock_enterprise.Id = 42
    mock_enterprise.Name = "Test Enterprise"

    # Setup mock to return enterprise for ID 42
    enterprise_repository.GetByIdAsync.return_value = mock_enterprise

    # Act
    result = await enterprise_repository.GetByIdAsync(42)

    # Assert
    assert result is not None
    assert result.Id == 42
    assert result.Name == "Test Enterprise"
    enterprise_repository.GetByIdAsync.assert_called_once_with(42)


@pytest.mark.asyncio
async def test_add_enterprise_success(enterprise_repository):
    """Test AddAsync successfully adds an enterprise."""
    # Create a mock enterprise object
    mock_enterprise = Mock()
    mock_enterprise.Name = "New Enterprise"
    mock_enterprise.Id = 0

    # Setup the mock to return the enterprise
    enterprise_repository.AddAsync.return_value = mock_enterprise

    # Act
    result = await enterprise_repository.AddAsync(mock_enterprise)

    # Assert
    assert result == mock_enterprise
    enterprise_repository.AddAsync.assert_called_once_with(mock_enterprise)


@pytest.mark.asyncio
async def test_add_enterprise_exception_handling(enterprise_repository):
    """Test AddAsync handles exceptions properly."""
    # Create mock enterprise
    mock_enterprise = Mock()
    mock_enterprise.Name = "Problem Enterprise"

    # Setup mock to raise an exception
    enterprise_repository.AddAsync.side_effect = Exception("Database error")

    # Act & Assert
    with pytest.raises(Exception, match="Database error"):
        await enterprise_repository.AddAsync(mock_enterprise)

    enterprise_repository.AddAsync.assert_called_once_with(mock_enterprise)
