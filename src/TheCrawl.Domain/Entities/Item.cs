using TheCrawl.Domain.ValueObjects;

namespace TheCrawl.Domain.Entities;

public enum ItemType { Weapon, Armor, Consumable, SponsorDrop }

public class Item
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public ItemType Type { get; private set; }
    public int EffectValue { get; private set; }
    public Position Position { get; private set; }

    private Item() { }

    public Item(string name, string description, ItemType type, int effectValue, Position position)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        Type = type;
        EffectValue = effectValue;
        Position = position;
    }

    public static Item Restore(
        Guid id, string name, string description,
        ItemType type, int effectValue, Position position) => new()
    {
        Id = id,
        Name = name,
        Description = description,
        Type = type,
        EffectValue = effectValue,
        Position = position
    };
}
