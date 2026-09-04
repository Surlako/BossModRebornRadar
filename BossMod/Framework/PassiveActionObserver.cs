using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace BossMod;

// Read-only action-state bridge used by the radar build. Unlike ActionManagerEx this class
// never hooks action requests, alters arguments, adjusts locks/cooldowns, or executes actions.
sealed unsafe class PassiveActionObserver : IDisposable
{
    public Event<ulong, ActorCastEvent> ActionEffectReceived = new();

    private readonly ActionManager* _inst = ActionManager.Instance();
    private readonly HookAddress<ActionEffectHandler.Delegates.Receive> _processPacketActionEffectHook;

    public PassiveActionObserver()
    {
        _processPacketActionEffectHook = new(ActionEffectHandler.Addresses.Receive, ProcessPacketActionEffectDetour);
    }

    public void Dispose() => _processPacketActionEffectHook.Dispose();

    public void GetCooldowns(Span<Cooldown> cooldowns)
    {
        var rg = _inst->GetRecastGroupDetail(0);
        var i = 0;
        for (; i < 80; ++i)
            GetCooldown(ref cooldowns[i], rg++);

        rg = _inst->GetRecastGroupDetail(80);
        if (rg != null)
        {
            for (; i < 82; ++i)
                GetCooldown(ref cooldowns[i], rg++);
        }
        else
        {
            for (; i < 82; ++i)
                cooldowns[i] = default;
        }

        rg = _inst->GetRecastGroupDetail(82);
        if (rg != null)
        {
            for (; i < 87; ++i)
                GetCooldown(ref cooldowns[i], rg++);
        }
        else
        {
            for (; i < 87; ++i)
                cooldowns[i] = default;
        }
    }

    public ClientState.DutyAction[] GetDutyActions() =>
        [GetDutyAction(0), GetDutyAction(1), GetDutyAction(2), GetDutyAction(3), GetDutyAction(4)];

    private static void GetCooldown(ref Cooldown result, RecastDetail* data)
    {
        if (data->IsActive)
        {
            result.Elapsed = data->Elapsed;
            result.Total = data->Total;
        }
        else
        {
            result = default;
        }
    }

    private static ClientState.DutyAction GetDutyAction(ushort slot)
    {
        var dm = DutyActionManager.GetInstanceIfReady();
        if (dm == null || !dm->ActionActive[0] || slot >= dm->NumValidSlots)
            return default;

        var charges = slot < 2 ? (dm->CurCharges[slot], dm->MaxCharges[slot]) : default;
        return new(new(ActionType.Spell, dm->ActionId[slot]), charges.Item1, charges.Item2);
    }

    private void ProcessPacketActionEffectDetour(uint casterID, Character* casterObj, Vector3* targetPos, ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects, GameObjectId* targets)
    {
        var info = new ActorCastEvent(new((ActionType)header->ActionType, header->ActionId), header->AnimationTargetId, header->AnimationLock, header->NumTargets, *targetPos,
            header->GlobalSequence, header->SourceSequence, Network.PacketDecoder.IntToFloatAngle(header->RotationInt));
        var rawEffects = (ulong*)effects;
        for (var i = 0; i < header->NumTargets; ++i)
        {
            var targetEffects = new ActionEffects();
            for (var j = 0; j < ActionEffects.MaxCount; ++j)
                targetEffects[j] = rawEffects[i * 8 + j];
            info.Targets.Add(new(targets[i], targetEffects));
        }

        ActionEffectReceived.Fire(casterID, info);
        _processPacketActionEffectHook.Original(casterID, casterObj, targetPos, header, effects, targets);
    }
}
