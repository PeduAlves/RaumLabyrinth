public interface Damagable
{
    void TakeDamage(int amount);
}

public enum EnemyDamagablePart
{
    Head,
    Arm,
    Leg,
    Torso
}