# Smoke test script — checks all key HTTP endpoints
# Exit code 0 = all pass, Exit code 1 = any failure

$checks = @(
    @{ Name = "Frontend";     Url = "http://localhost" },
    @{ Name = "Health";       Url = "http://localhost/health" },
    @{ Name = "API Info";     Url = "http://localhost/api" },
    @{ Name = "Swagger API1"; Url = "http://localhost:5000/swagger/index.html" },
    @{ Name = "Swagger API2"; Url = "http://localhost:5001/swagger/index.html" }
)

$failed = 0

foreach ($check in $checks) {
    try {
        $r = Invoke-WebRequest -Uri $check.Url -UseBasicParsing -TimeoutSec 10
        Write-Host "PASS  $($check.Name): $($r.StatusCode)" -ForegroundColor Green
    } catch {
        Write-Host "FAIL  $($check.Name): $_" -ForegroundColor Red
        $failed++
    }
}

if ($failed -gt 0) {
    Write-Host "`nSmoke checks FAILED ($failed failures)" -ForegroundColor Red
    exit 1
} else {
    Write-Host "`nAll smoke checks PASSED" -ForegroundColor Green
    exit 0
}
