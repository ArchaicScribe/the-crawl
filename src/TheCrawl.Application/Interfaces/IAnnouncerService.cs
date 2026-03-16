namespace TheCrawl.Application.Interfaces;

public interface IAnnouncerService
{
    string OnSessionStart(string playerName, string playerClass);
    string OnKill(string playerName, string enemyName, int killCount);
    string OnPlayerDamaged(string playerName, int damage, int remainingHp);
    string OnDeath(string playerName, int floorsCleared, int killCount);
    string OnFloorDescend(string playerName, int newFloor);
    string OnItemPickup(string playerName, string itemName);
    string OnObjectionSucceeds(string enemyName);
}
