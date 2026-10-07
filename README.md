# True Hardcore Rules

A server-only hardcore ruleset for **SPT 4.1.6** (server runtime .NET 10). It applies the **Hardcore Classic** rules, except that stash upgrades in the hideout work as in vanilla.

It changes only the economy and services. Bot AI, bot spawns, bosses, loot spawns, extracts and raid flow stay vanilla, so SAIN, BSP and similar mods behave exactly as before.

## What it does (default config)

| Area | Rule |
|---|---|
| Flea | Locked. It never unlocks. |
| Insurance | Off for every trader, including modded traders. |
| Scav raids | On, with a base cooldown of 45 minutes. |
| Traders | Cash prices ×1.5, stock and buy limits ×0.5, restock time ×2. Barter prices are unchanged. |
| Fence | Pays half what he normally does. Doesn't sell weapons, armor, rigs, backpacks or ammo. |
| Secure container | Every container is shrunk to at most 2×2. It only holds keys, info items, meds and ammo. |
| Stash | Vanilla, so you can upgrade it in the hideout. |
| Post-raid healing | The Therapist treatment screen is off. You heal with your own meds. |
| Repairs | Repair kits only, no trader repairs. |
| Starting gifts | No Prapor or Mechanic gifts for new profiles. |

## Install

1. **Remove HardcoreRules.** This mod replaces it. Delete or move out both of these:
   - `SPT_Runtime\user\mods\HardcoreRules`
   - `BepInEx\plugins\HardcoreRules`
2. Copy `TrueHardcoreRules.dll` and `config.json` to `SPT_Runtime\user\mods\TrueHardcoreRules\`.
3. **Before the first start, empty your secure container**, or at least move out anything outside its top-left 2×2. If you skip this, the mod keeps that container at its old size and logs which items to move. See Known issues.
4. Start the server. You should see `[TrueHardcoreRules] Hardcore rules applied.` plus one line for each rule.

### Build from source

You need the .NET 10 SDK.

```
dotnet build -c Release -p:SptGameDir="E:\Games\Escape From Tarkov - 4.1.6" -p:CopyToGame=true
```

`CopyToGame` copies the DLL into the mod folder. It also copies `config.json`, but only if the mod folder doesn't have one yet.

To check the rule math, run `dotnet run --project tests/RuleMathCheck`.

## Tuning

Edit `config.json` and restart the server. The file allows comments. If you delete a key, it falls back to its Hardcore Classic value.

- **Too punishing?**
  - Set `traders.stockMultiplier` to `0.75`.
  - Set `scavRaids.cooldownSeconds` to `1800`.
  - Or set `flea.enabled: true` with `unlockLevel: 30`, which gives you a late-game flea.
- **Not punishing enough?**
  - Set `scavRaids.enabled: false`.
  - Set `secureContainer.allowedCategories` to `["KEY", "INFO"]`.
  - Set `fence.payoutMultiplier` to `0.25`.
- **Leave a trader alone:** add its id to `traders.excludedTraderIds`.
- **Category names:** use any `BaseClasses` name from SPT, such as `KEY`, `INFO`, `MEDS`, `AMMO`, `WEAPON`, `ARMORED_EQUIPMENT`, `VEST`, `BACKPACK` or `MAGAZINE`, or a raw 24-character template id.

## Interop

The mod runs after mods that edit the database during Preload (SVM, HardcoreRules) and after profiles load. It runs before Fence and flea offers are generated. Wherever it overlaps with another mod, its rules apply.

With `warnOnConflicts` on, it logs a warning when it detects any of these:

- **HardcoreRules**: remove it.
- **SVM**: changes to trader prices and stock stack. Set SVM's trader multipliers back to 1.
- **fleasimulator**.
- **acidphantasm-itemvaluation**.

It works with Fika without changes, because it has no client component.

## Known issues

- **Insurance, trader repairs and post-raid treatment are hidden by trader flags that the client reads.** The 4.1.6 server doesn't check those flags itself. Normal play respects them, but a client mod that forces the screens open could get around them.
- **Secure container shrink safety:** a container is only shrunk if no profile has an item outside the new area.
  - If some profile does, that container keeps its old size, and the log names the profile and the item. Move the item and restart.
  - The size check uses each item's base size and ignores attachments.
- **Disabling scav raids takes effect after your next scav raid.** A scav cooldown that's already running keeps its old end time.
- **Bots aren't affected by the secure container rule**, because they use the Boss container, which is excluded.
- **Edition starting stash:** EOD and Unheard profiles still start with the larger stash. Stash rules are vanilla.
- **Price and stock changes apply to every trader, including modded ones.** Use `excludedTraderIds` to leave one alone.
- **Insurance that's already pending** before install still comes back.
- **Not yet tested in game.** It compiles against the 4.1.6 server DLLs, and the hook points were checked against the decompiled 4.1.6 server and client.
