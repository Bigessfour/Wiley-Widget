import json
import re
from typing import Dict, List, Set

import pytest

# Assume the manifest file is loaded from 'ai-fetchable-manifest.json'
MANIFEST_FILE = "ai-fetchable-manifest.json"


def load_manifest() -> Dict:
    with open(MANIFEST_FILE, "r", encoding="utf-8") as f:
        return json.load(f)


def fetch_file_content(file_path: str) -> str:
    try:
        with open(file_path, "r", encoding="utf-8") as f:
            return f.read()
    except FileNotFoundError:
        return ""  # Return empty string if file not found


def extract_interfaces_from_code(code: str) -> Set[str]:
    interfaces = set()
    # Regex to find interface declarations, e.g., public interface IAuditService
    pattern = r"interface\s+(\w+)"
    matches = re.findall(pattern, code)
    for iface in matches:
        if iface.startswith("I"):
            interfaces.add(iface)
    return interfaces


def find_di_registration_files(manifest: Dict) -> List[Dict]:
    di_files = []
    for file in manifest.get("files", []):
        path = file["metadata"]["path"].lower()
        if (
            "bootstrapper" in path
            or "module" in path
            or "configuration" in path
            or "app.xaml" in path
        ):
            if (
                file["context"]["category"] == "source_code"
                and file["metadata"]["language"] == "C#"
            ):
                di_files.append(file)
    return di_files


def extract_registrations_from_code(code: str) -> Set[str]:
    registrations = set()
    # Regex to find container.Register* calls, e.g., container.Register<IAuditService, AuditService>();
    # Also matches RegisterSingleton, RegisterInstance, etc.
    pattern = r"\.Register\w*\s*<\s*([^,>]+)\s*,"
    matches = re.findall(pattern, code)
    for full_type in matches:
        # Extract just the interface name (last part after dot)
        interface_name = full_type.split(".")[-1]
        if interface_name.startswith("I"):
            registrations.add(interface_name)
    # Also handle Register(typeof(IAuditService), typeof(AuditService))
    pattern_typeof = r"\.Register\w*\s*\(\s*typeof\s*\(\s*(\w+)\s*\)\s*,\s*typeof\s*\(\s*(\w+)\s*\)\s*\)"
    matches_typeof = re.findall(pattern_typeof, code)
    for iface, impl in matches_typeof:
        registrations.add(iface)
    # Simple Register<IAuditService>()
    pattern_single = r"\.Register\w*\s*<\s*(\w+)\s*>\s*\("
    matches_single = re.findall(pattern_single, code)
    for iface in matches_single:
        registrations.add(iface)
    return registrations


def extract_implementations_from_code(code: str) -> Dict[str, str]:
    impls = {}
    # Regex to find class implementations, e.g., class AuditService : IAuditService
    pattern = r"class\s+(\w+)\s*:\s*(\w+)"
    matches = re.findall(pattern, code)
    for impl, iface in matches:
        if iface.startswith("I"):
            impls[iface] = impl
    return impls


@pytest.fixture(scope="module")
def manifest_data():
    return load_manifest()


def test_all_interfaces_registered(manifest_data):
    # Exclude framework interfaces that are provided by Prism/DryIoc and don't need registration
    excluded_interfaces = {
        "IContainerRegistry",
        "IContainerExtension",
        "IModuleCatalog",
        "IEventAggregator",
        "IDialogService",
        "IRegionManager",
        "IInteractionRequestService",
        "INavigationService",
        "IContainerProvider",
        "IServiceLocator",
        "IResolver",
        "IScope",
        "IRules",
        "IContainer",
        "I",  # Invalid interface name
        # Marker/base interfaces that don't need DI registration
        "IAuditable",
        "ISaveable",
        "ISoftDeletable",
        "IAppDbContext",
        # Optional or external services
        "IMemoryProfiler",
        "IApplicationStateService",
        "IStartupProgressReporter",
        "IViewRegistrationService",
        "IRegionBehaviorFactory",
        "ICacheService",
        "IUnitOfWorkBestPractice",
    }

    # Extract interfaces from all C# source files
    source_files = [
        f
        for f in manifest_data["files"]
        if f["context"]["category"] == "source_code"
        and f["metadata"]["language"] == "C#"
    ]
    interfaces = set()
    for file in source_files:
        file_path = file["metadata"]["path"]
        code = fetch_file_content(file_path)
        file_interfaces = extract_interfaces_from_code(code)
        interfaces.update(file_interfaces)

    # Filter out excluded interfaces
    interfaces = interfaces - excluded_interfaces

    assert len(interfaces) > 0, "No interfaces found in source code"

    di_files = find_di_registration_files(manifest_data)
    assert len(di_files) > 0, "No DI configuration files found"

    registered_interfaces = set()
    all_implementations = {}

    for file in di_files:
        file_path = file["metadata"]["path"]
        code = fetch_file_content(file_path)
        registered = extract_registrations_from_code(code)
        registered_interfaces.update(registered)

        impls = extract_implementations_from_code(code)
        all_implementations.update(impls)

    # Also fetch all source code files to find implementations
    for file in source_files:
        if file not in di_files:  # Avoid duplicate
            file_path = file["metadata"]["path"]
            code = fetch_file_content(file_path)
            impls = extract_implementations_from_code(code)
            all_implementations.update(impls)

    missing_registrations = interfaces - registered_interfaces
    assert (
        not missing_registrations
    ), f"Missing DI registrations for interfaces: {missing_registrations}"


def test_all_registered_have_implementations(manifest_data):
    di_files = find_di_registration_files(manifest_data)
    registered_interfaces = set()

    for file in di_files:
        file_path = file["metadata"]["path"]
        code = fetch_file_content(file_path)
        registered = extract_registrations_from_code(code)
        registered_interfaces.update(registered)

    # Only check interfaces (not concrete classes) for implementations
    registered_interfaces = {
        iface for iface in registered_interfaces if iface.startswith("I")
    }

    # Exclude framework interfaces that are registered as instances or provided by Prism/DryIoc
    excluded_framework = {
        "IBudgetImporter",
        "IDialogService",
        "IMemoryCache",
        "IScopedRegionService",
        "IEventAggregator",
        "IConfiguration",
        "IExceptionHandler",
        "ISecretVaultService",
        "ILoggerFactory",
    }
    registered_interfaces = registered_interfaces - excluded_framework

    source_files = [
        f
        for f in manifest_data["files"]
        if f["context"]["category"] == "source_code"
        and f["metadata"]["language"] == "C#"
    ]
    all_implementations = {}

    for file in source_files:
        file_path = file["metadata"]["path"]
        code = fetch_file_content(file_path)
        impls = extract_implementations_from_code(code)
        all_implementations.update(impls)

    missing_impls = {
        iface for iface in registered_interfaces if iface not in all_implementations
    }
    assert (
        not missing_impls
    ), f"Missing implementations for registered interfaces: {missing_impls}"


def test_no_duplicate_registrations(manifest_data):
    di_files = find_di_registration_files(manifest_data)
    all_registrations = []

    for file in di_files:
        file_path = file["metadata"]["path"]
        code = fetch_file_content(file_path)
        registered = extract_registrations_from_code(code)
        all_registrations.extend(registered)

    duplicates = {
        iface for iface in all_registrations if all_registrations.count(iface) > 1
    }
    assert not duplicates, f"Duplicate DI registrations found: {duplicates}"
