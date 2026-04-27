using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace DeathWounds;

/// <summary>
/// Survival death wounds: random leg (move) or torso (hunger) tier. Save keys stay compatible with the old combined mod.
/// </summary>
public sealed class DeathWoundDebuffSystem : ModSystem
{
    public static readonly WoundLine[] WoundLines =
    {
        new(AttrMoveTiers, WoundBodyPart.Leg, "move"),
        new(AttrTorsoHungerTiers, WoundBodyPart.Torso, "hunger"),
    };

    public static readonly string[] DeathDebuffTypeAttrKeys = { AttrMoveTiers, AttrTorsoHungerTiers };

    /// <summary>Legacy key name (unchanged) so existing worlds keep wound tiers.</summary>
    public const string AttrMoveTiers = "traitcoreDebuffMoveTiers";

    /// <summary>Legacy key name; was stamina tiers, now hunger rate wound.</summary>
    public const string AttrTorsoHungerTiers = "traitcoreDebuffStaminaTiers";

    public const string AttrChatOnRespawn = "traitcoreDebuffOnDeathChatPending";

    internal const string MoveDebuffStatId = "deathwounds-debuff-move";
    internal const string TorsoHungerDebuffStatId = "deathwounds-debuff-torso-hunger";

    internal const float DebuffPerTier = 0.25f;
    private const int MaxTiers = 3;

    private ICoreServerAPI? sapi;

    public override void Start(ICoreAPI api)
    {
        api.RegisterItemClass("ItemWoundCure", typeof(ItemWoundCure));
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        api.Event.PlayerDeath += OnPlayerDeath;
        api.Event.PlayerRespawn += OnPlayerRespawn;
        api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
    }

    public override void Dispose()
    {
        if (sapi is not null)
        {
            sapi.Event.PlayerDeath -= OnPlayerDeath;
            sapi.Event.PlayerRespawn -= OnPlayerRespawn;
            sapi.Event.PlayerNowPlaying -= OnPlayerNowPlaying;
        }
    }

    private void OnPlayerDeath(IServerPlayer byPlayer, DamageSource _)
    {
        TryIncrementRandomDebuffOnDeath(byPlayer);
    }

    private void OnPlayerRespawn(IServerPlayer player)
    {
        TrySendAndClearWoundMessageIfPending(player);
        ApplyDebuffs(player);
    }

    private void OnPlayerNowPlaying(IServerPlayer player)
    {
        TrySendAndClearWoundMessageIfPending(player);
        ApplyDebuffs(player);
    }

    public static void ApplyDebuffs(IServerPlayer player)
    {
        ApplyMovementDebuffStats(player);
        ApplyTorsoHungerDebuffStats(player);
    }

    public static void SendChat(IServerPlayer player, string message)
    {
        player.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.CommandSuccess);
    }

    private void TryIncrementRandomDebuffOnDeath(IServerPlayer player)
    {
        if (player?.Entity is null)
        {
            return;
        }

        if (player.WorldData?.CurrentGameMode != EnumGameMode.Survival)
        {
            return;
        }

        var a = player.Entity.WatchedAttributes;
        var openLines = new List<string>(DeathDebuffTypeAttrKeys.Length);
        for (int i = 0; i < WoundLines.Length; i++)
        {
            string key = WoundLines[i].AttrKey;
            if (GetTierCount(player, key) < MaxTiers)
            {
                openLines.Add(key);
            }
        }

        if (openLines.Count == 0)
        {
            return;
        }

        int roll = sapi is not null
            ? sapi.World.Rand.Next(openLines.Count)
            : 0;
        string chosenAttr = openLines[roll];
        a.SetInt(chosenAttr, GetTierCount(player, chosenAttr) + 1);
        a.MarkPathDirty(chosenAttr);

        a.SetInt(AttrChatOnRespawn, 1);
        a.MarkPathDirty(AttrChatOnRespawn);

        ApplyDebuffs(player);
    }

    public static int GetMoveTierCount(IServerPlayer? player) =>
        GetTierCount(player, AttrMoveTiers);

    public static int GetTorsoHungerTierCount(IServerPlayer? player) =>
        GetTierCount(player, AttrTorsoHungerTiers);

    public static int GetStaminaTierCount(IServerPlayer? player) => GetTorsoHungerTierCount(player);

    private static int GetTierCount(IServerPlayer? player, string key)
    {
        if (player?.Entity is null)
        {
            return 0;
        }

        int t = player.Entity.WatchedAttributes.GetInt(key, 0);
        if (t < 0)
        {
            return 0;
        }

        if (t > MaxTiers)
        {
            return MaxTiers;
        }

        return t;
    }

    private static void TrySendAndClearWoundMessageIfPending(IServerPlayer player)
    {
        if (player?.Entity is null)
        {
            return;
        }

        var a = player.Entity.WatchedAttributes;
        if (a.GetInt(AttrChatOnRespawn, 0) == 0)
        {
            return;
        }

        a.SetInt(AttrChatOnRespawn, 0);
        a.MarkPathDirty(AttrChatOnRespawn);

        SendChat(player, Lang.Get("deathwounds:deathdebuff-respawn-bodywarn"));
    }

    private static void ApplyMovementDebuffStats(IServerPlayer player)
    {
        int m = GetMoveTierCount(player);
        Entity e = player.Entity;
        if (e is null)
        {
            return;
        }

        e.Stats.Remove("walkspeed", MoveDebuffStatId);
        e.Stats.Remove("sprintSpeed", MoveDebuffStatId);
        if (m > 0)
        {
            float p = -DebuffPerTier * m;
            e.Stats.Set("walkspeed", MoveDebuffStatId, p, true);
            e.Stats.Set("sprintSpeed", MoveDebuffStatId, p, true);
        }
    }

    private static void ApplyTorsoHungerDebuffStats(IServerPlayer player)
    {
        int t = GetTorsoHungerTierCount(player);
        if (player.Entity is null)
        {
            return;
        }

        player.Entity.Stats.Remove("hungerrate", TorsoHungerDebuffStatId);
        if (t > 0)
        {
            player.Entity.Stats.Set("hungerrate", TorsoHungerDebuffStatId, DebuffPerTier * t, true);
        }
    }

    public static bool TryCureOneRandomWoundInPart(IServerPlayer? player, WoundBodyPart part, Random? rnd = null)
    {
        if (player?.Entity is null)
        {
            return false;
        }

        var candidates = new List<string>(4);
        for (int i = 0; i < WoundLines.Length; i++)
        {
            WoundLine wl = WoundLines[i];
            if (wl.Part != part)
            {
                continue;
            }

            if (GetTierCount(player, wl.AttrKey) > 0)
            {
                candidates.Add(wl.AttrKey);
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        Random r = rnd ?? new Random();
        string key = candidates[r.Next(candidates.Count)];
        int tier = GetTierCount(player, key) - 1;
        var a = player.Entity.WatchedAttributes;
        a.SetInt(key, Math.Max(0, tier));
        a.MarkPathDirty(key);
        ApplyDebuffs(player);
        return true;
    }
}
