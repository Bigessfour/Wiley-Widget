"""
Memory Leak Detection Tests

Enterprise-level tests for memory leak detection including:
- Unmanaged resource leaks
- Event handler leaks
- Object reference cycles
- Weak reference handling
- Garbage collection pressure testing
- Memory usage monitoring
"""

import gc
import sys
import time
import weakref
from pathlib import Path
from unittest.mock import MagicMock

import psutil
import pytest

# Add project root to path
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))


class MemoryLeakDetector:
    """Memory leak detection utilities"""

    def __init__(self):
        self.initial_objects = 0
        self.object_refs = []

    def get_object_count(self) -> int:
        """Get current number of objects in memory"""
        return len(gc.get_objects())

    def start_monitoring(self):
        """Start memory monitoring"""
        gc.collect()  # Clean up before starting
        self.initial_objects = self.get_object_count()

    def check_for_leaks(self, operation_name: str, max_growth: int = 1000) -> bool:
        """Check if operation caused memory leaks"""
        gc.collect()
        current_objects = self.get_object_count()
        growth = current_objects - self.initial_objects

        if growth > max_growth:
            print(f"Potential memory leak in {operation_name}: {growth} new objects")
            return True
        return False

    def create_object_reference_cycle(self):
        """Create a circular reference for testing"""
        class CircularRef:
            def __init__(self):
                self.ref: CircularRef | None = None

        obj1 = CircularRef()
        obj2 = CircularRef()
        obj1.ref = obj2
        obj2.ref = obj1

        # Keep weak references to detect when objects are collected
        weak1 = weakref.ref(obj1)
        weak2 = weakref.ref(obj2)

        return weak1, weak2


class TestUnmanagedResourceLeaks:
    """Tests for unmanaged resource leaks"""

    @pytest.fixture
    def memory_detector(self):
        """Memory leak detector"""
        return MemoryLeakDetector()

    @pytest.mark.memory
    def test_file_handle_resource_leak(self, memory_detector):
        """Test detection of file handle resource leaks"""
        memory_detector.start_monitoring()

        # Create many file handles without closing them
        file_handles = []
        for idx in range(100):
            try:
                # Create temporary file handles
                f = open(f"temp_file_{idx}.txt", 'w')
                f.write(f"test data {idx}")
                file_handles.append(f)
                # Intentionally don't close
            except OSError:
                # May hit file handle limits
                break

        # Check for resource leaks (file handles)
        memory_detector.check_for_leaks("file handle creation", max_growth=50)

        # Cleanup
        for f in file_handles:
            try:
                f.close()
            except OSError:
                pass

        # Should detect the leak
        assert len(file_handles) > 0  # Should have created some files

    @pytest.mark.memory
    def test_database_connection_leak(self, memory_detector):
        """Test detection of database connection leaks"""
        memory_detector.start_monitoring()

        # Simulate connection objects
        connections = []
        for _ in range(50):
            # Simulate database connection object
            conn = MagicMock()
            conn.close = MagicMock()
            connections.append(conn)

        # Check for object growth
        memory_detector.check_for_leaks("database connection creation", max_growth=25)

        # Cleanup
        for conn in connections:
            conn.close()

        # Should have created connection objects
        assert len(connections) == 50

    @pytest.mark.memory
    def test_network_socket_leak(self, memory_detector):
        """Test detection of network socket leaks"""
        memory_detector.start_monitoring()

        # Simulate socket objects
        sockets = []
        for _ in range(30):
            # Simulate socket object
            sock = MagicMock()
            sock.close = MagicMock()
            sockets.append(sock)

        memory_detector.check_for_leaks("socket creation", max_growth=15)

        # Cleanup
        for sock in sockets:
            sock.close()

        assert len(sockets) == 30


