# SpaceTradeEngine Data Validator

Validates game data JSON files (`assets/data/*`) against JSON Schemas.

## Schemas
- `schemas/wares.schema.json`
- `schemas/stations.schema.json`
- `schemas/factions.schema.json`
- `schemas/events.schema.json`

## Build
```pwsh
cd "c:\Users\alexi\Downloads\UnendingGalaxyDeluxe\SpaceTradeEngine\tools\DataValidator"
dotnet build -c Release
```

## Run
Default root is `assets/data` relative to repo; pass a path to override.
```pwsh
# Validate default data root
dotnet run -c Release

# Validate a specific data folder
dotnet run -c Release -- "c:\path\to\your\assets\data"
```

Exit code is non-zero if any file fails validation.
