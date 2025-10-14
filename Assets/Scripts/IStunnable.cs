public interface IStunnable
{
    bool ApplyStun(float seconds);
    bool IsStunned { get; }
}