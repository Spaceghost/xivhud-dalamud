using XivHud.Character;

public sealed class CharacterMathTests
{
    private static (int, int, bool)[] Armour(int level, params int[] skip) =>
        Enumerable.Range(2, 11).Where(slot => !skip.Contains(slot)).Select(slot => (slot, level, false)).ToArray();

    [Fact] public void AverageCountsTwelveSlotsAndExcludesWaistAndSoulCrystal()
    {
        // Main hand + off hand + 10 armour/accessory slots (2–12 minus waist 5).
        var pieces = Armour(600, 5).Append((0, 600, false)).Append((1, 600, false))
            .Append((5, 999, false)).Append((13, 136, false));
        Assert.Equal(600, CharacterMath.AverageItemLevel(pieces));
    }
    [Fact] public void TwoHandedMainHandIsCountedTwice()
    {
        var pieces = Armour(600, 5).Append((0, 660, true));
        Assert.Equal((660 * 2 + 600 * 10) / 12, CharacterMath.AverageItemLevel(pieces));
        Assert.Equal(610, CharacterMath.AverageItemLevel(pieces));
    }
    [Fact] public void OneHandedMainHandWithShieldCountsEachOnce()
    {
        var pieces = Armour(600, 5).Append((0, 660, false)).Append((1, 600, false));
        Assert.Equal(605, CharacterMath.AverageItemLevel(pieces));
    }
    [Fact] public void TwoHandedFlagOnlyDoublesTheMainHandSlot()
    {
        var pieces = Armour(600, 5).Append((0, 600, false)).Append((1, 600, true));
        Assert.Equal(600, CharacterMath.AverageItemLevel(pieces));
    }
    [Theory]
    [InlineData(30)] [InlineData(136)] [InlineData(1)]
    public void SoulCrystalLevelsNeverAffectTheAverage(int crystal) =>
        Assert.Equal(0, CharacterMath.AverageItemLevel([(13, crystal, false)]));
    [Fact] public void EmptyAndInvalidSlotsAreIgnoredAndTheAverageFloors()
    {
        Assert.Equal(0, CharacterMath.AverageItemLevel([]));
        Assert.Equal(0, CharacterMath.AverageItemLevel([(-1, 900, false), (14, 900, false)]));
        Assert.Equal(1, CharacterMath.AverageItemLevel([(0, 23, false)]));
    }

    private static readonly (uint, int)[] Base = [(12, 100), (13, 80), (27, 50), (44, 30), (0, 99), (46, 0)];
    private static readonly (uint, int)[] Special = [(12, 5), (27, 10), (22, 7)];

    [Fact] public void HighQualityBonusAppliesOnlyForSpecialBonusKindOne()
    {
        var stats = CharacterMath.ItemStats(Base, Special, 1, true);
        Assert.Equal(new (uint, int)[] { (12, 105), (13, 80), (27, 60), (44, 30), (22, 7) }, stats);
    }
    [Fact] public void NormalQualityIgnoresTheHighQualityColumns() =>
        Assert.Equal(new (uint, int)[] { (12, 100), (13, 80), (27, 50), (44, 30) }, CharacterMath.ItemStats(Base, Special, 1, false));
    [Theory]
    [InlineData(0u)] [InlineData(2u)] [InlineData(4u)] [InlineData(6u)] [InlineData(7u)]
    [InlineData(8u)] [InlineData(9u)] [InlineData(10u)] [InlineData(11u)]
    public void SetBonusesAndFieldEffectsInTheSameColumnsAreNeverAddedEvenWhenHighQuality(uint kind) =>
        Assert.Equal(new (uint, int)[] { (12, 100), (13, 80), (27, 50), (44, 30) }, CharacterMath.ItemStats(Base, Special, kind, true));
    [Fact] public void DuplicateParamsSumAndCancelledValuesDisappear()
    {
        var stats = CharacterMath.ItemStats([(21, 40), (21, 2), (6, 5)], [(6, -5)], 1, true);
        Assert.Equal(new (uint, int)[] { (21, 42) }, stats);
    }
    [Theory]
    [InlineData(12u, true)] [InlineData(13u, true)] [InlineData(17u, true)] [InlineData(18u, true)]
    [InlineData(21u, true)] [InlineData(24u, true)] [InlineData(27u, false)] [InlineData(1u, false)] [InlineData(14u, false)]
    public void WeaponAndArmourColumnsAreTheHeadlineFigures(uint id, bool primary) =>
        Assert.Equal(primary, CharacterMath.IsPrimaryItemStat(id));

