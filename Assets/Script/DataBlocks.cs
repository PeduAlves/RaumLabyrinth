public interface Damagable
{
    void TakeDamage(int amount);
}

public enum EnemyDamagablePart
{
    Head,
    LeftArm,
    RightArm,
    RightLeg,
    LeftLeg,
    Torso
}

public enum EnemyState
{
    Idle,
    Patrolling,
    Pursuing,
    Searching,
    Attacking
}

public enum EnemyMovementState
{
    Idle,
    Moving,
    Airborne
}