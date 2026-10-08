# Maintenance

## Routine upstream update

1. Let the weekly **Sync upstream** workflow open a pull request, or run it manually from the Actions tab.
2. Confirm **Radar boundary** and **Build** pass.
3. Review changes touching these high-risk files before merging:
   - `BossMod/Framework/Plugin.cs`
   - `BossMod/Framework/PassiveActionObserver.cs`
   - `BossMod/Framework/WorldStateGameSync.cs`
   - IPC, AI, autorotation, movement, and action-manager code
4. Set `release-version.txt` to the chosen four-part numeric version in the pull request, then merge it. A change to this file on `main` starts the release workflow.
5. Alternatively, create a four-part numeric tag or run **Publish** manually with the version. Use only one release method for a given version.
6. Confirm the **Publish** workflow creates `latest.zip` and updates `pluginmaster.json`.

## Merge conflicts

The sync workflow deliberately stops on a conflict. Resolve it locally while preserving the companion bootstrap and passive observer. Run:

```powershell
pwsh scripts/Verify-RadarBoundary.ps1
dotnet build -c Release BossMod/BossModReborn.csproj
```

Never resolve a bootstrap conflict by restoring upstream `Plugin.cs`; that would re-enable IPC and action-capable managers.

## Versioning

Dalamud assembly versions use four numeric components. Prefer the current BossModReborn version when it has not already been released here. If a companion-only fix is needed, increment the fourth component.
