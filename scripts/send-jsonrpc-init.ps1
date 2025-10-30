# Sends a framed JSON-RPC initialize message to the csharp-mcp container's stdin
$body = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"ps-test","version":"1.0"}}}'
$len = [System.Text.Encoding]::UTF8.GetByteCount($body)
$frame = "Content-Length: $len`r`n`r`n" + $body

Write-Output "Injecting framed initialize (length=$len) into csharp-mcp stdin..."
$frame | docker exec -i csharp-mcp sh -c 'cat >/proc/1/fd/0'

Start-Sleep -Milliseconds 600

Write-Output "Recent container logs:"
docker logs csharp-mcp --tail 80
