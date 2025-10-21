"""SQL connectivity smoke tests using Python and Azure AD tokens.

This script replaces the PowerShell implementation of `test-ef-connection.ps1`.
It exercises connection scenarios against local SQL Server instances and Azure SQL
Database using the Microsoft recommended Azure AD authentication flow. The
script is intentionally dependency-light and fails fast when prerequisites are
missing.

Usage (PowerShell):

```
python test_ef_connection.py --verbose
```

Optional arguments allow overriding the server name, database name, and the ODBC
SQL Server driver. See ``python test_ef_connection.py --help`` for details.
"""

from __future__ import annotations

import argparse
import contextlib
import logging
import os
import sys
from dataclasses import dataclass
from typing import TYPE_CHECKING, Any, Dict, Iterable, Optional, Sequence, Tuple

TOKEN_SCOPE = "https://database.windows.net/.default"
SQL_ACCESS_TOKEN_COPT = 1256
DEFAULT_DRIVER = os.environ.get(
    "SQLSERVER_ODBC_DRIVER", "ODBC Driver 18 for SQL Server"
)
DEFAULT_TIMEOUT_SECONDS = 30

try:  # pragma: no cover - import guard
    import pyodbc  # type: ignore
except ImportError as import_error:  # pragma: no cover - handled in runtime checks
    pyodbc = None  # type: ignore
    PYODBC_IMPORT_ERROR = import_error
else:  # pragma: no cover - import guard bookkeeping
    PYODBC_IMPORT_ERROR = None

try:  # pragma: no cover - import guard
    from azure.core.exceptions import ClientAuthenticationError
    from azure.identity import CredentialUnavailableError, DefaultAzureCredential
except ImportError as import_error:  # pragma: no cover - handled later
    DefaultAzureCredential = None  # type: ignore[assignment]
    CredentialUnavailableError = None  # type: ignore[assignment]
    ClientAuthenticationError = None  # type: ignore[assignment]
    AZURE_IDENTITY_IMPORT_ERROR = import_error
else:  # pragma: no cover - import guard bookkeeping
    AZURE_IDENTITY_IMPORT_ERROR = None

if TYPE_CHECKING:  # pragma: no cover - typing-only imports
    import pyodbc as pyodbc_types
    from azure.identity import DefaultAzureCredential as DefaultAzureCredentialType
else:  # pragma: no cover - typing fallbacks
    DefaultAzureCredentialType = Any
    pyodbc_types = Any  # type: ignore[assignment]

CREDENTIAL_EXCEPTION_TYPES: Tuple[type[Exception], ...] = tuple(
    exc
    for exc in (CredentialUnavailableError, ClientAuthenticationError)
    if isinstance(exc, type)
)


@dataclass(frozen=True)
class ConnectionScenario:
    """Single SQL connectivity scenario."""

    name: str
    connection_string: str
    requires_token: bool = True


def parse_connection_string(connection_string: str) -> Dict[str, str]:
    """Parse a semi-colon delimited connection string into a dictionary."""

    parameters: Dict[str, str] = {}
    for part in connection_string.split(";"):
        if not part.strip():
            continue
        if "=" not in part:
            raise ValueError(f"Invalid connection string segment: '{part}'")
        key, value = part.split("=", 1)
        parameters[key.strip().lower()] = value.strip()
    return parameters


def to_bool_setting(value: str, *, default: bool = True) -> str:
    """Convert textual boolean flag to the driver-friendly yes/no form."""

    truthy = {"true", "1", "yes"}
    falsy = {"false", "0", "no"}
    normalized = value.lower()
    if normalized in truthy:
        return "yes"
    if normalized in falsy:
        return "no"
    return "yes" if default else "no"


def build_odbc_connection_string(
    parameters: Dict[str, str], *, driver: str, timeout_seconds: int
) -> str:
    """Convert connection-string parameters into an ODBC connection string."""

    server = parameters.get("server") or parameters.get("data source")
    database = parameters.get("database")
    if not server or not database:
        raise ValueError(
            "Server and Database must be supplied in the connection string."
        )

    encrypt = to_bool_setting(parameters.get("encrypt", "true"))
    trust_cert = to_bool_setting(
        parameters.get("trustservercertificate", "false"), default=False
    )

    parts = [
        f"DRIVER={{{driver}}}",
        f"SERVER={server}",
        f"DATABASE={database}",
        f"Encrypt={encrypt}",
        f"TrustServerCertificate={trust_cert}",
        f"Connection Timeout={timeout_seconds}",
    ]

    application_intent = parameters.get("application intent")
    if application_intent:
        parts.append(f"Application Intent={application_intent}")

    return ";".join(parts)