    [Theory]
    [InlineData(1, 1, "1st Sun of the 1st Astral Moon")]
    [InlineData(2, 32, "32nd Sun of the 1st Umbral Moon")]
    [InlineData(3, 2, "2nd Sun of the 2nd Astral Moon")]
    [InlineData(11, 13, "13th Sun of the 6th Astral Moon")]
    [InlineData(12, 11, "11th Sun of the 6th Umbral Moon")]
    [InlineData(6, 23, "23rd Sun of the 3rd Umbral Moon")]
    public void NamedayUsesTheEorzeanCalendar(int month, int day, string expected) =>
        Assert.Equal(expected, CharacterMath.Nameday(month, day));
    [Theory]
    [InlineData(0, 1)] [InlineData(13, 1)] [InlineData(1, 0)] [InlineData(1, 33)] [InlineData(-1, -1)]
    public void NamedayRejectsDaysOutsideTheCalendar(int month, int day) => Assert.Equal("", CharacterMath.Nameday(month, day));
    [Theory]
    [InlineData(1, "1st")] [InlineData(2, "2nd")] [InlineData(3, "3rd")] [InlineData(4, "4th")]
    [InlineData(11, "11th")] [InlineData(12, "12th")] [InlineData(13, "13th")]
    [InlineData(21, "21st")] [InlineData(22, "22nd")] [InlineData(23, "23rd")] [InlineData(31, "31st")]
    [InlineData(101, "101st")] [InlineData(111, "111th")] [InlineData(112, "112th")] [InlineData(0, "0th")]
    public void OrdinalsHandleTheTeens(int value, string expected) => Assert.Equal(expected, CharacterMath.Ordinal(value));

    [Theory]
    [InlineData(50, 100, .5f)] [InlineData(150, 100, 1f)] [InlineData(-5, 100, 0f)] [InlineData(5, 0, 1f)] [InlineData(5, -1, 1f)] [InlineData(0, 100, 0f)]
    public void ProgressClamps(long current, long needed, float expected) => Assert.Equal(expected, CharacterMath.Progress(current, needed));

    [Theory]
    [InlineData("white mage", "White Mage")] [InlineData("gladiator", "Gladiator")] [InlineData("Paladin", "Paladin")]
    [InlineData("", "")] [InlineData("dark knight", "Dark Knight")] [InlineData("one-handed", "One-Handed")]
    public void JobNamesAreTitleCased(string text, string expected) => Assert.Equal(expected, CharacterMath.TitleCase(text));

