namespace ZombieOptOut;

public sealed class Config
{
    public bool Debug { get; set; }
    public bool AFKReplacement { get; set; } = true;
    public float AFKReplacementValidTime { get; set; } = 200f;
    public float SCPFillDuration { get; set; } = 15f;
    public float ZombieFillDuration { get; set; } = 10f;
    public float HealthCompensation { get; set; } = 200f;
    public bool DisableXPLoss { get; set; } = true;
    public int OptInExp { get; set; } = 100;
    public bool StackZombieCompensation { get; set; } = true;
    public bool UseCustomRoles { get; set; } = true;
}