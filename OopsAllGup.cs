using BepInEx;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.ExpansionManagement;
using System;
using System.Linq;
using UnityEngine;

// Allow scanning for ConCommand, and other stuff for Risk of Rain 2
[assembly: HG.Reflection.SearchableAttribute.OptIn]
namespace OopsAllGup
{
    [BepInPlugin(GUID, ModName, Version)]
    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    public class OopsAllGup : BaseUnityPlugin
    {
        public const string GUID = "com.justinderby.oopsallgup";
        public const string ModName = "OopsAllGup";
        public const string Version = "1.0.0";

        public static ConfigEntry<bool> ModEnabled;
        public static ConfigEntry<int> SplitCount;
        public static ConfigEntry<int> Lives;
        public static ConfigEntry<bool> KinForcesGup;

        public void Awake()
        {
            ModEnabled = Config.Bind<bool>("General", "ModEnabled", true, "Enable mod. (Default: true)");
            SplitCount = Config.Bind<int>("General", "SplitCount", 2, "When undergoing mitosis, how many should spawn. (Default: 2)");
            Lives = Config.Bind<int>("General", "Lives", 3, "How many lives a gup has. (Default: 3)");
            KinForcesGup = Config.Bind<bool>("General", "OverrideArtifactOfKin", true, "When Artifact of Kin is enabled, force Gup as the monster. (Default: true)");

            On.EntityStates.Gup.BaseSplitDeath.OnEnter += BaseSplitDeath_OnEnter;
            IL.RoR2.BodySplitter.PerformInternal += BodySplitter_PerformInternal;
            On.RoR2.ClassicStageInfo.HandleSingleMonsterTypeArtifact += ClassicStageInfo_HandleSingleMonsterTypeArtifact;

            try
            {
                if (RiskOfOptionsCompatibility.enabled)
                {
                    RiskOfOptionsCompatibility.InstallRiskOfOptions();
                }
            } catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public void Destroy()
        {
            On.EntityStates.Gup.BaseSplitDeath.OnEnter -= BaseSplitDeath_OnEnter;
            IL.RoR2.BodySplitter.PerformInternal -= BodySplitter_PerformInternal;
            On.RoR2.ClassicStageInfo.HandleSingleMonsterTypeArtifact -= ClassicStageInfo_HandleSingleMonsterTypeArtifact;
        }
        private CharacterSpawnCard getGeepCard()
        {
            return LegacyResourcesAPI.Load<CharacterSpawnCard>("SpawnCards/CharacterSpawnCards/cscGeepBody");
        }

        private CharacterSpawnCard getGupCard()
        {
            return LegacyResourcesAPI.Load<CharacterSpawnCard>("SpawnCards/CharacterSpawnCards/cscGupBody");
        }

        private void BaseSplitDeath_OnEnter(On.EntityStates.Gup.BaseSplitDeath.orig_OnEnter orig, EntityStates.Gup.BaseSplitDeath self)
        {
            orig(self);
            if (!ModEnabled.Value)
            {
                return;
            }
            if (self is EntityStates.Gup.GupDeath || self is EntityStates.Gup.GeepDeath)
            {
                GupDetails details = self.outer.commonComponents.characterBody.gameObject.GetComponent<GupDetails>();
                if (!details)
                {
                    details = self.outer.commonComponents.characterBody.gameObject.AddComponent<GupDetails>();
                    details.livesLeft = Lives.Value;
                }
                details.livesLeft--;
                if (details.livesLeft > 1)
                {
                    Debug.LogError($"Forcing a new split! livesLeft = {details.livesLeft}");
                    ForceGupSplit(self);
                }
            }
        }
        
        private void ForceGupSplit(EntityStates.Gup.BaseSplitDeath entity)
        {
            entity.characterSpawnCard = getGeepCard();
            entity.spawnCount = SplitCount.Value;
        }

        private void BodySplitter_PerformInternal(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            c.GotoNext(x => x.MatchCallOrCallvirt<BodySplitter>("AddBodyVelocity"));
            c.Index += 1;
            // "this"
            c.Emit(OpCodes.Ldarg_0);
            // "CharacterBody exists = characterMaster.GetBody();"
            c.Emit(OpCodes.Ldloc_S, (byte)16);
            c.EmitDelegate<Action<BodySplitter, CharacterBody>>((bodySplitter, characterBody) =>
            {
                // Ensure we copy the GupDetails to the newly created bodies
                GupDetails detailsOldBody = bodySplitter.body.gameObject.GetComponent<GupDetails>();
                if (detailsOldBody)
                {
                    GupDetails detailsNewBody = characterBody.gameObject.GetComponent<GupDetails>();
                    if (!detailsNewBody)
                    {
                        detailsNewBody = characterBody.gameObject.AddComponent<GupDetails>();
                    }
                    Debug.LogError($"Copying over gup lives ({detailsNewBody.livesLeft})...");
                    detailsNewBody.CopyFrom(detailsOldBody);
                }
                else
                {
                    Debug.LogError("CANNOT FIND OLD BODY");
                }
            });
        }

        private void ClassicStageInfo_HandleSingleMonsterTypeArtifact(On.RoR2.ClassicStageInfo.orig_HandleSingleMonsterTypeArtifact orig, DirectorCardCategorySelection monsterCategories, Xoroshiro128Plus rng)
        {
            if (!ModEnabled.Value || !KinForcesGup.Value || !Run.instance.IsExpansionEnabled(ExpansionCatalog.expansionDefs.FirstOrDefault(def => def.nameToken == "DLC1_NAME")))
            {
                orig(monsterCategories, rng);
                return;
            }

            monsterCategories.Clear();
            int index = monsterCategories.AddCategory("Gup", 1f);
            CharacterSpawnCard card = getGupCard();
            monsterCategories.AddCard(index, new DirectorCard()
            {
                spawnCard = card,
                selectionWeight = 1,
                spawnDistance = DirectorCore.MonsterSpawnDistance.Standard,
                preventOverhead = false,
                minimumStageCompletions = 0,
            });
            
            BodyIndex body = card.prefab.GetComponent<CharacterMaster>().bodyPrefab.GetComponent<CharacterBody>().bodyIndex;
            if (Stage.instance)
            {
                Stage.instance.singleMonsterTypeBodyIndex = body;
                return;
            }
            Stage.onServerStageBegin += (stage) =>
            {
                stage.singleMonsterTypeBodyIndex = body;
            };
        }

    }
}
