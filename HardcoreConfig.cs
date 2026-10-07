namespace TrueHardcoreRules;

// Defaults here = the "Hardcore Classic" ruleset, so any key missing from config.json keeps its Classic value.
public class HardcoreConfig
{
    public bool Enabled { get; set; } = true;
    public bool WarnOnConflicts { get; set; } = true;
    public FleaRules Flea { get; set; } = new();
    public InsuranceRules Insurance { get; set; } = new();
    public ScavRules ScavRaids { get; set; } = new();
    public TraderRules Traders { get; set; } = new();
    public FenceRules Fence { get; set; } = new();
    public SecureContainerRules SecureContainer { get; set; } = new();
    public HealingRules Healing { get; set; } = new();
    public RepairRules Repairs { get; set; } = new();
    public GiftRules StartingGifts { get; set; } = new();
}

public class FleaRules
{
    public bool Enabled { get; set; } = false;
    public int UnlockLevel { get; set; } = 15;
}

public class InsuranceRules
{
    public bool Enabled { get; set; } = false;
}

public class ScavRules
{
    public bool Enabled { get; set; } = true;
    public int CooldownSeconds { get; set; } = 2700;
}

public class TraderRules
{
    public bool Enabled { get; set; } = true;
    public double CashPriceMultiplier { get; set; } = 1.5;
    public double StockMultiplier { get; set; } = 0.5;
    public double RestockTimeMultiplier { get; set; } = 2.0;
    public List<string> ExcludedTraderIds { get; set; } = [];
}

public class FenceRules
{
    public bool Enabled { get; set; } = true;
    public double PayoutMultiplier { get; set; } = 0.5;
    public List<string> BlockedCategories { get; set; } = ["WEAPON", "ARMORED_EQUIPMENT", "VEST", "BACKPACK", "AMMO", "AMMO_BOX"];
}

public class SecureContainerRules
{
    public bool Enabled { get; set; } = true;
    public int Width { get; set; } = 2;
    public int Height { get; set; } = 2;
    public List<string> AllowedCategories { get; set; } = ["KEY", "INFO", "MEDS", "AMMO"];
    // Developer + Boss containers. Every bot uses the Boss container, so excluding it keeps bot loadouts vanilla.
    public List<string> ExcludedContainerIds { get; set; } = ["5c0a5a5986f77476aa30ae64", "5c0a794586f77461c458f892"];
}

public class HealingRules
{
    public bool TherapistPostRaidHealing { get; set; } = false;
}

public class RepairRules
{
    public bool TraderRepairs { get; set; } = false;
}

public class GiftRules
{
    public bool Enabled { get; set; } = false;
    public List<string> GiftIds { get; set; } = ["PraporGiftDay1", "PraporGiftDay2", "MechanicGiftDay1"];
}
