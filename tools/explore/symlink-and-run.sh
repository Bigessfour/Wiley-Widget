#!/bin/sh
set -e
# Create symlinks from /src/src/* -> /src/* so old relative references work
for d in /src/src/*; do
	name=$(basename "$d")
	[ -e "/src/$name" ] && continue
	ln -s "$d" "/src/$name" || true
done

ls -la /src | sed -n '1,80p'

echo '--- now running script ---'
exec dotnet script /app/quickbooks-service.csx
