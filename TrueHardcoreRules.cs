using System.Reflection;
using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;

namespace TrueHardcoreRules;

// Runs after mods that edit the DB at Preload (SVM, HardcoreRules) and after profiles load (SaveCallbacks),
// but before Fence assorts (TraderCallbacks) and flea offers (RagfairCallbacks) are generated.
[Injectable(TypePriority = OnLoadOrder.TraderCallbacks - 1)]
public class TrueHardcoreRules(
    ISptLogger<TrueHardcoreRules> logger,
    ModHelper modHelper,
    GlobalTable globals,
    TradersTable traders,
    TemplateTable templates,
    TraderConfig traderConfig,
    GiftsConfig giftsConfig,
    SaveServer saveServer,
    IReadOnlyList<SptMod> loadedMods) : IOnLoad
{
    private const string Tag = "[TrueHardcoreRules]";
    private const int FleaNeverUnlocks = 99; // max player level is ~79
    private const int ScavDisabledCooldownSeconds = 10 * 365 * 24 * 3600;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly (string Match, string Advice)[] KnownOverlaps =
    [
        ("HardcoreRules", "duplicates flea/insurance/scav/Fence/secure-container rules. Remove its server folder and BepInEx/plugins/HardcoreRules."),
        ("Server Value Modifier", "SVM runs first; its trader price/stock, insurance, flea, repair and secure-container settings are stacked on or overridden here."),
        ("fleasimulator", "also changes the flea market."),
        ("itemvaluation", "changes item prices; the trader cash multiplier here stacks on top."),
    ];

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var cfg = LoadConfig();
        if (cfg is not { Enabled: true })
        {
            return Task.CompletedTask;
        }

        if (cfg.WarnOnConflicts)
        {
            WarnAboutConflicts();
        }

        ApplyFlea(cfg.Flea);
        ApplyScav(cfg.ScavRaids);
        ApplyTraderServices(cfg);
        ApplyTraders(cfg.Traders);
        ApplyFence(cfg.Fence);
        ApplySecureContainers(cfg.SecureContainer);
        ApplyGifts(cfg.StartingGifts);

        logger.Success($"{Tag} Hardcore rules applied.");
        return Task.CompletedTask;
    }

    private HardcoreConfig? LoadConfig()
    {
        var path = System.IO.Path.Combine(modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly()), "config.json");
        try
        {
            return JsonSerializer.Deserialize<HardcoreConfig>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex)
        {
            logger.Error($"{Tag} Could not read {path}. Nothing was changed.", ex);
            return null;
        }
    }

    private void WarnAboutConflicts()
    {
        foreach (var mod in loadedMods)
        {
            if (mod.ModMetadata.ModGuid == "com.truehardcorerules.server")
            {
                continue;
            }

            var id = $"{mod.ModMetadata.Name} {System.IO.Path.GetFileName(mod.Directory)}";
            foreach (var (match, advice) in KnownOverlaps)
            {
                if (id.Contains(match, StringComparison.OrdinalIgnoreCase))
                {
                    logger.Warning($"{Tag} Detected {mod.ModMetadata.Name}: {advice}");
                }
            }
        }
    }

    private void ApplyFlea(FleaRules flea)
    {
        globals.Configuration.RagFair.MinUserLevel = flea.Enabled ? flea.UnlockLevel : FleaNeverUnlocks;
        logger.Info($"{Tag} Flea: {(flea.Enabled ? $"unlocks at level {flea.UnlockLevel}" : "locked")}");
    }

    private void ApplyScav(ScavRules scav)
    {
        // Takes effect from the next scav raid; a lock already running keeps its old end time.
        globals.Configuration.SavagePlayCooldown = scav.Enabled ? scav.CooldownSeconds : ScavDisabledCooldownSeconds;
        logger.Info($"{Tag} Scav raids: {(scav.Enabled ? $"{scav.CooldownSeconds}s base cooldown" : "disabled")}");
    }

    // The client reads these trader flags to decide which traders show insure / repair / post-raid treatment.
    private void ApplyTraderServices(HardcoreConfig cfg)
    {
        foreach (var trader in traders.Values)
        {
            var b = trader.Base;
            if (!cfg.Insurance.Enabled && b.Insurance != null)
            {
                b.Insurance.Availability = false;
            }

            if (!cfg.Repairs.TraderRepairs && b.Repair != null)
            {
                b.Repair.Availability = false;
            }

            if (!cfg.Healing.TherapistPostRaidHealing)
            {
                b.Medic = false;
            }
        }

        logger.Info($"{Tag} Insurance: {OnOff(cfg.Insurance.Enabled)}, trader repairs: {OnOff(cfg.Repairs.TraderRepairs)}, post-raid treatment: {OnOff(cfg.Healing.TherapistPostRaidHealing)}");
    }

    private void ApplyTraders(TraderRules t)
    {
        if (!t.Enabled)
        {
            return;
        }

        var excluded = t.ExcludedTraderIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cash = new HashSet<MongoId> { Money.ROUBLES, Money.EUROS, Money.DOLLARS };

        foreach (var (id, trader) in traders)
        {
            // Fence's assort is generated separately (see ApplyFence).
            if (id == Traders.FENCE || excluded.Contains(id.ToString()) || trader.Assort == null)
            {
                continue;
            }

            foreach (var scheme in (trader.Assort.BarterScheme?.Values.SelectMany(s => s).SelectMany(s => s) ?? []))
            {
                if (cash.Contains(scheme.Template) && scheme.Count is { } count)
                {
                    scheme.Count = RuleMath.Scale(count, t.CashPriceMultiplier);
                }
            }

            // Restocks clone Assort.Items, so these reductions persist across resets.
            foreach (var item in trader.Assort.Items ?? [])
            {
                if (item.ParentId != "hideout" || item.Upd == null)
                {
                    continue;
                }

                if (item.Upd.UnlimitedCount != true && item.Upd.StackObjectsCount is { } stock)
                {
                    item.Upd.StackObjectsCount = RuleMath.Scale(stock, t.StockMultiplier);
                }

                if (item.Upd.BuyRestrictionMax is { } limit)
                {
                    item.Upd.BuyRestrictionMax = (int)RuleMath.Scale(limit, t.StockMultiplier);
                }
            }
        }

        foreach (var update in traderConfig.UpdateTime)
        {
            update.Seconds.Min = (int)(update.Seconds.Min * t.RestockTimeMultiplier);
            update.Seconds.Max = (int)(update.Seconds.Max * t.RestockTimeMultiplier);
        }

        traderConfig.UpdateTimeDefault = (int)(traderConfig.UpdateTimeDefault * t.RestockTimeMultiplier);
        logger.Info($"{Tag} Traders: cash prices x{t.CashPriceMultiplier}, stock x{t.StockMultiplier}, restock time x{t.RestockTimeMultiplier}");
    }

    private void ApplyFence(FenceRules f)
    {
        if (!f.Enabled)
        {
            return;
        }

        if (traders.TryGetValue(Traders.FENCE, out var fence))
        {
            foreach (var level in fence.Base.LoyaltyLevels ?? [])
            {
                if (level.BuyPriceCoefficient is { } coef)
                {
                    level.BuyPriceCoefficient = RuleMath.PayoutCoef(coef, f.PayoutMultiplier);
                }
            }
        }

        // Matched against base classes too, and also applied to Fence's weapon/armor presets.
        foreach (var category in ResolveCategories(f.BlockedCategories))
        {
            traderConfig.Fence.Blacklist.Add(category);
        }

        logger.Info($"{Tag} Fence: pays x{f.PayoutMultiplier}, won't sell {string.Join(", ", f.BlockedCategories)}");
    }

    private void ApplySecureContainers(SecureContainerRules s)
    {
        if (!s.Enabled)
        {
            return;
        }

        var allowed = ResolveCategories(s.AllowedCategories).ToHashSet();
        var excluded = s.ExcludedContainerIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cannotShrink = FindContainersThatCannotShrink(s, excluded);

        foreach (var (tpl, container) in templates.Items)
        {
            if (container.Parent != BaseClasses.MOB_CONTAINER || excluded.Contains(tpl.ToString()) || container.Properties?.Grids == null)
            {
                continue;
            }

            foreach (var grid in container.Properties.Grids)
            {
                var p = grid.Properties;
                if (p == null)
                {
                    continue;
                }

                if (!cannotShrink.Contains(tpl))
                {
                    p.CellsH = Math.Min(p.CellsH ?? s.Width, s.Width);
                    p.CellsV = Math.Min(p.CellsV ?? s.Height, s.Height);
                }

                // Keep the container's original exclusions (other secure containers, specific items).
                var stillExcluded = (p.Filters ?? []).SelectMany(f => f.ExcludedFilter ?? []).ToHashSet();
                stillExcluded.Add(BaseClasses.MOB_CONTAINER);
                p.Filters = [new GridFilter { Filter = [.. allowed], ExcludedFilter = stillExcluded }];
            }
        }

        logger.Info($"{Tag} Secure containers: {s.Width}x{s.Height}, only {string.Join(", ", s.AllowedCategories)}");
    }

    /// <summary>
    /// Shrinking a grid under an item that sits outside the new bounds would orphan it in the profile.
    /// Any container template with such an item keeps its size this session; the player is told what to move.
    /// </summary>
    private HashSet<MongoId> FindContainersThatCannotShrink(SecureContainerRules s, HashSet<string> excluded)
    {
        var blocked = new HashSet<MongoId>();
        foreach (var profile in saveServer.GetProfiles().Values)
        {
            var pmc = profile.CharacterData?.PmcData;
            var items = pmc?.Inventory?.Items;
            if (items == null)
            {
                continue;
            }

            foreach (var container in items.Where(i => i.SlotId == "SecuredContainer"))
            {
                if (excluded.Contains(container.Template.ToString()) || !templates.Items.TryGetValue(container.Template, out var containerTpl))
                {
                    continue;
                }

                var containerId = container.Id.ToString();
                foreach (var child in items.Where(i => i.ParentId == containerId))
                {
                    var grid = containerTpl.Properties?.Grids?.FirstOrDefault(g => g.Name == child.SlotId)?.Properties;
                    var loc = child.GetParsedLocation();
                    if (grid == null || loc == null)
                    {
                        continue;
                    }

                    var gridW = Math.Min(grid.CellsH ?? s.Width, s.Width);
                    var gridH = Math.Min(grid.CellsV ?? s.Height, s.Height);
                    // ponytail: base template size only; attachments that extend an item (weapons) aren't counted.
                    templates.Items.TryGetValue(child.Template, out var childTpl);
                    var w = (int)(childTpl?.Properties?.Width ?? 1);
                    var h = (int)(childTpl?.Properties?.Height ?? 1);

                    if (!RuleMath.Fits(loc.X ?? 0, loc.Y ?? 0, w, h, loc.R == ItemRotation.Vertical, gridW, gridH))
                    {
                        blocked.Add(container.Template);
                        logger.Warning($"{Tag} {pmc!.Info?.Nickname}: '{childTpl?.Name ?? child.Template.ToString()}' in your {containerTpl.Name} is outside the new {gridW}x{gridH} area. "
                            + "That container keeps its old size until you move the item to your stash and restart the server.");
                    }
                }
            }
        }

        return blocked;
    }

    private void ApplyGifts(GiftRules g)
    {
        if (g.Enabled)
        {
            return;
        }

        // GiftService skips gift ids that don't exist, so removing them is enough.
        foreach (var id in g.GiftIds)
        {
            giftsConfig.Gifts.Remove(id);
        }

        logger.Info($"{Tag} Starting gifts removed: {string.Join(", ", g.GiftIds)}");
    }

    /// <summary>Accepts BaseClasses names (KEY, MEDS, ...) or raw 24-char template ids.</summary>
    private IEnumerable<MongoId> ResolveCategories(IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            if (typeof(BaseClasses).GetField(name.ToUpperInvariant(), BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is MongoId id)
            {
                yield return id;
            }
            else if (MongoId.IsValidMongoId(name))
            {
                yield return new MongoId(name);
            }
            else
            {
                logger.Warning($"{Tag} Unknown category '{name}' ignored. Use a name like KEY, MEDS, WEAPON or a 24-character template id.");
            }
        }
    }

    private static string OnOff(bool on) => on ? "on" : "off";
}
