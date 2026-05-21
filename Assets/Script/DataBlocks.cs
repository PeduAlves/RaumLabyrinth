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
    Broke,
    Looking,
    Walking,
    Attacking,
}

public static class EnemyStateExtensions
{
    public static string GetAnimationTrigger(this EnemyState state)
    {
        return state switch
        {
            EnemyState.Broke => "Break",
            EnemyState.Looking => "Look",
            EnemyState.Walking => "Walk",
            EnemyState.Attacking => "Attack",
            _ => throw new System.ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }
}

public enum EnemyMovementState
{
    Idle,
    Moving,
    Airborne
}