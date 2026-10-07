using TrueHardcoreRules;

static void Check(bool ok, string what) { if (!ok) throw new Exception("FAILED: " + what); }

Check(RuleMath.Scale(10, 0.5) == 5, "stock halves");
Check(RuleMath.Scale(1, 0.5) == 1, "stock never drops below 1");
Check(RuleMath.Scale(10000, 1.5) == 15000, "price x1.5");
Check(RuleMath.PayoutCoef(60, 0.5) == 80, "Fence paying 40% now pays 20%");
Check(RuleMath.PayoutCoef(60, 1) == 60, "payout x1 is unchanged");
Check(RuleMath.PayoutCoef(10, 3) == 0, "coef clamps at 0");
Check(RuleMath.Fits(1, 1, 1, 1, false, 2, 2), "1x1 at (1,1) fits 2x2");
Check(!RuleMath.Fits(2, 0, 1, 1, false, 2, 2), "x=2 is outside 2x2");
Check(!RuleMath.Fits(0, 1, 1, 2, false, 2, 2), "1x2 at y=1 overflows");
Check(RuleMath.Fits(0, 1, 1, 2, true, 2, 2), "rotated 1x2 becomes 2x1 and fits");
// Shipped config.json parses with the same options the mod uses and matches Hardcore Classic.
var opts = new System.Text.Json.JsonSerializerOptions { ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true, PropertyNameCaseInsensitive = true };
var cfg = System.Text.Json.JsonSerializer.Deserialize<HardcoreConfig>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../../config.json")), opts)!;
Check(cfg.Enabled && !cfg.Flea.Enabled && !cfg.Insurance.Enabled && cfg.ScavRaids.CooldownSeconds == 2700, "config.json core rules");
Check(cfg.Traders.CashPriceMultiplier == 1.5 && cfg.SecureContainer.AllowedCategories.Count == 4 && cfg.SecureContainer.ExcludedContainerIds.Count == 2, "config.json lists replace defaults, not append");
Console.WriteLine("RuleMath + config OK");