def get_available_drivers() -> list[str]:
    """Get list of available ODBC drivers."""
    ensure_pyodbc_available()
    return pyodbc.drivers()  # type: ignore[attr-defined]


def build_odbc_connection_string_with_fallback(
    parameters: Dict[str, str], *, timeout_seconds: int
) -> str:
    """Build ODBC connection string with driver fallback."""
    preferred_drivers = [
        "ODBC Driver 18 for SQL Server",
        "ODBC Driver 17 for SQL Server",
        "SQL Server",
        "SQL Server Native Client 11.0",
    ]

    available_drivers = get_available_drivers()

    for driver in preferred_drivers:
        if driver in available_drivers:
            try:
                return build_odbc_connection_string(
                    parameters, driver=driver, timeout_seconds=timeout_seconds
                )
            except Exception:
                continue

    raise RuntimeError(
        f"No suitable ODBC driver found. Available drivers: {available_drivers}"
    )


def ensure_pyodbc_available() -> None:
    """Raise a helpful error message if pyodbc is not installed."""

    if pyodbc is None:  # pragma: no cover - runtime validation
        message = (
            "pyodbc is required. Install it with 'pip install pyodbc' and make sure the "
            "SQL Server ODBC driver is available on this machine."
        )
        raise RuntimeError(message) from PYODBC_IMPORT_ERROR


def create_default_credential() -> Any:
    """Instantiate DefaultAzureCredential with sane desktop defaults."""

    if DefaultAzureCredential is None:  # pragma: no cover - runtime validation
        message = (
            "azure-identity is required. Install it with 'pip install azure-identity' to use "
            "Azure AD authentication."
        )
        raise RuntimeError(message) from AZURE_IDENTITY_IMPORT_ERROR

    return DefaultAzureCredential(
        exclude_interactive_browser_credential=False,
        exclude_visual_studio_code_credential=False,
        exclude_powershell_credential=False,
    )


def acquire_access_token(credential: Any) -> bytes:
    """Request an access token bytes payload for Azure SQL."""

    token = credential.get_token(TOKEN_SCOPE)
    return token.token.encode("utf-16-le")


def open_connection(
    parameters: Dict[str, str],
    *,
    driver: str,
    timeout_seconds: int,
    token_bytes: Optional[bytes] = None,
) -> Any:
    """Establish an ODBC connection, optionally with an Azure AD token."""

    ensure_pyodbc_available()

    sanitized = dict(parameters)
    sanitized.pop("authentication", None)

    # Try specified driver first, then fallback
    connection_string = build_odbc_connection_string(
        sanitized, driver=driver, timeout_seconds=timeout_seconds
    )

    connect_kwargs: Dict[str, Any] = {"timeout": timeout_seconds}
    if token_bytes is not None:
        connect_kwargs["attrs_before"] = {SQL_ACCESS_TOKEN_COPT: token_bytes}

    try:
        return pyodbc.connect(connection_string, **connect_kwargs)  # type: ignore[attr-defined]
    except Exception as exc:
        if (
            "driver" in str(exc).lower()
            or "data source name not found" in str(exc).lower()
        ):
            # Try fallback drivers
            fallback_connection_string = build_odbc_connection_string_with_fallback(
                sanitized, timeout_seconds=timeout_seconds
            )
            return pyodbc.connect(fallback_connection_string, **connect_kwargs)  # type: ignore[attr-defined]
        raise


def run_connection_test(
    scenario: ConnectionScenario,
    *,
    driver: str,
    timeout_seconds: int,
    credential: Optional[Any],
    force_token: bool,
) -> bool:
    """Run the SQL connectivity test for a specific scenario."""

    parameters = parse_connection_string(scenario.connection_string)
    requires_token = scenario.requires_token or force_token
    token_bytes: Optional[bytes] = None

    if requires_token and credential is None:
        logging.error(
            "Azure AD authentication required for '%s', but azure-identity is not installed.",
            scenario.name,
        )
        return False

    if credential is not None and requires_token:
        try:
            token_bytes = acquire_access_token(credential)
        except Exception as exc:  # pragma: no cover - runtime specific
            if CREDENTIAL_EXCEPTION_TYPES and isinstance(
                exc, CREDENTIAL_EXCEPTION_TYPES
            ):
                logging.error("Unable to obtain Azure AD token: %s", exc)
            else:
                logging.error("Unexpected error obtaining Azure AD token: %s", exc)
            return False

    logging.info("\nTesting: %s", scenario.name)
    if pyodbc is not None and hasattr(pyodbc, "Error"):
        pyodbc_error = pyodbc.Error  # type: ignore[attr-defined]
    else:
        pyodbc_error = Exception

    try:
        with contextlib.closing(
            open_connection(
                parameters,
                driver=driver,
                timeout_seconds=timeout_seconds,
                token_bytes=token_bytes,
            )
        ) as connection:
            with contextlib.closing(connection.cursor()) as cursor:
                cursor.execute("SELECT @@VERSION AS Version")
                version_row = cursor.fetchone()
                version_text = version_row[0] if version_row else "(no result)"
                first_line = str(version_text).splitlines()[0]
            logging.info("✅ SUCCESS (%s)", first_line)
            return True
    except pyodbc_error as exc:  # pragma: no cover - requires SQL Server
        logging.error("❌ FAILED: %s", exc)
        return False
    except Exception as exc:  # pragma: no cover - defensive guard
        logging.error("❌ FAILED: %s", exc)
        return False


