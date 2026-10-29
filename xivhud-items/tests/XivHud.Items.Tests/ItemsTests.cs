using XivHud.Items.Core;
namespace XivHud.Items.Tests;
public sealed class ItemsTests
{
    private static BagSnapshot Bags(int loaded = 4) => new([
        new(100, "Iron Ore", "Stone", 0, 8, false, 1, 1, 1),
        new(100, "Iron Ore", "Stone", 0, 2, true, 2, 1, 1)], loaded, 140, 2);
    [Fact] public void SearchFindsCategoriesAndPreservesSeparateHqStacks()
    {
        Assert.Equal(2, Bags().Search(" STONE ").Count);
        Assert.Single(Bags().Search("iron", true));
        Assert.Equal(10, Bags().Count(100));
        Assert.Empty(Bags().Search("copper"));
    }
    [Fact] public void SharedStockIsSubtractedAfterCombiningRecipes()
    {
        RecipeEntry[] recipes = [new(1, "Ingot", "Smith", 1, 3, [new(100, "Iron Ore", 3)]), new(2, "Plate", "Smith", 1, 1, [new(100, "Iron Ore", 2)])];
        var need = Assert.Single(Worklist.Materials(recipes, new Dictionary<uint, int> { [1] = 3, [2] = 2 }, Bags()));
        Assert.Equal(13, need.Required); // Craft count, not result yield.
        Assert.Equal(10L, need.Owned); Assert.Equal(3L, need.Missing);
    }
    [Fact] public void CollectablesDoNotCountAsCraftingStock()
    {
        var bags = new BagSnapshot([new(100, "Ore", "Stone", 0, 12, false, 1, 1, 1, true)], 4, 140, 1);
        Assert.Equal(12, bags.Count(100)); Assert.Equal(0, bags.UsableCount(100));
        RecipeEntry[] recipes = [new(1, "Ingot", "Smith", 1, 1, [new(100, "Ore", 3)])];
        Assert.Equal(3L, Assert.Single(Worklist.Materials(recipes, new Dictionary<uint, int> { [1] = 1 }, bags)).Missing);
    }
    [Fact] public void PartialBagsAndCrystalsHaveUnknownAvailability()
    {
        RecipeEntry[] recipes = [new(1, "Ingot", "Smith", 1, 1, [new(100, "Ore", 3), new(2, "Shard", 1)])];
        var plan = new Dictionary<uint, int> { [1] = 1 };
        Assert.All(Worklist.Materials(recipes, plan, Bags(3)), item => { Assert.Null(item.Owned); Assert.Null(item.Missing); });
        Assert.Null(Worklist.Materials(recipes, plan, Bags()).Single(i => i.ItemId == 2).Owned);
    }
    [Fact] public void ExcessStockDoesNotProduceNegativeMissingAndCountsAreBounded()
    {
        RecipeEntry[] recipes = [new(1, "Ingot", "Smith", 1, 1, [new(100, "Ore", 1)])];
        Assert.Equal(0L, Assert.Single(Worklist.Materials(recipes, new Dictionary<uint, int> { [1] = 1 }, Bags())).Missing);
        Assert.Equal(Worklist.MaximumCrafts, Assert.Single(Worklist.Materials(recipes, new Dictionary<uint, int> { [1] = int.MaxValue }, Bags())).Required);
        Assert.Empty(Worklist.Materials(recipes, new Dictionary<uint, int> { [1] = -1 }, Bags()));
    }
}
