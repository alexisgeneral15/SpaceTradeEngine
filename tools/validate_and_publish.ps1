param(
  [string]$DataRoot = "$PSScriptRoot\..\assets\data",
  [switch]$SkipPublish
)

Write-Host "== Building DataValidator =="
Push-Location "$PSScriptRoot\DataValidator"
try {
  dotnet build -c Release | Out-Host
  $exit = $LASTEXITCODE
  if ($exit -ne 0) { throw "DataValidator build failed ($exit)" }

  Write-Host "== Running DataValidator =="
  dotnet run -c Release -- "$DataRoot" | Out-Host
  $exit = $LASTEXITCODE
  if ($exit -ne 0) { throw "Validation failed ($exit)" }
}
finally {
  Pop-Location
}

if (-not $SkipPublish) {
  Write-Host "== Publishing SpaceTradeEngine =="
  Push-Location "$PSScriptRoot\.."
  try {
    dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true | Out-Host
    $exit = $LASTEXITCODE
    if ($exit -ne 0) { throw "Publish failed ($exit)" }
  }
  finally {
    Pop-Location
  }
}

Write-Host "== Done =="