def build_scenarios(server: str, database: str) -> Iterable[ConnectionScenario]:
    """Create the default set of connection scenarios."""

    # Local SQL Server scenarios
    yield ConnectionScenario(
        name="Local SQL Server Express",
        connection_string=(
            "Server=localhost\\SQLEXPRESS;"
            "Database=master;"
            "Trusted_Connection=True;"
            "Encrypt=no;"
        ),
        requires_token=False,
    )

    yield ConnectionScenario(
        name="Local SQL Server Default Instance",
        connection_string=(
            "Server=localhost;"
            "Database=master;"
            "Trusted_Connection=True;"
            "Encrypt=no;"
        ),
        requires_token=False,
    )

    # Azure SQL scenarios (only if server is not localhost)
    if server and server.lower() not in ("localhost", "."):
        base = (
            f"Server=tcp:{server}.database.windows.net,1433;"
            f"Database={database};"
            "Encrypt=True;"
            "TrustServerCertificate=False;"
            f"Connection Timeout={DEFAULT_TIMEOUT_SECONDS};"
        )

        yield ConnectionScenario(
            name="Authentication=Active Directory Default",
            connection_string=f"{base}Authentication=Active Directory Default;",
            requires_token=True,
        )

        yield ConnectionScenario(
            name="No Authentication parameter",
            connection_string=base,
            requires_token=True,
        )


def configure_logging(verbose: bool) -> None:
    """Configure basic logging."""

    level = logging.DEBUG if verbose else logging.INFO
    logging.basicConfig(level=level, format="%(message)s")


def parse_args(argv: Optional[Iterable[str]] = None) -> argparse.Namespace:
    """Parse command-line arguments."""

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--server",
        default=os.environ.get("AZURE_SQL_SERVER", "wileywidget-sql"),
        help="Azure SQL server name (without .database.windows.net).",
    )
    parser.add_argument(
        "--database",
        default=os.environ.get("AZURE_SQL_DATABASE", "WileyWidgetDB"),
        help="Azure SQL database name.",
    )
    parser.add_argument(
        "--driver",
        default=DEFAULT_DRIVER,
        help="ODBC driver name to use (default: %(default)s).",
    )
    parser.add_argument(
        "--timeout",
        type=int,
        default=DEFAULT_TIMEOUT_SECONDS,
        help="Connection timeout in seconds (default: %(default)s).",
    )
    parser.add_argument(
        "--no-token",
        action="store_true",
        help="Skip Azure AD token acquisition even if the scenario requests it.",
    )
    parser.add_argument(
        "--verbose",
        action="store_true",
        help="Enable verbose logging output.",
    )
    sequence: Optional[Sequence[str]]
    if argv is None:
        sequence = None
    else:
        sequence = list(argv)

    return parser.parse_args(sequence)


def main(argv: Optional[Iterable[str]] = None) -> int:
    """Script entry point."""

    args = parse_args(argv)
    configure_logging(args.verbose)

    credential: Optional[Any] = None
    if not args.no_token:
        if AZURE_IDENTITY_IMPORT_ERROR is not None:
            logging.warning(
                "azure-identity not installed. Install it to enable Azure AD authentication."
            )
        else:
            credential = create_default_credential()

    scenarios = build_scenarios(args.server, args.database)

    successes = 0
    total = 0

    for scenario in scenarios:
        total += 1
        success = run_connection_test(
            scenario,
            driver=args.driver,
            timeout_seconds=args.timeout,
            credential=credential,
            force_token=not args.no_token,
        )
        if success:
            successes += 1

    logging.info("\nSummary: %s/%s scenarios succeeded", successes, total)
    return 0 if successes == total else 1


if __name__ == "__main__":  # pragma: no cover - CLI entry point
    sys.exit(main())
