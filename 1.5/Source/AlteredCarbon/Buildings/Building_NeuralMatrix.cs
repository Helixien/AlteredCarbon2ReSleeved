﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace AlteredCarbon
{
    [HotSwappable]
    public class Building_NeuralMatrix : Building
    {
        public CompPowerTrader compPower;
        public CompNeuralCache compCache;
        public CompFacility compFacility;
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            compPower = this.TryGetComp<CompPowerTrader>();
            compCache = this.TryGetComp<CompNeuralCache>();
            compFacility = this.TryGetComp<CompFacility>();
        }

        public bool Powered => this.compPower.PowerOn;
        public float NeedleCastRange()
        {
            var boost = 1f;
            if (Powered)
            {
                foreach (var linked in LinkedBuildings.Where(x => x.def == AC_DefOf.AC_CastingRelay && x.TryGetComp<CompPowerTrader>().PowerOn))
                {
                    boost += 5f;
                }
            }
            return boost;
        }
        public IEnumerable<NeuralStack> StoredNeuralStacks => compCache.innerContainer.OfType<NeuralStack>();
        public IEnumerable<NeuralStack> AllNeuralStacks => AllNeuralCaches.SelectMany(x => x.innerContainer.OfType<NeuralStack>());
        public List<Thing> LinkedBuildings => compFacility.LinkedBuildings;
        public IEnumerable<CompNeuralCache> AllNeuralCaches => LinkedBuildings.Select(x => x.TryGetComp<CompNeuralCache>()).Concat(compCache).Where(x => x != null);
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos())
            {
                yield return g;
            }
            if (Faction == Faction.OfPlayer)
            {
                foreach (var g in EjectAll())
                {
                    yield return g;
                }
                foreach (var g in GetManageMatrix())
                {
                    yield return g;
                }
            }
        }

        public IEnumerable<Gizmo> GetManageMatrix()
        {
            var manageNeuralMatrix = new Command_Action
            {
                defaultLabel = "AC.ManageNeuralMatrix".Translate(),
                defaultDesc = "AC.ManageNeuralMatrixDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Gizmos/ManageNeuralMatrix"),
                action = delegate
                {
                    Find.WindowStack.Add(new Window_NeuralMatrixManagement(this));
                }
            };
            manageNeuralMatrix.TryDisableCommand(new CommandInfo
            {
                lockedProjects = new List<ResearchProjectDef> { AC_DefOf.AC_NeuralDigitalization },
                building = this
            });
            yield return manageNeuralMatrix;
        }

        private IEnumerable<Gizmo> EjectAll()
        {
            var stacks = StoredNeuralStacks.ToList();
            if (stacks.Any())
            {
                var ejectAll = new Command_Action();
                ejectAll.defaultLabel = "AC.EjectAll".Translate();
                ejectAll.defaultDesc = "AC.EjectAllNeuralStacksDesc".Translate();
                ejectAll.icon = ContentFinder<Texture2D>.Get("UI/Gizmos/EjectAllStacks");
                ejectAll.action = delegate
                {
                    compCache.EjectContents();
                };
                yield return ejectAll;
            }
        }
    }
}