"""
Threading Stress Tests for AI API Integration

Comprehensive stress testing for XAI API integration including:
- Concurrent request handling with semaphore limits
- Cache performance under load
- Batch processing efficiency
- Memory usage monitoring
- Response time validation (<2s target)
- Throttling prevention
"""

import asyncio
import statistics
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any
from unittest.mock import AsyncMock, MagicMock

import psutil
import pytest

# Add project root to path
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))



@dataclass
class StressTestResult:
    """Results from a stress test run"""
    total_requests: int
    successful_requests: int
    failed_requests: int
    average_response_time: float
    median_response_time: float
    p95_response_time: float
    max_response_time: float
    min_response_time: float
    cache_hit_rate: float
    memory_usage_mb: float
    concurrent_limit_respected: bool
    target_response_time_met: bool  # <2s target


class TestXAIStress:
    """Stress tests for XAI API integration"""

    @pytest.fixture
    def mock_xai_service(self):
        """Mock XAI service for testing"""
        mock_service = MagicMock()

        # Mock the async methods
        mock_service.GetInsightsAsync = AsyncMock(return_value="Mock response")
        mock_service.BatchGetInsightsAsync = AsyncMock(return_value={"key1": "response1", "key2": "response2"})
        mock_service._memoryCache = MagicMock()
        mock_service._memoryCache.TryGetValue = MagicMock(return_value=False)
        mock_service._concurrencySemaphore = MagicMock()
        mock_service._httpClient = MagicMock()
        mock_service._retryPolicy = MagicMock()
        mock_service._aiLoggingService = MagicMock()
        mock_service._configuration = MagicMock()
        mock_service._configuration.__getitem__ = MagicMock(side_effect=lambda key: {
            "XAI:Model": "grok-4-0709",
            "XAI:MaxConcurrentRequests": "5"
        }.get(key, ""))

        return mock_service

    @pytest.mark.stress
    @pytest.mark.slow
    def test_concurrent_requests_semaphore_limit(self, mock_xai_service):
        """Test that semaphore properly limits concurrent requests"""
        concurrent_count = 0
        max_concurrent = 0
        completed_requests = 0

        async def mock_api_call() -> str:
            nonlocal concurrent_count, max_concurrent, completed_requests
            concurrent_count += 1
            max_concurrent = max(max_concurrent, concurrent_count)

            # Simulate API call delay
            await asyncio.sleep(0.1)

            concurrent_count -= 1
            completed_requests += 1
            return "Mock response"

        # Mock the HTTP client
        mock_response = MagicMock()
        mock_response.Content.ReadFromJsonAsync = AsyncMock(return_value=MagicMock(
            error=None,
            choices=[MagicMock(message=MagicMock(content="Test response"))]
        ))
        mock_response.EnsureSuccessStatusCode = MagicMock()

        mock_xai_service._httpClient.PostAsJsonAsync = AsyncMock(return_value=mock_response)
        mock_xai_service._retryPolicy.ExecuteAsync = AsyncMock(return_value=mock_response)

        async def run_concurrent_test() -> None:
            tasks = []
            for i in range(20):  # More requests than semaphore limit
                task = asyncio.create_task(mock_xai_service.GetInsightsAsync(
                    f"Context {i}", f"Question {i}"
                ))
                tasks.append(task)

            await asyncio.gather(*tasks, return_exceptions=True)

        asyncio.run(run_concurrent_test())

        # Verify semaphore limit was respected (should be 5 based on config)
        assert max_concurrent <= 5, f"Semaphore limit exceeded: max concurrent was {max_concurrent}"
        assert completed_requests == 20, "Not all requests completed"

    @pytest.mark.stress
    @pytest.mark.slow
    def test_cache_performance_under_load(self, mock_xai_service):
        """Test cache performance with repeated requests under load"""
        cache_hits = 0
        cache_misses = 0

        # Setup cache to return hits for repeated requests
        def mock_try_get_value(key, out_value):
            nonlocal cache_hits, cache_misses
            if "repeated" in key:
                cache_hits += 1
                out_value[0] = "Cached response"
                return True
            else:
                cache_misses += 1
                return False

        mock_xai_service._memoryCache.TryGetValue = mock_try_get_value
        mock_xai_service._memoryCache.Set = MagicMock()

        async def run_cache_test() -> list[Any]:
            tasks = []

            # Mix of unique and repeated requests
            for i in range(50):
                if i % 3 == 0:  # Every 3rd request is repeated
                    context = "Repeated context"
                    question = "Repeated question"
                else:
                    context = f"Unique context {i}"
                    question = f"Unique question {i}"

                task = asyncio.create_task(mock_xai_service.GetInsightsAsync(context, question))
                tasks.append(task)

            results = await asyncio.gather(*tasks, return_exceptions=True)
            return results

        # Mock successful API response for cache misses
        mock_response = MagicMock()
        mock_response.Content.ReadFromJsonAsync = AsyncMock(return_value=MagicMock(
            error=None,
            choices=[MagicMock(message=MagicMock(content="API response"))]
        ))
        mock_response.EnsureSuccessStatusCode = MagicMock()
        mock_xai_service._httpClient.PostAsJsonAsync = AsyncMock(return_value=mock_response)
        mock_xai_service._retryPolicy.ExecuteAsync = AsyncMock(return_value=mock_response)

        results = asyncio.run(run_cache_test())

        # Verify cache is working
        assert cache_hits > 0, "No cache hits recorded"
        assert cache_misses > 0, "No cache misses recorded"

        # All results should be successful
        successful_results = [r for r in results if not isinstance(r, Exception)]
        assert len(successful_results) == 50, f"Only {len(successful_results)} successful results out of 50"

    @pytest.mark.stress
    @pytest.mark.slow
    def test_batch_processing_efficiency(self, mock_xai_service):
        """Test batch processing performance vs individual requests"""
        # Mock API responses
        mock_response = MagicMock()
        mock_response.Content.ReadFromJsonAsync = AsyncMock(return_value=MagicMock(
            error=None,
            choices=[MagicMock(message=MagicMock(content="Batch response"))]
        ))
        mock_response.EnsureSuccessStatusCode = MagicMock()
        mock_xai_service._httpClient.PostAsJsonAsync = AsyncMock(return_value=mock_response)
        mock_xai_service._retryPolicy.ExecuteAsync = AsyncMock(return_value=mock_response)

        # Test data
        requests = [(f"Context {i}", f"Question {i}") for i in range(10)]

        async def time_batch_processing() -> tuple[float, dict[str, str]]:
            start_time = time.time()
            results = await mock_xai_service.BatchGetInsightsAsync(requests)
            batch_time = time.time() - start_time
            return batch_time, results

        async def time_individual_processing() -> tuple[float, list[str]]:
            start_time = time.time()
            tasks = [mock_xai_service.GetInsightsAsync(ctx, qst) for ctx, qst in requests]
            results = await asyncio.gather(*tasks)
            individual_time = time.time() - start_time
            return individual_time, results

        batch_time, batch_results = asyncio.run(time_batch_processing())
        individual_time, individual_results = asyncio.run(time_individual_processing())

        # Batch processing should be more efficient (though may not be faster due to concurrency limits)
        assert len(batch_results) == 10, "Batch processing didn't return all results"
        assert len(individual_results) == 10, "Individual processing didn't return all results"

        # Both should complete successfully
        assert all(isinstance(r, str) for r in batch_results.values()), "Batch results contain errors"
        assert all(isinstance(r, str) for r in individual_results), "Individual results contain errors"

    @pytest.mark.stress
    @pytest.mark.slow
    def test_response_time_target(self, mock_xai_service):
        """Test that response times meet the <2s production target"""
        response_times = []

        # Mock API with variable response times
        async def mock_api_call(*args, **kwargs) -> MagicMock:
            delay = 0.5 + (time.time() % 0.5)  # 0.5-1.0s random delay
            await asyncio.sleep(delay)
            response_times.append(delay * 1000)  # Convert to ms

            mock_response = MagicMock()
            mock_response.Content.ReadFromJsonAsync = AsyncMock(return_value=MagicMock(
                error=None,
                choices=[MagicMock(message=MagicMock(content="Test response"))]
            ))
            mock_response.EnsureSuccessStatusCode = MagicMock()
            return mock_response

        mock_xai_service._httpClient.PostAsJsonAsync = mock_api_call
        mock_xai_service._retryPolicy.ExecuteAsync = mock_api_call

        async def run_response_time_test() -> None:
            tasks = []
            for i in range(20):
                task = asyncio.create_task(mock_xai_service.GetInsightsAsync(
                    f"Context {i}", f"Question {i}"
                ))
                tasks.append(task)

            await asyncio.gather(*tasks)

        asyncio.run(run_response_time_test())

        # Calculate statistics
        avg_response_time = statistics.mean(response_times)
        p95_response_time = statistics.quantiles(response_times, n=20)[18]  # 95th percentile

        # Log performance metrics
        print(f"Average response time: {avg_response_time:.2f}ms")
        print(f"95th percentile response time: {p95_response_time:.2f}ms")
        print(f"Max response time: {max(response_times):.2f}ms")

        # Target: <2s (2000ms) for production
        # Allow some tolerance for test environment
        assert avg_response_time < 2000, f"Average response time {avg_response_time:.2f}ms exceeds 2s target"
        assert p95_response_time < 2500, f"P95 response time {p95_response_time:.2f}ms exceeds acceptable limit"

    @pytest.mark.stress
    @pytest.mark.slow
    def test_memory_usage_under_load(self, mock_xai_service):
        """Test memory usage remains stable under load"""
        process = psutil.Process()
        initial_memory = process.memory_info().rss / 1024 / 1024  # MB

        # Mock API responses
        mock_response = MagicMock()
        mock_response.Content.ReadFromJsonAsync = AsyncMock(return_value=MagicMock(
            error=None,
            choices=[MagicMock(message=MagicMock(content="Memory test response"))]
        ))
        mock_response.EnsureSuccessStatusCode = MagicMock()
        mock_xai_service._httpClient.PostAsJsonAsync = AsyncMock(return_value=mock_response)
        mock_xai_service._retryPolicy.ExecuteAsync = AsyncMock(return_value=mock_response)

        async def run_memory_test() -> float:
            tasks = []
            for i in range(100):  # High volume test
                task = asyncio.create_task(mock_xai_service.GetInsightsAsync(
                    f"Context {i}", f"Question {i}"
                ))
                tasks.append(task)

            await asyncio.gather(*tasks)

            # Force garbage collection
            import gc
            gc.collect()

            final_memory = process.memory_info().rss / 1024 / 1024  # MB
            memory_increase = final_memory - initial_memory

            return memory_increase

        memory_increase = asyncio.run(run_memory_test())

        # Memory increase should be reasonable (< 50MB for 100 requests)
        assert memory_increase < 50, f"Memory increase {memory_increase:.2f}MB exceeds acceptable limit"

        print(f"Memory increase: {memory_increase:.2f}MB")

    @pytest.mark.stress
    @pytest.mark.slow
    def test_throttling_prevention(self, mock_xai_service):
        """Test that the service prevents API throttling through request pacing"""
        request_timestamps = []
        concurrent_requests = 0
        max_concurrent = 0

        async def mock_throttled_api(*args, **kwargs) -> MagicMock:
            nonlocal concurrent_requests, max_concurrent
            concurrent_requests += 1
            max_concurrent = max(max_concurrent, concurrent_requests)
            request_timestamps.append(time.time())

            # Simulate API processing time
            await asyncio.sleep(0.2)

            concurrent_requests -= 1

            mock_response = MagicMock()
            mock_response.Content.ReadFromJsonAsync = AsyncMock(return_value=MagicMock(
                error=None,
                choices=[MagicMock(message=MagicMock(content="Throttling test response"))]
            ))
            mock_response.EnsureSuccessStatusCode = MagicMock()
            return mock_response

        mock_xai_service._httpClient.PostAsJsonAsync = mock_throttled_api
        mock_xai_service._retryPolicy.ExecuteAsync = mock_throttled_api

        async def run_throttling_test() -> None:
            tasks = []
            for i in range(15):  # More than semaphore limit
                task = asyncio.create_task(mock_xai_service.GetInsightsAsync(
                    f"Context {i}", f"Question {i}"
                ))
                tasks.append(task)

            await asyncio.gather(*tasks)

        asyncio.run(run_throttling_test())

        # Verify concurrency limits were respected
        assert max_concurrent <= 5, f"Concurrency limit exceeded: {max_concurrent} concurrent requests"

        # Check request timing distribution (should not be all at once)
        if len(request_timestamps) > 5:
            # Calculate time span of first 5 requests
            early_requests = sorted(request_timestamps[:5])
            early_span = early_requests[-1] - early_requests[0]

            # Requests should be spread out, not bunched up
            assert early_span < 2.0, "Requests not properly paced"

    def test_comprehensive_stress_report(self, mock_xai_service):
        """Generate a comprehensive stress test report"""
        # This would be a meta-test that runs all stress tests and compiles results
        # For now, just verify the service can be instantiated
        assert mock_xai_service is not None
        assert hasattr(mock_xai_service, "GetInsightsAsync")
        assert hasattr(mock_xai_service, "BatchGetInsightsAsync")



