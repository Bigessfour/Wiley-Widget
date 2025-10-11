try:
    import azure
    print('Azure SDK available')
except ImportError as e:
    print(f'Azure SDK missing: {e}')

try:
    import azure.identity
    print('Azure Identity available')
except ImportError as e:
    print(f'Azure Identity missing: {e}')

try:
    import azure.storage
    print('Azure Storage available')
except ImportError as e:
    print(f'Azure Storage missing: {e}')

try:
    import pyodbc
    print('pyodbc available')
except ImportError as e:
    print(f'pyodbc missing: {e}')

try:
    import aiohttp
    print('aiohttp available')
except ImportError as e:
    print(f'aiohttp missing: {e}')