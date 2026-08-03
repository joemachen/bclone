namespace Bclone.Sim.World;

/// <summary>
/// A load somebody set down on the ground because no store would take it (D96).
/// </summary>
/// <remarks>
/// <para>
/// <b>A genuinely new place for goods to be</b> — not a building, not a larder, not a pair of
/// arms. Joe's rule: <em>villagers may put a load down when no store will take it, and anyone
/// can pick it up again.</em> No decay, and no capacity: it is a heap on the ground.
/// </para>
/// <para>
/// <b>⭐ It is deliberately NOT a <see cref="StoreBuilding"/>, and that is the restraint
/// rather than an omission.</b> D96 took both of the restraints on offer, and the first —
/// <em>goods on the ground are supply-invisible</em> — is bought by this type existing at all.
/// <c>TotalFood</c>, <c>LogsInSheds</c>, <c>FirewoodInSheds</c>, <c>FoodTheVillageHasRoomFor</c>
/// and the labour quota all walk <see cref="Core.SimWorld.StoreBuildings"/>; a heap is not in
/// that list, so <b>not one of them had to be taught to skip it</b>.
/// </para>
/// <para>
/// A fifth <see cref="StoreKind"/> would have been the opposite: found by
/// <c>NearestStoreAccepting</c>, summed by <c>TotalAccepting</c>, counted as room — and every
/// one of those would have needed a new <em>"…except the ground"</em> clause. That is D76's
/// seam in a new costume, and it has already run to five instalments. <b>Supply-invisibility
/// asked as a question is a rule five readers can forget; supply-invisibility as a different
/// list is one nothing can.</b>
/// </para>
/// <para>
/// <b>Why it is not simply lost, which is what happens today.</b> D83 ruled that goods in
/// somebody's arms are not supply, and that is right — but a load on the ground is different,
/// because <em>it is in a place, so it can be walked to</em>. That is the same line D29 draws
/// between a household larder and a store, and a heap lands on the reachable side of it.
/// </para>
/// </remarks>
public sealed class GroundStack
{
    /// <summary>Where it was set down. It does not move until somebody carries it.</summary>
    public required GridPos Position { get; init; }

    /// <summary>What is in the heap. One kind per heap; a tile may hold several.</summary>
    public required Goods Goods { get; init; }

    /// <summary>How much is lying here. Never negative, and a heap at zero is removed.</summary>
    public int Amount { get; set; }
}
