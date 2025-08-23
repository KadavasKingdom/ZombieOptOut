namespace ZombieOptOut;

public sealed class Config
{
    public bool Debug { get; set; }
    public float FillDuration { get; set; } = 10f;
    public float HealthCompensation { get; set; } = 200f;
}