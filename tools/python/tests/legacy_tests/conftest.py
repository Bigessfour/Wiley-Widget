"""
Pytest configuration and fixtures for Wiley Widget tests
"""

import gc
import os
import sys
import tempfile
from pathlib import Path

import pytest

# Add project root to path
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

# Import test utilities
from tests.test_memory_leaks import MemoryLeakDetector
from tests.test_resource_exhaustion import ResourceExhaustionTester


# Register custom markers
def pytest_configure(config):
    """Register custom pytest markers"""
    config.addinivalue_line("markers", "memory: Memory-related tests")
    config.addinivalue_line("markers", "stress: Stress/performance tests")
    config.addinivalue_line("markers", "slow: Slow-running tests")
    config.addinivalue_line("markers", "io: I/O related tests")
    config.addinivalue_line("markers", "database: Database-related tests")
    config.addinivalue_line("markers", "concurrency: Concurrency-related tests")


@pytest.fixture
def resource_tester():
    """Fixture providing a ResourceExhaustionTester instance"""
    tester = ResourceExhaustionTester()
    yield tester
    # Cleanup after test
    tester.cleanup()


@pytest.fixture
def memory_detector():
    """Fixture providing a MemoryLeakDetector instance"""
    detector = MemoryLeakDetector()
    yield detector


@pytest.fixture(scope="session", autouse=True)
def setup_test_environment():
    """Setup test environment"""
    # Ensure garbage collection is enabled
    gc.set_threshold(700, 10, 10)

    # Clean up any existing temp files from previous runs
    import glob
    temp_files = glob.glob(os.path.join(tempfile.gettempdir(), "test_*"))
    for temp_file in temp_files:
        try:
            os.unlink(temp_file)
        except OSError:
            pass
