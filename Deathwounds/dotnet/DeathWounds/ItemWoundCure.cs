using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace DeathWounds;

/// <summary>Consumable cure (leg/torso/…) for random death-touched wound line in that <see cref="WoundBodyPart"/>.</summary>
public class ItemWoundCure : Item
{
    public override void OnHeldInteractStop(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel)
    {
        base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);

        if (secondsUsed < 2.95f)
        {
            return;
        }

        if (byEntity is EntityPlayer eplr &&
            eplr.Player is IServerPlayer splayer &&
            byEntity.Api.Side == EnumAppSide.Server)
        {
            WoundBodyPart? part = WoundCurePartFromItem(this);
            if (part is not null)
            {
                Random rnd = splayer.Entity.World.Rand;
                if (DeathWoundDebuffSystem.TryCureOneRandomWoundInPart(splayer, part.Value, rnd))
                {
                    DeathWoundDebuffSystem.SendChat(splayer, Lang.Get("deathwounds:woundcure-heal-chat"));
                }
                else
                {
                    DeathWoundDebuffSystem.SendChat(splayer, Lang.Get("deathwounds:woundcure-nothing-to-cure"));
                }
            }
        }
    }

    private static WoundBodyPart? WoundCurePartFromItem(Item it)
    {
        if (it.Attributes?["woundCurePart"].AsString() is { } s)
        {
            if (string.Equals(s, "leg", StringComparison.OrdinalIgnoreCase))
            {
                return WoundBodyPart.Leg;
            }

            if (string.Equals(s, "arm", StringComparison.OrdinalIgnoreCase))
            {
                return WoundBodyPart.Arm;
            }

            if (string.Equals(s, "torso", StringComparison.OrdinalIgnoreCase))
            {
                return WoundBodyPart.Torso;
            }

            if (string.Equals(s, "head", StringComparison.OrdinalIgnoreCase))
            {
                return WoundBodyPart.Head;
            }
        }

        if (it.Code is null)
        {
            return null;
        }

        string? last = it.Code.Path.Split('-')[^1];
        if (string.Equals(last, "leg", StringComparison.OrdinalIgnoreCase))
        {
            return WoundBodyPart.Leg;
        }

        if (string.Equals(last, "torso", StringComparison.OrdinalIgnoreCase))
        {
            return WoundBodyPart.Torso;
        }

        return null;
    }
}
