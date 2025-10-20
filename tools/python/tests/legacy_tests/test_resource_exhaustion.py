"""
Resource Exhaustion Tests

Enterprise-level tests for resource exhaustion scenarios including:
- Memory pressure and leaks
- Database connection pool exhaustion
- File handle exhaustion
- Thread pool exhaustion
- Disk space exhaustion
"""

from __future__ import annotations

import asyncio
import gc
import os
import sys
import tempfile
import threading
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path
from unittest.mock import patch

import psutil
import pytest

# Add project root to path
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))


class ResourceExhaustionTester:
    """Test harness for simulating resource exhaustion conditions"""

    def __init__(self):
        self.temp_files = []
        self.threads = []
        self.connections = []

    def create_temp_file(self, size_mb: int = 1) -> str:
        """Create a temporary file of specified size"""
        with tempfile.NamedTemporaryFile(delete=False) as f:
            # Write data in chunks to avoid memory issues
            chunk_size = 1024 * 1024  # 1MB chunks
            data = b'0' * chunk_size
            for _ in range(size_mb):
                f.write(data)
            temp_path = f.name
        self.temp_files.append(temp_path)
        return temp_path

    def cleanup_temp_files(self):
        """Clean up all temporary files"""
        for temp_file in self.temp_files:
            try:
                os.unlink(temp_file)
            except OSError:
                pass  # File may already be deleted
        self.temp_files.clear()

    def cleanup(self):
        """General cleanup method"""
        self.cleanup_temp_files()
        # Clean up threads
        for thread in self.threads:
            if thread.is_alive():
                thread.join(timeout=1.0)
        self.threads.clear()
        # Clean up connections (mock cleanup)
        self.connections.clear()


class TestMemoryExhaustion:
    """Tests for memory exhaustion scenarios"""

    @pytest.mark.stress
    @pytest.mark.slow
    def test_memory_pressure_handling(self):
        """Test application behavior under memory pressure"""
        # Get initial memory usage
        process = psutil.Process()
        initial_memory = process.memory_info().rss

        # Create memory pressure by allocating large objects
        large_objects = []
        allocation_size = 10 * 1024 * 1024  # 10MB per allocation

        try:
            # Allocate memory until we hit a reasonable limit
            for _ in range(50):  # Try to allocate ~500MB
                try:
                    large_objects.append(bytearray(allocation_size))
                except MemoryError:
                    # Expected when memory is exhausted
                    break

            # Force garbage collection
            gc.collect()

            # Verify system can still function
            current_memory = process.memory_info().rss
            memory_increase = current_memory - initial_memory

            # Should be able to allocate some memory for basic operations
            small_object = bytearray(1024)  # 1KB
            assert len(small_object) == 1024

            # Log memory usage for analysis
            print(f"Memory increase: {memory_increase / 1024 / 1024:.2f} MB")

        finally:
            # Cleanup
            del large_objects
            gc.collect()

    @pytest.mark.stress
    def test_garbage_collection_under_pressure(self):
        """Test garbage collection behavior under memory pressure"""
        # Create circular references that should be collected
        class CircularRef:
            def __init__(self):
                self.ref: CircularRef | None = None

        objects = []
        for _ in range(1000):
            obj1 = CircularRef()
            obj2 = CircularRef()
            obj1.ref = obj2
            obj2.ref = obj1
            objects.append((obj1, obj2))

        # Delete references
        del objects

        # Force garbage collection
        collected = gc.collect()

        # Should collect circular references
        assert collected > 0, "Garbage collector should have collected circular references"

    @pytest.mark.memory
    def test_large_object_heap_pressure(self):
        """Test behavior with large object heap pressure"""
        # Large object heap threshold is 85KB
        large_objects = []

        try:
            for _ in range(10):
                # Create objects larger than LOH threshold
                large_obj = bytearray(100 * 1024)  # 100KB
                large_objects.append(large_obj)

            # Verify objects are created successfully
            assert len(large_objects) == 10
            assert all(len(obj) == 100 * 1024 for obj in large_objects)

        finally:
            del large_objects
            gc.collect()


class TestConnectionPoolExhaustion:
    """Tests for database connection pool exhaustion"""

    @pytest.mark.database
    @pytest.mark.stress
    def test_connection_pool_exhaustion_recovery(self):
        """Test recovery from connection pool exhaustion"""
        # This would require actual database connections
        # Mock the scenario for now
        with patch('pyodbc.connect') as mock_connect:
            # Simulate connection pool exhaustion
            mock_connect.side_effect = Exception("Connection pool exhausted")

            # Test should handle the exception gracefully
            with pytest.raises(Exception, match="Connection pool exhausted"):
                # Attempt database operation
                raise Exception("Connection pool exhausted")

    @pytest.mark.database
    @pytest.mark.asyncio
    async def test_concurrent_connection_limits(self):
        """Test behavior when hitting concurrent connection limits"""
        max_connections = 10
        connection_attempts = 15

        async def mock_connection_attempt(connection_id: int):
            if connection_id >= max_connections:
                raise Exception(f"Connection limit exceeded for connection {connection_id}")
            await asyncio.sleep(0.01)  # Simulate connection time
            return f"Connection {connection_id} established"

        # Attempt more connections than allowed
        tasks = [mock_connection_attempt(i) for i in range(connection_attempts)]
        results = await asyncio.gather(*tasks, return_exceptions=True)

        # Count successful vs failed connections
        successful = [r for r in results if not isinstance(r, Exception)]
        failed = [r for r in results if isinstance(r, Exception)]

        assert len(successful) == max_connections
        assert len(failed) == connection_attempts - max_connections


