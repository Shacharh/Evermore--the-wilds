using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that owns the player's item inventory for a battle session.
/// Data layer only — use InventoryUI for all UI concerns and item-use logic.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [System.Serializable]
    public struct ItemStack
    {
        public ItemData item;
        public int      quantity;
    }

    [SerializeField] private List<ItemStack> startingItems = new();

    private readonly Dictionary<string, ItemData>  _itemById  = new();
    private readonly Dictionary<string, int>       _quantities = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        foreach (var stack in startingItems)
        {
            if (stack.item == null) continue;
            _itemById[stack.item.ID] = stack.item;
            _quantities[stack.item.ID] = Mathf.Clamp(stack.quantity, 1, stack.item.MaxHeld);
        }
    }

    // ── Query ─────────────────────────────────────────────────────────────────

    public int  GetQuantity(ItemData item) => item != null && _quantities.TryGetValue(item.ID, out int q) ? q : 0;
    public bool HasItem(ItemData item)     => GetQuantity(item) > 0;

    /// <summary>Returns all items currently in stock (quantity > 0), sorted by archetype then name.</summary>
    public List<(ItemData item, int qty)> GetAll()
    {
        var result = new List<(ItemData, int)>();
        foreach (var kvp in _quantities)
            if (kvp.Value > 0 && _itemById.TryGetValue(kvp.Key, out ItemData item))
                result.Add((item, kvp.Value));

        result.Sort((a, b) =>
        {
            int archetypeCompare = a.Item1.Archetype.CompareTo(b.Item1.Archetype);
            return archetypeCompare != 0 ? archetypeCompare : string.Compare(a.Item1.DisplayName, b.Item1.DisplayName);
        });
        return result;
    }

    // ── Mutation ──────────────────────────────────────────────────────────────

    /// <summary>Adds qty of an item. Returns true if any amount was added (false if already at maxHeld).</summary>
    public bool AddItem(ItemData item, int qty = 1)
    {
        if (item == null || qty <= 0) return false;

        _itemById[item.ID] = item;
        int current = GetQuantity(item);
        int toAdd   = Mathf.Min(qty, item.MaxHeld - current);
        if (toAdd <= 0) return false;

        _quantities[item.ID] = current + toAdd;
        return true;
    }

    /// <summary>Removes qty of an item. Returns true if the item was available and consumed.</summary>
    public bool RemoveItem(ItemData item, int qty = 1)
    {
        if (item == null || qty <= 0) return false;

        int current = GetQuantity(item);
        if (current < qty) return false;

        _quantities[item.ID] = current - qty;
        return true;
    }
}
