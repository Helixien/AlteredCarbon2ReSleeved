﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace AlteredCarbon
{
    public enum ConnectStatus
    {
        Connectable, OutOfRange, TargetDead, CasterDead, LowConscioussness, NoConnectedMatrix, 
        MatrixHasNoPower, Lost, StoppedManually, ConnectionDisrupted, SwitchedFaction,
        StackDestroyed
    }

    public interface INeedlecastable
    {
        public Thing ThingHolder { get; }
        public Dictionary<Pawn, ConnectStatus> GetAllConnectablePawns();
        public bool Needlecasting { get; }
        public Hediff_RemoteStack NeedleCastingInto {  get; }
        public void NeedlecastTo(LocalTargetInfo target);
        public NeuralData NeuralData { get; set; }
    }

    [HotSwappable]
    public class Hediff_NeuralStack : Hediff_Implant, IStackHolder, INeedlecastable
    {
        public Ability_ArchotechStackSkip skipAbility;
        public ThingDef SourceStack
        {
            get
            {
                if (this.def == AC_DefOf.AC_NeuralStack)
                {
                    return AC_DefOf.AC_ActiveNeuralStack;
                }
                return AC_DefOf.AC_ActiveArchotechStack;
            }
        }
        private NeuralData neuralData;
        public NeuralData NeuralData
        {
            get
            {
                if (neuralData is null)
                {
                    neuralData = new NeuralData();
                    neuralData.CopyFromPawn(pawn, SourceStack, copyRaceGenderInfo: true);
                }
                return neuralData;
            }
            set
            {
                if (value.hostPawn != null)
                {
                    value.CopyFromPawn(value.hostPawn, value.sourceStack);
                    AC_Utils.DebugMessage("Copying from pawn: " + value.name.ToStringShort);
                }
                neuralData = value;
            }
        }

        public Thing ThingHolder => this.pawn;
        public Pawn Pawn => this.pawn;
        public Hediff_RemoteStack needleCastingInto;
        public Hediff_RemoteStack NeedleCastingInto => needleCastingInto;
        public bool Needlecasting => needleCastingInto != null;
        public ThingStyleDef savedStyle;
        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (this.def == AC_DefOf.AC_ArchotechStack)
            {
                if (skipAbility.ShowGizmoOnPawn())
                {
                    yield return skipAbility.GetGizmo();
                }
            }
            if (Needlecasting)
            {
                yield return new Command_Action
                {
                    defaultLabel = "AC.EndNeedlecasting".Translate(),
                    defaultDesc = "AC.EndNeedlecastingDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Gizmos/EndNeedlecasting"),
                    action = delegate
                    {
                        needleCastingInto.EndNeedlecasting(ConnectStatus.StoppedManually);
                    }
                };
            }
            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Duplicate stack",
                    action = () =>
                    {
                        Recipe_DuplicateNeuralStack.DuplicateStack(pawn.Position + pawn.Rotation.FacingCell, pawn.Map, NeuralData);
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Duplicate pawn",
                    action = () =>
                    {
                        var copy = AC_Utils.ClonePawn(pawn);
                        var copyStack = Recipe_DuplicateNeuralStack.DuplicateStack(pawn.Position + pawn.Rotation.FacingCell, pawn.Map, NeuralData);
                        Recipe_InstallActiveNeuralStack.ApplyNeuralStack(def, copy, copy.GetNeck(), copyStack);
                        copyStack.DestroyNoKill();
                        GenPlace.TryPlaceThing(copy, pawn.Position + pawn.Rotation.FacingCell, pawn.Map, ThingPlaceMode.Near);
                    }
                };
            }
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());
            NeuralData.AppendDebugData(sb, pawn);
            return sb.ToString().TrimEndNewlines();
        }

        public IEnumerable<Gizmo> GetNeedleCastingGizmos()
        {
            if (AC_DefOf.AC_NeuralCasting.IsFinished is false)
            {
                yield break;
            }
            if (pawn.ParentHolder is Building_CryptosleepCasket && pawn.IsColonist || pawn.IsColonistPlayerControlled)
            {
                if (Needlecasting)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "AC.EndNeedlecasting".Translate(),
                        defaultDesc = "AC.EndNeedlecastingDesc".Translate(),
                        icon = ContentFinder<Texture2D>.Get("UI/Gizmos/EndNeedlecasting"),
                        action = delegate
                        {
                            needleCastingInto.EndNeedlecasting(ConnectStatus.StoppedManually);
                        }
                    };
                }
                else
                {
                    yield return new Command_NeedlecastAction(this.pawn, new CommandInfo
                    {
                        icon = "UI/Gizmos/Needlecasting",
                        action = NeedlecastTo
                    })
                    {
                        defaultLabel = "AC.StartNeedlecasting".Translate(),
                        defaultDesc = "AC.StartNeedlecastingDesc".Translate()
                    };
                }
            }
        }

        public void NeedlecastTo(LocalTargetInfo target)
        {
            needleCastingInto = target.Pawn.GetRemoteStack();
            NeuralData.CopyFromPawn(pawn, SourceStack);
            needleCastingInto.Needlecast(this);
            pawn.health.AddHediff(AC_DefOf.AC_NeedlecastingStasis);
        }

        public Dictionary<Pawn, ConnectStatus> GetAllConnectablePawns()
        {
            return AC_Utils.GetAllConnectablePawnsFor(this);
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            this.Part = pawn.GetNeck();
            base.PostAdd(dinfo);
            var stackDef = SourceStack;
            var ideo = pawn.Faction?.ideos.PrimaryIdeo;
            var category = ideo?.GetStyleAndCategoryFor(stackDef);
            if (category?.styleDef != null)
            {
                savedStyle = category.styleDef;
            }
            foreach (var hediff in pawn.health.hediffSet.hediffs.ToList())
            {
                if (hediff != this && hediff is Hediff_NeuralStack otherStack)
                {
                    otherStack.preventKill = otherStack.preventSpawningStack = true;
                    pawn.health.RemoveHediff(otherStack);
                    otherStack.preventKill = otherStack.preventSpawningStack = false;
                }
            }

            var emptySleeveHediff = pawn.GetHediff(AC_DefOf.AC_EmptySleeve);
            if (emptySleeveHediff != null)
            {
                pawn.health.RemoveHediff(emptySleeveHediff);
            }
            if (AlteredCarbonManager.Instance.PawnsWithStacks.Contains(pawn) is false)
            {
                AlteredCarbonManager.Instance.RegisterPawn(pawn);
                AlteredCarbonManager.Instance.TryAddRelationships(pawn, this.NeuralData.StackGroupData);
            }
            CreateSkipAbilityIfMissing();
        }

        private void CreateSkipAbilityIfMissing()
        {
            if (this.def == AC_DefOf.AC_ArchotechStack && skipAbility is null)
            {
                skipAbility = (Ability_ArchotechStackSkip)Activator.CreateInstance(AC_DefOf.AC_ArchotechStackSkip.abilityClass);
                skipAbility.def = AC_DefOf.AC_ArchotechStackSkip;
                skipAbility.holder = this.pawn;
                skipAbility.pawn = this.pawn;
                skipAbility.Init();
            }
        }

        public override bool ShouldRemove => false;

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            if (!NeuralData.ContainsData)
            {
                NeuralData.CopyFromPawn(this.pawn, SourceStack);
            }
            base.Notify_PawnDied(dinfo, culprit);
        }

        public override void Notify_PawnKilled()
        {
            if (!NeuralData.ContainsData)
            {
                NeuralData.CopyFromPawn(this.pawn, SourceStack);
            }
            base.Notify_PawnKilled();
        }

        public bool preventKill;
        public override void PostRemoved()
        {
            base.PostRemoved();

            if (Needlecasting)
            {
                needleCastingInto.EndNeedlecasting(ConnectStatus.ConnectionDisrupted);
            }

            if (!preventKill && !this.pawn.Dead)
            {
                this.pawn.Kill(null);
            }

            if (this.def == AC_DefOf.AC_ArchotechStack)
            {
                if (!preventSpawningStack)
                {
                    SpawnStack(placeMode: ThingPlaceMode.Near);
                }
                pawn.health.hediffSet.hediffs.RemoveAll(x => x.def.defName == "VPE_PsycastAbilityImplant");
                pawn.health.hediffSet.hediffs.RemoveAll(x => x.def == HediffDefOf.PsychicAmplifier);
            }

            var group = NeuralData.StackGroupData;
            group.copiedPawns.Remove(this.pawn);
            if (group.originalPawn == this.pawn)
            {
                group.originalPawn = null;
            }
        }

        public bool preventSpawningStack;
        public NeuralStack SpawnStack(bool destroyPawn = false, ThingPlaceMode placeMode = ThingPlaceMode.Near, 
            Caravan caravan = null, bool psycastEffect = false, Map mapToSpawn = null)
        {
            if (preventSpawningStack)
            {
                return null;
            }
            preventSpawningStack = true;
            try
            {
                float partHealth = pawn.health.hediffSet.GetPartHealth(part);
                if (partHealth <= 0)
                {
                    preventSpawningStack = false;
                    return null;
                }
                if (def != AC_DefOf.AC_ArchotechStack && placeMode == ThingPlaceMode.Direct
                    && pawn.Corpse is Corpse corpse && corpse.Destroyed is false)
                {
                    placeMode = ThingPlaceMode.Near;
                }
                var healthRatio = partHealth / part.def.GetMaxHealth(pawn);
                var stackDef = SourceStack;
                var neuralStack = ThingMaker.MakeThing(stackDef) as NeuralStack;
                if (savedStyle != null)
                {
                    neuralStack.SetStyleDef(savedStyle);
                }
                if (neuralStack.IsArchotechStack is false)
                {
                    neuralStack.HitPoints = (int)(neuralStack.MaxHitPoints * healthRatio);
                }
                neuralStack.NeuralData.CopyFromPawn(this.pawn, stackDef);
                neuralStack.NeuralData.CopyOriginalData(NeuralData);
                mapToSpawn ??= this.pawn.MapHeld;
                if (mapToSpawn != null)
                {
                    GenPlace.TryPlaceThing(neuralStack, this.pawn.PositionHeld, (Map)mapToSpawn, placeMode);
                    if (psycastEffect)
                    {
                        FleckMaker.Static(neuralStack.Position, neuralStack.Map, AC_DefOf.PsycastAreaEffect, 3f);
                    }
                }
                else if (caravan != null)
                {
                    CaravanInventoryUtility.GiveThing(caravan, neuralStack);
                }
                var degradationHediff = pawn.health.hediffSet.GetFirstHediff<Hediff_StackDegradation>();
                if (degradationHediff != null)
                {
                    neuralStack.NeuralData.stackDegradation = degradationHediff.stackDegradation;
                    pawn.health.RemoveHediff(degradationHediff);
                }
                pawn.health.RemoveHediff(this);
                AlteredCarbonManager.Instance.RegisterStack(neuralStack);
                AlteredCarbonManager.Instance.RegisterExtractedStack(this.pawn, neuralStack);
                AlteredCarbonManager.Instance.ReplacePawnWithStack(pawn, neuralStack);
                AlteredCarbonManager.Instance.deadPawns.Add(pawn);
                neuralStack.NeuralData.hostPawn = null;
                if (LookTargets_Patch.targets.TryGetValue(pawn, out var targets))
                {
                    foreach (var target in targets)
                    {
                        target.targets.Remove(pawn);
                        target.targets.Add(neuralStack);
                    }
                }
                if (destroyPawn)
                {
                    if (this.pawn.Dead)
                    {
                        this.pawn.Corpse.Destroy();
                    }
                    else
                    {
                        this.pawn.Destroy();
                    }
                }
                return neuralStack;
            }
            catch (Exception ex)
            {
                Log.Error("Error spawning stack: " + this + " - " + ex.ToString());
            }
            preventSpawningStack = false;
            return null;
        }

        private ThingStyleDef GetStyleDef(ThingDef stackDef, NeuralStack neuralStack)
        {
            return null;
        }

        [HarmonyPatch(typeof(HediffSet), "ExposeData")]
        public static class HediffSet_ExposeData_Patch
        {
            public static Pawn curPawn;

            public static void Postfix(HediffSet __instance)
            {
                curPawn = __instance.pawn;
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref neuralData, "neuralData");
            Scribe_Deep.Look(ref skipAbility, "skipAbility");
            Scribe_References.Look(ref needleCastingInto, "needleCastingInto");
            Scribe_Defs.Look(ref savedStyle, "savedStyle");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                CreateSkipAbilityIfMissing();
            }
            var curPawn = this.pawn ?? HediffSet_ExposeData_Patch.curPawn;
            HediffSet_ExposeData_Patch.curPawn = null;
            if (skipAbility != null)
            {
                skipAbility.holder = curPawn;
                skipAbility.pawn = curPawn;
            }
        }
    }
}