    // Live ClassJob rows (role, category, jobIndex, parent, id, limited, abbreviation) from the installed sheet.
    [Theory]
    [InlineData(1, 30u, 1, 1u, 19u, false, "PLD", JobSection.Tank)]
    [InlineData(1, 30u, 12, 32u, 32u, false, "DRK", JobSection.Tank)]
    [InlineData(1, 30u, 17, 37u, 37u, false, "GNB", JobSection.Tank)]
    [InlineData(4, 31u, 6, 6u, 24u, false, "WHM", JobSection.Healer)]
    [InlineData(4, 31u, 9, 26u, 28u, false, "SCH", JobSection.Healer)]
    [InlineData(4, 31u, 13, 33u, 33u, false, "AST", JobSection.Healer)]
    [InlineData(2, 30u, 10, 29u, 30u, false, "NIN", JobSection.Melee)]
    [InlineData(2, 30u, 21, 41u, 41u, false, "VPR", JobSection.Melee)]
    [InlineData(3, 30u, 5, 5u, 23u, false, "BRD", JobSection.Ranged)]
    [InlineData(3, 30u, 11, 31u, 31u, false, "MCH", JobSection.Ranged)]
    [InlineData(3, 31u, 8, 26u, 27u, false, "SMN", JobSection.Magic)]
    [InlineData(3, 31u, 22, 42u, 42u, false, "PCT", JobSection.Magic)]
    [InlineData(3, 31u, 16, 36u, 36u, true, "BLU", JobSection.Limited)]
    [InlineData(2, 30u, 23, 43u, 43u, true, "BST", JobSection.Limited)]
    [InlineData(0, 33u, 0, 8u, 8u, false, "CRP", JobSection.Hand)]
    [InlineData(0, 33u, 0, 15u, 15u, false, "CUL", JobSection.Hand)]
    [InlineData(0, 32u, 0, 16u, 16u, false, "MIN", JobSection.Land)]
    [InlineData(0, 32u, 0, 18u, 18u, false, "FSH", JobSection.Land)]
    public void JobsSitInTheirOwnRoleSection(byte role, uint category, byte jobIndex, uint parent, uint id, bool limited, string abbreviation, JobSection expected) =>
        Assert.Equal(expected, CharacterMath.SectionFor(role, category, jobIndex, parent, id, limited, abbreviation));
    [Theory]
    [InlineData(1, 30u, 0, 1u, 1u, "GLA")]
    [InlineData(3, 31u, 0, 26u, 26u, "ACN")]
    [InlineData(2, 30u, 0, 29u, 29u, "ROG")]
    [InlineData(3, 30u, 0, 5u, 5u, "ARC")]
    [InlineData(4, 31u, 0, 6u, 6u, "CNJ")]
    [InlineData(0, 30u, 0, 0u, 0u, "")]      // ADV
    [InlineData(0, 0u, 0, 0u, 44u, "")]      // blank row 44 (ExpArrayIndex 0)
    [InlineData(0, 0u, 0, 0u, 45u, "  ")]    // blank row 45
    public void BaseClassesFoldIntoTheirJobsAndBlankRowsVanish(byte role, uint category, byte jobIndex, uint parent, uint id, string abbreviation) =>
        Assert.Null(CharacterMath.SectionFor(role, category, jobIndex, parent, id, false, abbreviation));
    [Theory]
    [InlineData(1, 30u, JobSection.Tank)]    // GLA, MRD
    [InlineData(2, 30u, JobSection.Melee)]   // PGL, LNC, ROG
    [InlineData(3, 30u, JobSection.Ranged)]  // ARC
    [InlineData(3, 31u, JobSection.Magic)]   // THM, ACN: a caster although Scholar is a healer
    [InlineData(4, 31u, JobSection.Healer)]  // CNJ
    public void ABaseClassShownForALockedJobUsesItsOwnRole(byte role, uint category, JobSection expected) =>
        Assert.Equal(expected, CharacterMath.RoleSection(role, category));
    [Fact] public void RoleZeroHasNoCombatSection() => Assert.Null(CharacterMath.RoleSection(0, 30));
    [Fact] public void OnlyAJobWithALockedUnlockQuestIsShownAsItsBaseClass()
    {
        Assert.True(CharacterMath.ShowAsBaseClass(19, 1, false));   // Paladin before its quest: Gladiator
        Assert.False(CharacterMath.ShowAsBaseClass(19, 1, true));   // Paladin unlocked
        Assert.True(CharacterMath.ShowAsBaseClass(28, 26, false));  // Scholar before its quest: Arcanist
        Assert.False(CharacterMath.ShowAsBaseClass(32, 32, false)); // Dark Knight has no base class
        Assert.False(CharacterMath.ShowAsBaseClass(8, 8, false));   // Carpenter
        Assert.False(CharacterMath.ShowAsBaseClass(0, 0, false));
    }
    [Theory]
    [InlineData(19u, 100, 0L, true)]          // ParamGrow[101] is empty: level 100 is the cap
    [InlineData(19u, 99, 29023000L, false)]   // ParamGrow[100] is non-zero although 100 is the cap
    [InlineData(36u, 80, 6400000L, true)]     // Blue Mage cap is not in the sheet
    [InlineData(36u, 79, 5992000L, false)]
    [InlineData(19u, 0, 300L, false)]         // locked
    public void MaxLevelUsesTheFollowingGrowthRowAndBlueMageCap(uint id, int level, long following, bool expected) =>
        Assert.Equal(expected, CharacterMath.IsMaxLevel(id, level, following));

