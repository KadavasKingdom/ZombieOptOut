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
}