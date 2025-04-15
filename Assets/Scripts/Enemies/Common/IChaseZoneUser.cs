public interface IChaseZoneUser
{
    void SetPlayerInChaseZone(bool isInZone);
    bool IsAggroed { get; }
}