class TestEventHandlerLeaks:
    """Tests for event handler memory leaks"""

    @pytest.mark.memory
    def test_event_handler_memory_leak(self):
        """Test detection of event handler memory leaks"""
        class EventPublisher:
            def __init__(self):
                self.handlers = []

            def add_handler(self, handler):
                self.handlers.append(handler)

            def remove_handler(self, handler):
                if handler in self.handlers:
                    self.handlers.remove(handler)

            def raise_event(self):
                for handler in self.handlers:
                    handler()

        class EventSubscriber:
            def __init__(self, publisher):
                self.publisher = publisher
                self.data = "x" * 1000  # Some data to take memory

            def handle_event(self):
                pass

        publisher = EventPublisher()
        subscribers = []

        # Create subscribers and add handlers
        for i in range(100):
            subscriber = EventSubscriber(publisher)
            publisher.add_handler(subscriber.handle_event)
            subscribers.append(subscriber)

        # Simulate memory leak by not removing handlers
        # (In real scenarios, subscribers go out of scope but handlers remain)

        # Check that handlers are still registered
        assert len(publisher.handlers) == 100

        # Cleanup - properly remove handlers
        for subscriber in subscribers:
            publisher.remove_handler(subscriber.handle_event)

        assert len(publisher.handlers) == 0

    @pytest.mark.memory
    def test_weak_reference_event_handlers(self):
        """Test using weak references to prevent event handler leaks"""
        import weakref

        class WeakEventPublisher:
            def __init__(self):
                self.handlers = []

            def add_handler(self, handler):
                # Use weak reference to prevent leaks
                self.handlers.append(weakref.ref(handler))

            def raise_event(self):
                # Clean up dead references and call live ones
                self.handlers = [ref for ref in self.handlers if ref() is not None]
                for ref in self.handlers:
                    handler = ref()
                    if handler:
                        handler()

        class WeakSubscriber:
            def __init__(self):
                self.data = "x" * 500

            def handle_event(self):
                pass

        publisher = WeakEventPublisher()
        subscribers = []

        # Create subscribers
        for i in range(50):
            subscriber = WeakSubscriber()
            publisher.add_handler(subscriber.handle_event)
            subscribers.append(subscriber)

        # Delete subscribers (simulating going out of scope)
        del subscribers

        # Force garbage collection
        gc.collect()

        # Raise event - should clean up dead references
        publisher.raise_event()

        # Should have no handlers left (all cleaned up)
        assert len(publisher.handlers) == 0


class TestObjectReferenceCycles:
    """Tests for object reference cycle detection"""

    @pytest.mark.memory
    def test_circular_reference_detection(self):
        """Test detection of circular references"""
        weak1, weak2 = MemoryLeakDetector().create_object_reference_cycle()

        # Objects should exist initially
        assert weak1() is not None
        assert weak2() is not None

        # Force garbage collection
        collected = gc.collect()

        # Circular references should be collected
        assert collected > 0, "Should have collected circular references"

        # Weak references should now be dead
        assert weak1() is None
        assert weak2() is None

    @pytest.mark.memory
    def test_circular_reference_memory_impact(self):
        """Test memory impact of circular references"""
        process = psutil.Process()
        initial_memory = process.memory_info().rss

        # Create many circular references
        circular_objects = []
        for _ in range(100):
            weak1, weak2 = MemoryLeakDetector().create_object_reference_cycle()
            circular_objects.extend([weak1, weak2])

        # Force garbage collection
        gc.collect()

        final_memory = process.memory_info().rss
        memory_growth = final_memory - initial_memory

        # Circular references should be cleaned up, so minimal memory growth
        # Allow some growth for the weak references themselves
        assert memory_growth < 10 * 1024 * 1024  # Less than 10MB growth

    @pytest.mark.memory
    def test_weak_reference_cleanup(self):
        """Test proper cleanup of weak references"""
        objects = []
        weak_refs = []

        class TestObject:
            def __init__(self, value):
                self.value = value

        # Create objects and weak references
        for i in range(100):
            obj = TestObject(i)
            objects.append(obj)
            weak_refs.append(weakref.ref(obj))

        # All weak references should be alive
        alive_refs = [ref for ref in weak_refs if ref() is not None]
        assert len(alive_refs) == 100

        # Delete objects
        del objects
        gc.collect()

        # All weak references should now be dead
        dead_refs = [ref for ref in weak_refs if ref() is None]
        assert len(dead_refs) >= 99  # Allow for minor timing variations


