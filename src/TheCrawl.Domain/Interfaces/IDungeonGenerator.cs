using TheCrawl.Domain.Entities;
using TheCrawl.Domain.Enums;

namespace TheCrawl.Domain.Interfaces;

public interface IDungeonGenerator
{
    Floor GenerateFloor(int floorNumber, ZoneType zone);
}
