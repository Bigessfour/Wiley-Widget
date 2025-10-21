#!/bin/bash
# Quick test execution script for RegionViewRegistry tests
# Usage: ./test-regionviewregistry.sh [standard|coverage|dev|rebuild]

set -e

MODE=${1:-standard}
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEST_RESULTS="$PROJECT_ROOT/TestResults/RegionViewRegistry"

echo "=== RegionViewRegistry Test Runner (Bash) ==="
echo "Mode: $MODE"
echo "Project Root: $PROJECT_ROOT"
echo ""

# Ensure test results directory exists
mkdir -p "$TEST_RESULTS"

case $MODE in
standard)
	echo "Running tests in standard mode..."
	docker-compose -f docker-compose.regionviewregistry-tests.yml run --rm regionviewregistry-tests
	;;

coverage)
	echo "Running tests with coverage..."
	docker-compose -f docker-compose.regionviewregistry-tests.yml run --rm regionviewregistry-tests

	# Check for coverage files
	if [ -f "$TEST_RESULTS"/**/coverage.cobertura.xml ]; then
		echo "Coverage report generated successfully"
	fi
	;;

dev)
	echo "Starting development mode with live mounting..."
	docker-compose -f docker-compose.regionviewregistry-tests.yml run --rm -it regionviewregistry-tests-dev
	;;

rebuild)
	echo "Rebuilding Docker image..."
	docker-compose -f docker-compose.regionviewregistry-tests.yml build --no-cache regionviewregistry-tests
	docker-compose -f docker-compose.regionviewregistry-tests.yml run --rm regionviewregistry-tests
	;;

clean)
	echo "Cleaning test artifacts..."
	rm -rf "$TEST_RESULTS"/*
	docker-compose -f docker-compose.regionviewregistry-tests.yml down --rmi all --volumes
	echo "Cleanup complete"
	;;

*)
	echo "Usage: $0 {standard|coverage|dev|rebuild|clean}"
	exit 1
	;;
esac

EXIT_CODE=$?

# Display results summary
echo ""
echo "=== Test Execution Summary ==="
if [ $EXIT_CODE -eq 0 ]; then
	echo "✓ Tests completed successfully"
else
	echo "✗ Tests failed with exit code: $EXIT_CODE"
fi

exit $EXIT_CODE