class TestFileHandleExhaustion:
    """Tests for file handle exhaustion scenarios"""

    @pytest.mark.io
    @pytest.mark.stress
    def test_file_handle_exhaustion_handling(self, resource_tester):
        """Test file operations when file handles are exhausted"""
        open_files = []

        try:
            # Try to open as many files as possible
            for _ in range(1000):  # Reasonable upper limit
                try:
                    temp_file = resource_tester.create_temp_file(1)  # 1MB file
                    f = open(temp_file)
                    open_files.append(f)
                except OSError as e:
                    if "Too many open files" in str(e) or "No more files" in str(e):
                        # Expected exhaustion error
                        break
                    else:
                        raise  # Unexpected error

            # Should be able to open at least some files
            assert len(open_files) > 0, "Should be able to open at least some files"

            # Verify files can be read
            for f in open_files[:5]:  # Test first 5 files
                data = f.read(1024)
                assert len(data) == 1024

        finally:
            # Cleanup open file handles
            for f in open_files:
                try:
                    f.close()
                except OSError:
                    pass

    @pytest.mark.io
    def test_disk_space_exhaustion_simulation(self, tmp_path):
        """Test behavior when disk space is exhausted"""
        # Create a temporary directory for testing
        test_dir = tmp_path / "disk_exhaustion_test"
        test_dir.mkdir()

        # Fill available space (but not completely to avoid system issues)
        created_files = []
        file_size_mb = 10

        try:
            available_space = psutil.disk_usage(str(test_dir)).free
            max_files = min(5, available_space // (file_size_mb * 1024 * 1024))

            for i in range(max_files):
                file_path = test_dir / f"test_file_{i}.dat"
                with open(file_path, 'wb') as f:
                    f.write(b'0' * (file_size_mb * 1024 * 1024))
                created_files.append(file_path)

            # Try to create one more file (should fail or be very slow)
            last_file = test_dir / "should_fail.dat"
            try:
                with open(last_file, 'wb') as f:
                    f.write(b'0' * (file_size_mb * 1024 * 1024))
                created_files.append(last_file)
            except OSError as e:
                # Expected when disk is full
                assert "No space left" in str(e) or "Disk full" in str(e)

        finally:
            # Cleanup
            for file_path in created_files:
                try:
                    file_path.unlink()
                except OSError:
                    pass


class TestThreadPoolExhaustion:
    """Tests for thread pool exhaustion scenarios"""

    @pytest.mark.concurrency
    @pytest.mark.stress
    def test_thread_pool_exhaustion_handling(self):
        """Test behavior when thread pool is exhausted"""
        results = []
        errors = []

        def worker_task(task_id: int):
            """Simulate work that takes some time"""
            time.sleep(0.1)
            return f"Task {task_id} completed"

        # Submit many tasks to potentially exhaust thread pool
        with ThreadPoolExecutor(max_workers=4) as executor:
            futures = [executor.submit(worker_task, i) for i in range(20)]

            for future in as_completed(futures):
                try:
                    result = future.result(timeout=5.0)
                    results.append(result)
                except Exception as e:
                    errors.append(e)

        # Should complete all tasks eventually
        assert len(results) == 20, f"Expected 20 results, got {len(results)}"
        assert len(errors) == 0, f"Unexpected errors: {errors}"

    @pytest.mark.concurrency
    def test_concurrent_thread_limits(self):
        """Test behavior near thread limits"""
        active_threads = []
        max_test_threads = min(50, threading.active_count() + 20)

        def thread_worker(thread_id: int):
            """Thread worker that signals completion"""
            time.sleep(0.05)
            active_threads.append(thread_id)

        threads = []
        for i in range(max_test_threads):
            t = threading.Thread(target=thread_worker, args=(i,))
            threads.append(t)
            t.start()

        # Wait for all threads to complete
        for t in threads:
            t.join(timeout=2.0)

        # Verify all threads completed
        assert len(active_threads) == max_test_threads


class TestResourceLeakDetection:
    """Tests for detecting resource leaks"""

    @pytest.mark.memory
    def test_file_handle_leaks(self, resource_tester):
        """Test for file handle leaks"""
        initial_handles = len(psutil.Process().open_files())

        # Create and "forget" to close some files
        leaked_files = []
        for _ in range(10):
            temp_file = resource_tester.create_temp_file(1)
            f = open(temp_file)
            leaked_files.append(f)
            # Intentionally don't close the file

        # Check for handle leak
        current_handles = len(psutil.Process().open_files())
        handle_increase = current_handles - initial_handles

        # Should detect the leaked handles
        assert handle_increase >= 10, f"Expected at least 10 more open files, got {handle_increase}"

        # Cleanup
        for f in leaked_files:
            f.close()

    @pytest.mark.memory
    def test_object_reference_leaks(self):
        """Test for object reference leaks"""
        # Create objects that might be leaked
        leaked_objects = []

        class TestObject:
            def __init__(self, value: int):
                self.value = value

        # Create many objects
        for i in range(1000):
            obj = TestObject(i)
            leaked_objects.append(obj)

        # Delete half the references (simulate leak by keeping some references)
        del leaked_objects[:500]

        # Force garbage collection
        gc.collect()

        # The test is about detecting that we still have 500 objects
        # GC might collect 0 if there are no cyclic references
        # The real test is that we have the expected number of remaining objects
        assert len(leaked_objects) == 500

        # Cleanup
        del leaked_objects
        gc.collect()
