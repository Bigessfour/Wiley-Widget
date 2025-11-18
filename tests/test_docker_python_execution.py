#!/usr/bin/env python3
"""
Test Docker execution of Python validators with real codebase scanning.
"""
import subprocess
from pathlib import Path


def test_startup_validator_in_docker():
    """Test that startup validator runs successfully in Docker container."""
    repo_root = Path(__file__).parent.parent

    # Build the Python validator image if needed
    build_cmd = [
        "docker",
        "build",
        "-t",
        "wiley-python-validator",
        "-f",
        "docker/Dockerfile.python-tests",
        str(repo_root),
    ]

    print("Building Python validator Docker image...")
    result = subprocess.run(build_cmd, capture_output=True, text=True)
    assert result.returncode == 0, f"Failed to build image: {result.stderr}"

    # Run the validator
    run_cmd = [
        "docker",
        "run",
        "--rm",
        "-v",
        f"{repo_root}:/workspace:ro",
        "-w",
        "/workspace",
        "wiley-python-validator",
        "python",
        "tests/test_startup_validator.py",
    ]

    print("Running startup validator in Docker...")
    result = subprocess.run(run_cmd, capture_output=True, text=True)
    print(f"Exit code: {result.returncode}")
    print(f"Stdout: {result.stdout}")
    print(f"Stderr: {result.stderr}")

    assert result.returncode == 0, f"Validator failed: {result.stderr}"
    assert (
        "validation completed" in result.stdout.lower()
    ), "Validator should complete successfully"


def test_csx_files_validator_in_docker():
    """Test CSX files validator in Docker."""
    repo_root = Path(__file__).parent.parent

    run_cmd = [
        "docker",
        "run",
        "--rm",
        "-v",
        f"{repo_root}:/workspace:ro",
        "-w",
        "/workspace",
        "python:3.10-alpine",
        "sh",
        "-c",
        "pip install -r requirements.txt && python test_csx_files.py",
    ]

    print("Running CSX files validator in Docker...")
    result = subprocess.run(run_cmd, capture_output=True, text=True)
    print(f"Exit code: {result.returncode}")
    print(f"Stdout: {result.stdout}")
    print(f"Stderr: {result.stderr}")

    assert result.returncode == 0, f"CSX validator failed: {result.stderr}"


def test_python_dependencies_installation():
    """Test that Python dependencies install correctly in container."""
    run_cmd = [
        "docker",
        "run",
        "--rm",
        "python:3.10-alpine",
        "sh",
        "-c",
        "pip install --upgrade pip && pip install -r requirements.txt && python -c 'import yaml, requests; print(\"Dependencies installed successfully\")'",
    ]

    print("Testing Python dependencies installation...")
    result = subprocess.run(run_cmd, capture_output=True, text=True)
    print(f"Exit code: {result.returncode}")
    print(f"Stdout: {result.stdout}")
    print(f"Stderr: {result.stderr}")

    assert result.returncode == 0, f"Dependencies installation failed: {result.stderr}"
    assert (
        "Dependencies installed successfully" in result.stdout
    ), "Dependencies should install correctly"


def test_docker_volume_mounting():
    """Test that volume mounting works for codebase access."""
    repo_root = Path(__file__).parent.parent

    # Create a test file to verify mounting
    test_content = "test file for docker volume mounting"
    test_file = repo_root / "docker_test_file.txt"

    try:
        test_file.write_text(test_content)

        run_cmd = [
            "docker",
            "run",
            "--rm",
            "-v",
            f"{repo_root}:/workspace:ro",
            "alpine",
            "cat",
            "/workspace/docker_test_file.txt",
        ]

        print("Testing Docker volume mounting...")
        result = subprocess.run(run_cmd, capture_output=True, text=True)
        print(f"Exit code: {result.returncode}")
        print(f"Stdout: {result.stdout}")
        print(f"Stderr: {result.stderr}")

        assert result.returncode == 0, f"Volume mounting failed: {result.stderr}"
        assert test_content in result.stdout, "Volume should mount correctly"

    finally:
        # Clean up
        if test_file.exists():
            test_file.unlink()


if __name__ == "__main__":
    print("Running Docker Python execution tests...")

    try:
        test_python_dependencies_installation()
        print("✅ Python dependencies test passed")

        test_docker_volume_mounting()
        print("✅ Docker volume mounting test passed")

        test_csx_files_validator_in_docker()
        print("✅ CSX files validator test passed")

        # Note: test_startup_validator_in_docker requires Dockerfile.python-tests
        # Uncomment when that Dockerfile exists
        # test_startup_validator_in_docker()
        # print("✅ Startup validator test passed")

        print("All Docker Python execution tests passed! 🎉")

    except Exception as e:
        print(f"❌ Test failed: {e}")
        exit(1)
