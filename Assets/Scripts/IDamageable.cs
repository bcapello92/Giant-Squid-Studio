public interface IDamageable
{
    void TakeDamage(int amount);
}

public interface IStunnable
{
    void ApplyStun(float seconds);
}