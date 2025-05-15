using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.GameData;

namespace Mushymato.ExtendedTAS;

/// <summary>
/// Randomizing model, used to define upper and lower bounds of randomness on TAS creation.
/// </summary>
public sealed class TASExtRand
{
    public float SortOffset = 0f;
    public float Alpha = 0f;
    public float AlphaFade = 0f;
    public float Scale = 0f;
    public float ScaleChange = 0f;
    public float ScaleChangeChange = 0f;
    public float Rotation = 0f;
    public float RotationChange = 0f;
    public Vector2 Motion = Vector2.Zero;
    public Vector2 Acceleration = Vector2.Zero;
    public Vector2 AccelerationChange = Vector2.Zero;
    public Vector2 PositionOffset = Vector2.Zero;
    public double SpawnInterval = 0;
    public int SpawnDelay = 0;
}

/// <summary>
/// Extends vanilla TemporaryAnimatedSpriteDefinition with more fields + random model
/// </summary>
public sealed class TASExt : TemporaryAnimatedSpriteDefinition
{
    public float ScaleChangeChange = 0f;
    public Vector2 Motion = Vector2.Zero;
    public Vector2 Acceleration = Vector2.Zero;
    public Vector2 AccelerationChange = Vector2.Zero;
    public float? LayerDepth = null;

    // actually opacity
    public float Alpha = 1f;
    public bool PingPong = false;
    public double SpawnInterval = -1;
    public int SpawnDelay = -1;

    internal bool HasRand => RandMin != null && RandMax != null;
    public TASExtRand? RandMin = null;
    public TASExtRand? RandMax = null;
}

/// <summary>
/// A context used to spawn TAS with, holds some overwriting values.
/// </summary>
/// <param name="Def"></param>
internal sealed record TASContext(TASExt Def)
{
    private TimeSpan spawnTimeout = TimeSpan.Zero;
    private TimeSpan gsqTimeout = TimeSpan.Zero;
    internal Vector2 Pos = Vector2.Zero;

    internal Vector2 PosOffsetMin = Vector2.Zero;
    internal Vector2 PosOffsetMax = Vector2.Zero;

    internal int? OverrideLoops = null;
    internal float? OverrideDrawLayer = null;
    internal float? OverrideRotation = null;

    internal HashSet<TemporaryAnimatedSprite> Spawned = [];

    internal bool? GSQState = null;

