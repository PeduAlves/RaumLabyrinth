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

public static class EnemyDamagablePartExtensions
{
    public static EnemyConditions GetContionOfPartDamage(this EnemyDamagablePart part)
    {
        return part switch
        {
            EnemyDamagablePart.Head => EnemyConditions.Broken,
            EnemyDamagablePart.LeftArm => EnemyConditions.BrokeLeftArm,
            EnemyDamagablePart.RightArm => EnemyConditions.BrokeRightArm,
            EnemyDamagablePart.RightLeg => EnemyConditions.BrokeRightLeg,
            EnemyDamagablePart.LeftLeg => EnemyConditions.BrokeLeftLeg,
            _ => EnemyConditions.Stunned
        };
    }
}

public enum  EnemyConditions
{
    Stunned,
    Broken,
    BrokeLeftArm,
    BrokeRightArm,
    BrokeRightLeg,
    BrokeLeftLeg,
}

public static class EnemyConditionsExtensions
{
    public static string GetAnimatorConditionName(this EnemyConditions condition)
    {
        return condition switch
        {
            EnemyConditions.Broken => "Broken",
            EnemyConditions.Stunned => "Stunned",
            EnemyConditions.BrokeLeftArm => "Crippled.Arm.L",
            EnemyConditions.BrokeRightArm => "Crippled.Arm.R",
            EnemyConditions.BrokeRightLeg => "Crippled.Leg.R",
            EnemyConditions.BrokeLeftLeg => "Crippled.Leg.L",
            _ => throw new System.ArgumentOutOfRangeException(nameof(condition), condition, null)
        };
    }
}

public enum EnemyStates
{
    Looking,
    Walking,
    Attacking,
}

public static class EnemyStatesExtensions
{
    public static string GetAnimationTrigger(this EnemyStates state)
    {
        return state switch
        {
            EnemyStates.Looking => "Look",
            EnemyStates.Walking => "Walk",
            EnemyStates.Attacking => "Attack",
            _ => throw new System.ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }

    public static int GetStatePriority(this EnemyStates state)
    {
        return state switch
        {
            EnemyStates.Looking => 0,
            EnemyStates.Walking => 1,
            EnemyStates.Attacking => 2,
            _ => throw new System.ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }
}
