using BepInEx;
using BepInEx.Configuration;
using RoR2;
using RoR2.ExpansionManagement;
using System;
using System.Linq;
using UnityEngine.Networking;

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
        public const string Version = "1.0.1";

        public static ConfigEntry<bool> ModEnabled;
        public static ConfigEntry<int> SplitCount;
        public static ConfigEntry<int> Lives;
        public static ConfigEntry<bool> KinForcesGup;

        public void Awake()
        {
            Log.Init(Logger);

            ModEnabled = Config.Bind<bool>("General", "ModEnabled", true, "Enable mod. (Default: true)");
            SplitCount = Config.Bind<int>("General", "SplitCount", 2, "When undergoing mitosis, how many should spawn. (Default: 2)");
            Lives = Config.Bind<int>("General", "Lives", 3, "How many lives a gup has. (Default: 3)");
            KinForcesGup = Config.Bind<bool>("General", "OverrideArtifactOfKin", true, "When Artifact of Kin is enabled, force Gup as the monster. (Default: true)");

            On.EntityStates.Gup.BaseSplitDeath.OnEnter += BaseSplitDeath_OnEnter;
            On.RoR2.ClassicStageInfo.HandleSingleMonsterTypeArtifact += ClassicStageInfo_HandleSingleMonsterTypeArtifact;
            On.RoR2.BodySplitter.PerformInternal += BodySplitter_PerformInternal;

            try
            {
                if (RiskOfOptionsCompatibility.Enabled)
                {
                    RiskOfOptionsCompatibility.InstallRiskOfOptions();
                }
            } catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }

        public void Destroy()
        {
            On.EntityStates.Gup.BaseSplitDeath.OnEnter -= BaseSplitDeath_OnEnter;
            On.RoR2.ClassicStageInfo.HandleSingleMonsterTypeArtifact -= ClassicStageInfo_HandleSingleMonsterTypeArtifact;
            On.RoR2.BodySplitter.PerformInternal -= BodySplitter_PerformInternal;
        }
        private CharacterSpawnCard GetGeepCard()
        {
            return LegacyResourcesAPI.Load<CharacterSpawnCard>("SpawnCards/CharacterSpawnCards/cscGeepBody");
        }

        private CharacterSpawnCard GetGupCard()
        {
            return LegacyResourcesAPI.Load<CharacterSpawnCard>("SpawnCards/CharacterSpawnCards/cscGupBody");
        }

        private void BaseSplitDeath_OnEnter(On.EntityStates.Gup.BaseSplitDeath.orig_OnEnter orig, EntityStates.Gup.BaseSplitDeath self)
        {
            orig(self);
            if (!NetworkServer.active)
            {
                return;
            }
            if (!ModEnabled.Value)
            {
                return;
            }
            if (self is EntityStates.Gup.GupDeath || self is EntityStates.Gup.GeepDeath)
            {
                GupDetails details = self.outer.commonComponents.characterBody.masterObject.gameObject.GetComponent<GupDetails>();
                if (!details)
                {
                    details = self.outer.commonComponents.characterBody.masterObject.gameObject.AddComponent<GupDetails>();
                    details.livesLeft = Lives.Value;
                    Log.Debug($"Adding new life counter to gup. livesLeft = {details.livesLeft}");
                }
                details.livesLeft--;
                if (details.livesLeft > 1)
                {
                    Log.Debug($"Forcing a new split! livesLeft = {details.livesLeft}");
                    ForceGupSplit(self);
                }
            }
        }
        
        private void ForceGupSplit(EntityStates.Gup.BaseSplitDeath entity)
        {
            entity.characterSpawnCard = GetGeepCard();
            entity.spawnCount = SplitCount.Value;
        }

        private void BodySplitter_PerformInternal(On.RoR2.BodySplitter.orig_PerformInternal orig, BodySplitter self, MasterSummon masterSummon)
        {
            if (!NetworkServer.active || !ModEnabled.Value)
            {
                orig(self, masterSummon);
                return;
            }
            // Ensure we copy the GupDetails to the newly created bodies
            GupDetails detailsOldBody = self.body.masterObject.gameObject.GetComponent<GupDetails>();
            if (detailsOldBody == null)
            {
                Log.Error("Cannot find GupDetails on Gup!");
                orig(self, masterSummon);
                return;
            }

            Action<CharacterMaster> oldAction = masterSummon.preSpawnSetupCallback;
            masterSummon.preSpawnSetupCallback += (CharacterMaster characterMaster) =>
            {
                GupDetails detailsNewBody = characterMaster.gameObject.GetComponent<GupDetails>();
                if (!detailsNewBody)
                {
                    detailsNewBody = characterMaster.gameObject.AddComponent<GupDetails>();
                }
                detailsNewBody.CopyFrom(detailsOldBody);
                Log.Debug($"Copying over gup lives! livesLeft = {detailsNewBody.livesLeft}");

                oldAction?.Invoke(characterMaster);
            };
            orig(self, masterSummon);
        }

        private void ClassicStageInfo_HandleSingleMonsterTypeArtifact(On.RoR2.ClassicStageInfo.orig_HandleSingleMonsterTypeArtifact orig, DirectorCardCategorySelection monsterCategories, Xoroshiro128Plus rng)
        {
            if (!NetworkServer.active)
            {
                orig(monsterCategories, rng);
                return;
            }
            if (!ModEnabled.Value || !KinForcesGup.Value)
            {
                orig(monsterCategories, rng);
                return;
            }
            if (!Run.instance.IsExpansionEnabled(ExpansionCatalog.expansionDefs.FirstOrDefault(def => def.nameToken == "DLC1_NAME")))
            {
                Log.Warning("Survivors of the Void expansion not enabled; not enabling Gups with Artifact of Kin");
                orig(monsterCategories, rng);
                return;
            }

            monsterCategories.Clear();
            int index = monsterCategories.AddCategory("Gup", 1f);
            CharacterSpawnCard card = GetGupCard();
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