    // csharpier-ignore
    internal TemporaryAnimatedSprite Create()
    {
        TemporaryAnimatedSprite tas = TemporaryAnimatedSprite.GetTemporaryAnimatedSprite(
            Def.Texture,
            Def.SourceRect,
            Def.Interval,
            Def.Frames,
            OverrideLoops ?? Def.Loops,
            Pos + Random.Shared.NextVector2(PosOffsetMin, PosOffsetMax) + (Def.PositionOffset + (Def.HasRand ? Random.Shared.NextVector2(Def.RandMin!.PositionOffset, Def.RandMax!.PositionOffset) : Vector2.Zero)) * 4f,
            Def.Flicker,
            Def.Flip,
            (OverrideDrawLayer ?? Def.SortOffset) + (Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.SortOffset, Def.RandMax!.SortOffset) : 0),
            Def.AlphaFade + (Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.AlphaFade, Def.RandMax!.AlphaFade) : 0),
            Utility.StringToColor(Def.Color) ?? Color.White,
            (Def.Scale + (Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.Scale, Def.RandMax!.Scale) : 0)) * 4f,
            Def.ScaleChange + (Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.ScaleChange, Def.RandMax!.ScaleChange) : 0),
            (OverrideRotation ?? Def.Rotation) + (Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.Rotation, Def.RandMax!.Rotation) : 0),
            Def.RotationChange + (Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.RotationChange, Def.RandMax!.RotationChange) : 0)
        );
        tas.scaleChangeChange = Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.ScaleChangeChange, Def.RandMax!.ScaleChangeChange) : 0;
        tas.pingPong = Def.PingPong;
        tas.alpha = Def.Alpha + (Def.HasRand ? Random.Shared.NextSingle(Def.RandMin!.Alpha, Def.RandMax!.Alpha) : 0);
        tas.layerDepth = Def.LayerDepth ?? (Pos.Y + 0.66f * Game1.tileSize) / 10000f + Pos.X / Game1.tileSize * 1E-05f;
        tas.motion = Def.Motion + (Def.HasRand ? Random.Shared.NextVector2(Def.RandMin!.Motion, Def.RandMax!.Motion) : Vector2.Zero);
        tas.acceleration = Def.Acceleration + (Def.HasRand ? Random.Shared.NextVector2(Def.RandMin!.Acceleration, Def.RandMax!.Acceleration) : Vector2.Zero);
        tas.accelerationChange = Def.AccelerationChange + (Def.HasRand ? Random.Shared.NextVector2(Def.RandMin!.AccelerationChange, Def.RandMax!.AccelerationChange) : Vector2.Zero);
        return tas;
    }

    private bool TryCreateConditionally(GameStateQueryContext context, [NotNullWhen(true)] out TemporaryAnimatedSprite? tas)
    {
        if (GSQState ??= (GameStateQuery.CheckConditions(Def.Condition, context)))
        {
            tas = Create();
            return true;
        }
        tas = null;
        return false;
    }


    internal bool TryCreate(GameStateQueryContext context, Action<TemporaryAnimatedSprite> addSprite)
    {
        if (TryCreateConditionally(context, out TemporaryAnimatedSprite? tas))
        {
            addSprite(tas);
            return true;
        }
        return false;
    }

    internal bool TryCreateDelayed(GameStateQueryContext context, Action<TemporaryAnimatedSprite> addSprite)
    {
        if (Def.SpawnDelay > 0 && TryCreateConditionally(context, out TemporaryAnimatedSprite? tas))
        {
            DelayedAction.functionAfterDelay(
                () =>
                {
                    tas.endFunction = (extraInfo) => Spawned.Remove(tas);
                    Spawned.Add(tas);
                    addSprite(tas);
                },
                Def.SpawnDelay
                    + (Def.HasRand ? Random.Shared.Next(Def.RandMin!.SpawnDelay, Def.RandMax!.SpawnDelay) : 0)
            );
            return true;
        }
        return false;
    }

    internal bool TryCreateRespawning(
        GameTime time,
        GameStateQueryContext context,
        Action<TemporaryAnimatedSprite> addSprite
    )
    {
        if (spawnTimeout <= TimeSpan.Zero)
        {
            spawnTimeout = TimeSpan.FromMilliseconds(
                Def.SpawnInterval
                    + (
                        Def.HasRand
                            ? Random.Shared.NextDouble(Def.RandMin!.SpawnInterval, Def.RandMax!.SpawnInterval)
                            : 0
                    )
            );
            if (gsqTimeout <= TimeSpan.Zero)
            {
                gsqTimeout = TimeSpan.FromSeconds(1);
                GSQState = null;
            }
            if (TryCreateConditionally(context, out TemporaryAnimatedSprite? tas))
            {
                tas.endFunction = (extraInfo) => Spawned.Remove(tas);
                Spawned.Add(tas);
                addSprite(tas);
                return true;
            }
        }
        gsqTimeout -= time.ElapsedGameTime;
        spawnTimeout -= time.ElapsedGameTime;
        return false;
    }

    internal void RemoveAllSpawned(Func<TemporaryAnimatedSprite, bool> removeSprite)
    {
        foreach (TemporaryAnimatedSprite tas in Spawned)
        {
            removeSprite(tas);
        }
        Spawned.Clear();
    }
}

/// <summary>
/// Manages the TAS custom asset.
/// Even though multiple mod uses this, they must have their own asset name.
/// </summary>
internal sealed class TASAssetManager
{
    private readonly string assetName;
    private readonly IModHelper helper;
    internal string AssetName => assetName;
    internal TASAssetManager(IModHelper helper, string assetName)
    {
        this.helper = helper;
        this.helper.Events.Content.AssetRequested += OnAssetRequested;
        this.helper.Events.Content.AssetsInvalidated += OnAssetInvalidated;
        this.assetName = assetName;
    }

    private Dictionary<string, TASExt>? _tasData = null;
    internal Dictionary<string, TASExt> TASData
    {
        get
        {
            _tasData ??= helper.GameContent.Load<Dictionary<string, TASExt>>(assetName);
            return _tasData;
        }
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(assetName))
            e.LoadFromModFile<Dictionary<string, TASExt>>("assets/tas.json", AssetLoadPriority.Exclusive);
    }

    private void OnAssetInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(an => an.IsEquivalentTo(assetName)))
            _tasData = null;
    }

    public bool TryGetTASExt(string tasId, [NotNullWhen(true)] out TASExt? tasExt)
    {
        if (TASData.TryGetValue(tasId, out tasExt))
        {
            if (tasExt.Frames <= 0 || tasExt.Interval <= 0)
            {
                tasExt = null;
                return false;
            }
            return true;
        }
        return false;
    }

    public IEnumerable<TASExt> GetTASExtList(IEnumerable<string> tasIds)
    {
        foreach (string tasId in tasIds)
        {
            if (TryGetTASExt(tasId, out TASExt? tasExt))
            {
                yield return tasExt;
            }
        }
    }
}