    [Fact] public void CombatSectionsFollowBaseParamOrderPriority()
    {
        var sections = CharacterMath.Sections(StatLayout.Combat);
        Assert.Equal(["Attributes", "Offensive Properties", "Defensive Properties", "Physical Properties", "Mental Properties", "Role"],
            sections.Select(s => s.Section));
        Assert.Equal(new uint[] { 1, 2, 3, 4, 5 }, sections[0].BaseParams);
        Assert.Equal(new uint[] { 27, 44, 22 }, sections[1].BaseParams); // Critical Hit 19, Determination 20, Direct Hit 23
        Assert.Equal(new uint[] { 21, 24 }, sections[2].BaseParams);
        Assert.Equal(new uint[] { 20, 45 }, sections[3].BaseParams);
        Assert.Equal(new uint[] { 33, 34, 46 }, sections[4].BaseParams);
        Assert.Equal(new uint[] { 19, 6 }, sections[5].BaseParams);
    }
    [Fact] public void CrafterAndGathererLeadWithTheirOwnDisciplines()
    {
        var crafter = CharacterMath.Sections(StatLayout.Crafter);
        Assert.Equal("Crafting", crafter[0].Section); Assert.Equal(new uint[] { 70, 71, 11 }, crafter[0].BaseParams);
        var gatherer = CharacterMath.Sections(StatLayout.Gatherer);
        Assert.Equal("Gathering", gatherer[0].Section); Assert.Equal(new uint[] { 72, 73, 10 }, gatherer[0].BaseParams);
        Assert.All([crafter, gatherer], layout => Assert.Equal(new uint[] { 1, 2, 3, 4, 5 }, layout[1].BaseParams));
    }
    [Theory]
    [InlineData(StatLayout.Combat)] [InlineData(StatLayout.Crafter)] [InlineData(StatLayout.Gatherer)]
    public void NoLegacyResistancesOrDuplicatesAreShown(StatLayout layout)
    {
        var ids = CharacterMath.Sections(layout).SelectMany(s => s.BaseParams).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.DoesNotContain(ids, id => id is >= 29 and <= 32 or >= 37 and <= 43);
        Assert.All(ids, id => Assert.InRange(id, 1u, 73u));
    }
    [Theory]
    [InlineData(33u, StatLayout.Crafter, "CP")] [InlineData(32u, StatLayout.Gatherer, "GP")]
    [InlineData(30u, StatLayout.Combat, "MP")] [InlineData(31u, StatLayout.Combat, "MP")] [InlineData(0u, StatLayout.Combat, "MP")]
    public void LayoutAndSecondaryPoolFollowTheCategory(uint category, StatLayout layout, string pool)
    {
        Assert.Equal(layout, CharacterMath.LayoutFor(category));
        Assert.Equal(pool, CharacterMath.PoolLabel(category));
    }
    [Theory]
    [InlineData(1u, 1u, 1, true)] [InlineData(19u, 1u, 1, true)] [InlineData(6u, 1u, 1, false)]   // Paladin: STR, Tenacity
    [InlineData(5u, 5u, 4, true)] [InlineData(6u, 5u, 4, true)] [InlineData(19u, 5u, 4, false)]   // White Mage: MND, Piety
    [InlineData(2u, 2u, 3, true)] [InlineData(19u, 2u, 3, false)] [InlineData(6u, 2u, 3, false)]  // Bard: DEX only
    [InlineData(0u, 0u, 0, false)]
    public void PrimaryAndRoleStatsAreEmphasized(uint stat, uint primary, byte role, bool expected) =>
        Assert.Equal(expected, CharacterMath.Emphasized(stat, primary, role));

    [Fact] public void EquipmentListGroupsArmsAndAccessoriesWithoutTheWaist()
    {
        Assert.Equal(13, CharacterMath.EquipmentOrder.Length);
        Assert.DoesNotContain(CharacterMath.Waist, CharacterMath.EquipmentOrder);
        Assert.Equal(Enumerable.Range(0, 14).Where(i => i != 5), CharacterMath.EquipmentOrder.Order());
        Assert.Equal(["Main Hand", "Off Hand", "Head", "Body", "Hands", "Legs", "Feet"],
            CharacterMath.EquipmentOrder.Take(CharacterMath.EquipmentHalf).Select(i => CharacterMath.SlotNames[i]));
        Assert.Equal("Soul Crystal", CharacterMath.SlotNames[CharacterMath.EquipmentOrder[^1]]);
    }
    [Theory]
    [InlineData(0, 1, 7)] [InlineData(5, 1, 12)] [InlineData(6, 1, 12)] [InlineData(7, -1, 0)]
    [InlineData(12, -1, 5)] [InlineData(3, -1, 3)] [InlineData(9, 1, 9)] [InlineData(-1, 1, 0)]
    public void LeftAndRightJumpBetweenTheEquipmentGroups(int index, int direction, int expected) =>
        Assert.Equal(expected, CharacterMath.EquipmentJump(index, direction));
    [Theory]
    [InlineData(0, 1, 5, 1)] [InlineData(4, 1, 5, 4)] [InlineData(0, -1, 5, 0)] [InlineData(2, 6, 30, 8)]
    [InlineData(28, 6, 30, 29)] [InlineData(-1, 1, 3, 0)] [InlineData(3, 1, 0, -1)]
    public void SelectionMovesWithinTheList(int index, int delta, int count, int expected) =>
        Assert.Equal(expected, CharacterMath.Move(index, delta, count));
    [Fact] public void GearSetRefusalIsExplainedGently()
    {
        Assert.Contains("can't be equipped right now", CharacterMath.GearSetResult(3, -1));
        Assert.StartsWith("Changing to gear set 3", CharacterMath.GearSetResult(3, 0));
    }
}