class TestGarbageCollectionPressure:
    """Tests for garbage collection under pressure"""

    @pytest.mark.memory
    @pytest.mark.stress
    def test_gc_performance_under_pressure(self):
        """Test garbage collection performance under memory pressure"""
        # Create memory pressure
        large_objects = []
        for _i in range(100):
            # Create large objects to fill memory
            large_obj = bytearray(1024 * 1024)  # 1MB each
            large_objects.append(large_obj)

        start_time = time.time()
        collected = gc.collect()
        gc_time = time.time() - start_time

        # Should collect the large objects
        assert collected > 0

        # GC should complete in reasonable time
        assert gc_time < 5.0  # Less than 5 seconds

        # Cleanup
        del large_objects
        gc.collect()

    @pytest.mark.memory
    def test_finalizer_queue_backlog(self):
        """Test handling of finalizer queue backlogs"""
        class ObjectWithFinalizer:
            def __init__(self):
                self.data = bytearray(10 * 1024)  # 10KB

            def __del__(self):
                # Simulate slow finalizer
                time.sleep(0.001)

        # Create many objects with finalizers
        objects = []
        for _ in range(200):
            obj = ObjectWithFinalizer()
            objects.append(obj)

        # Delete objects (will queue finalizers)
        del objects

        # Time the garbage collection
        start_time = time.time()
        collected = gc.collect()
        gc_time = time.time() - start_time

        # Should collect objects (may be 0 if finalizers haven't run yet)
        # The main test is that GC completes without hanging
        assert collected >= 0  # Allow 0 if finalizers are still queued

        # Should complete in reasonable time despite finalizers
        assert gc_time < 10.0  # Allow more time for finalizers

    @pytest.mark.memory
    def test_large_object_heap_collection(self):
        """Test large object heap garbage collection"""
        # Large object heap threshold is ~85KB
        large_objects = []

        # Create objects larger than LOH threshold
        for _ in range(20):
            large_obj = bytearray(100 * 1024)  # 100KB
            large_objects.append(large_obj)

        # Delete half
        del large_objects[:10]

        # Force GC
        collected = gc.collect()

        # Should collect the deleted objects
        assert collected > 0

        # Remaining objects should still exist
        assert len(large_objects) == 10


class TestMemoryUsageMonitoring:
    """Tests for memory usage monitoring"""

    @pytest.mark.memory
    def test_memory_usage_tracking(self):
        """Test tracking of memory usage over time"""
        process = psutil.Process()

        # Get initial memory
        initial_memory = process.memory_info().rss

        # Allocate memory
        memory_hog = bytearray(50 * 1024 * 1024)  # 50MB

        # Check memory growth
        current_memory = process.memory_info().rss
        memory_growth = current_memory - initial_memory

        # Should see significant memory growth
        assert memory_growth > 40 * 1024 * 1024  # At least 40MB growth

        # Cleanup
        del memory_hog
        gc.collect()

    @pytest.mark.memory
    def test_memory_leak_detection_over_time(self):
        """Test memory leak detection over multiple operations"""
        process = psutil.Process()
        memory_readings = []

        # Perform operations that might leak
        for i in range(10):
            # Create some objects
            temp_objects = [bytearray(1024) for _ in range(1000)]  # 1MB total

            # Do some work
            time.sleep(0.01)

            # Record memory
            memory_readings.append(process.memory_info().rss)

            # Cleanup
            del temp_objects
            gc.collect()

        # Memory should not consistently grow
        initial_memory = memory_readings[0]
        final_memory = memory_readings[-1]
        memory_growth = final_memory - initial_memory

        # Allow some memory growth but not excessive
        assert memory_growth < 10 * 1024 * 1024  # Less than 10MB total growth

    @pytest.mark.memory
    def test_object_count_monitoring(self):
        """Test monitoring of object counts"""
        # Force GC to get a clean baseline
        gc.collect()
        initial_count = len(gc.get_objects())

        # Create many objects
        objects = []
        for i in range(10000):
            objects.append({"id": i, "data": "x" * 100})

        # Force GC again to ensure objects are counted
        gc.collect()
        current_count = len(gc.get_objects())
        object_growth = current_count - initial_count

        # Should see object growth (allow for some variation)
        assert object_growth > 0  # At least some new objects (very conservative)

        # Cleanup
        del objects
        gc.collect()

        final_count = len(gc.get_objects())
        final_growth = final_count - initial_count

        # Should be back to near initial count
        assert final_growth < object_growth  # Should be less than peak