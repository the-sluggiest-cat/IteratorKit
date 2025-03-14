﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Logging;
using IteratorKit.CMOracle;
using UnityEngine;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using RWCustom;
using System.Text;
using On.Menu;
using Menu;
using MoreSlugcats;
using IteratorKit.SLOracle;
using IteratorKit.CustomPearls;
using System.Linq.Expressions;
using IteratorKit.Debug;
using System.Runtime.ExceptionServices;
using SlugBase.SaveData;
using static Menu.Remix.InternalOI;
using IteratorKit.SSOracle;

namespace IteratorKit
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class IteratorKit : BaseUnityPlugin
    {
        //todo: revamp logging so we aren't the most prevalent thing in there 
        public const string PLUGIN_GUID = "slugcat.iteratorkit";
        public const string PLUGIN_NAME = "IteratorKit";
        public const string PLUGIN_DESC = "Heavily modified framework for creating and editing Iterator dialogue.<LINE> <LINE>For mod developers, please see the GitHub page: https://github.com/the-sluggiest-cat/IteratorKit/.<LINE>Originally created by Twofour2.";
        //BepInEx called 0.0a version invalid; so we're sticking with 0.0. fuck you too, Bep.
        public const string PLUGIN_VERSION = "0.0";

        private bool oracleHasSpawned = false;
        public CMOracle.CMOracle oracle;

        public static new ManualLogSource Logger { get; private set; }

        public List<string> oracleRoomIds = new List<string>();
        public List<OracleJSON> oracleJsonData = new List<OracleJSON>();
        public CMOracleDebugUI oracleDebugUI = new CMOracleDebugUI();
        public List<CMOracle.CMOracle> oracleList = new List<CMOracle.CMOracle>();
        public static bool debugMode = false;
        public Debug.CMOracleTestManager testManager = new CMOracleTestManager();

        public delegate void OnEvent(CMOracle.CMOracle oracle, string eventName); 
        public delegate void OnEventEnd(CMOracle.CMOracle oracle, string eventName);

        private void OnEnable()
        {
            Logger = base.Logger;
            
            On.Room.ReadyForAI += SpawnOracle;

            CMOracle.CMOracle.ApplyHooks();
            CMOverseer.ApplyHooks();

            On.RainWorld.PostModsInit += AfterModsInit;
            On.RainWorldGame.RestartGame += OnRestartGame;
            
            SlugBase.SaveData.SaveDataHooks.Apply();
            On.RainWorldGame.RawUpdate += RainWorldGame_RawUpdate;
            On.ShortcutHandler.Update += InformOfInvalidShortcutError;

            SSOracleOverride.ApplyHooks();
        }


        private void OnDisable()
        {
            Logger.LogInfo("Bye bye.");
            On.Room.ReadyForAI -= SpawnOracle;

            CMOracle.CMOracle.RemoveHooks();
           // CMOverseer.ApplyHooks();

            On.RainWorld.PostModsInit -= AfterModsInit; 
            On.RainWorldGame.RestartGame -= OnRestartGame;

            SlugBase.SaveData.SaveDataHooks.UnApply();

            On.RainWorldGame.RawUpdate -= RainWorldGame_RawUpdate;
            On.ShortcutHandler.Update -= InformOfInvalidShortcutError;

            //    SLConversation.RemoveHooks();
            SSOracleOverride.RemoveHooks();
        }

        

        private void RainWorldGame_RawUpdate(On.RainWorldGame.orig_RawUpdate orig, RainWorldGame self, float dt)
        {
            orig(self, dt);
            if (self.devToolsActive) {
                if (Input.GetKeyDown(KeyCode.Alpha0))
                {
                    RainWorldGame.ForceSaveNewDenLocation(self, self.FirstAnyPlayer.Room.name, false);
                    CMOracleDebugUI.ModWarningText($"RainWorldGame_RawUpdate(): Save file forced den location to {self.FirstAlivePlayer.Room.name}! Press \"R\" to reload.", self.rainWorld);
                   ((StoryGameSession)self.session).saveState.deathPersistentSaveData.theMark = true;
                }
                if (Input.GetKeyDown(KeyCode.Alpha6))
                {
                    Futile.atlasManager.LogAllElementNames();
                    IteratorKit.Logger.LogInfo("RainWorldGame_RawUpdate(): Logging shader names");
                    foreach(KeyValuePair<string, FShader> shader in self.rainWorld.Shaders)
                    {
                        IteratorKit.Logger.LogInfo($"RainWorldGame_RawUpdate(): {shader.Key}");
                    }
                }
                if (Input.GetKeyDown(KeyCode.Alpha9))
                {
                    if (!this.oracleDebugUI.debugUIActive)
                    {
                        oracleDebugUI.EnableDebugUI(self.rainWorld, this);
                    }
                    else
                    {
                        oracleDebugUI.DisableDebugUI();
                    }
                    
                }
                if (Input.GetKeyDown(KeyCode.Alpha8))
                {
                    oracleDebugUI.EnableDebugUI(self.rainWorld, this);
                    testManager.EnableTestMode(this, self);
                }
                if (Input.GetKeyDown(KeyCode.Alpha7) && this.testManager.testsActive)
                {
                    testManager.GoToNextOracle(self);
                }
                if (Input.GetKeyDown(KeyCode.Alpha6))
                {
                    foreach (CMOracle.CMOracle oracle in oracleList)
                    {
                        (oracle.oracleBehavior as CMOracleBehavior).SetHasHadMainPlayerConversation(false);
                    }
                    self.GetStorySession.saveState.progression.SaveWorldStateAndProgression(malnourished: false);
                    CMOracleDebugUI.ModWarningText("RainWorldGame_RawUpdate(): Removed flag for HasHadMainPlayerConversation and saved game. Reload now.", self.rainWorld);

                }
                
            }
        }

        private void OnRestartGame(On.RainWorldGame.orig_RestartGame orig, RainWorldGame self)
        {
            this.LoadOracleFiles(self.rainWorld);
            orig(self);
        }

        private void AfterModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
        {
            orig(self);
            LoadOracleFiles(self, true);
        }


        private void LoadOracleFiles(RainWorld rainWorld, bool isDuringInit = false)
        {
            EncryptDialogFiles();
            try
            {
                this.oracleList = new List<CMOracle.CMOracle>();
                SSOracleOverride.ssOracleJsonData = new List<OracleJSON>();
                this.oracleDebugUI.ClearDebugLabels();
                oracleRoomIds = new List<string>();
                oracleJsonData = new List<OracleJSON>();
                foreach (ModManager.Mod mod in ModManager.ActiveMods)
                {
                    if (Directory.Exists(mod.path + "/sprites"))
                    {
                        IteratorKit.Logger.LogWarning("LoadOracleFiles(): hunting for atlases in " + mod.path + "/sprites");
                        foreach (string file in Directory.GetFiles(mod.path + "/sprites"))
                        {
                            IteratorKit.Logger.LogInfo($"LoadOracleFiles(): {file}");
                            
                            if (Path.GetFileName(file).StartsWith("oracle"))
                            {
                                IteratorKit.Logger.LogWarning($"LoadOracleFiles(): Loading atlas! sprites/{Path.GetFileNameWithoutExtension(file)}");
                                Futile.atlasManager.LoadAtlas($"sprites/{Path.GetFileNameWithoutExtension(file)}");
                            }
                        }
                    }
                    
                    foreach (string file in Directory.GetFiles(mod.path))
                    {
                        try
                        {
                            if (file.EndsWith("enabledebug"))
                            {
                                this.EnableDebugMode(rainWorld);
                            }
                            if (file.EndsWith("oracle.json"))
                            {
                                this.LoadOracleFile(file);
                            }
                            if (file.EndsWith("pearls.json"))
                            {
                                List<DataPearlJson> ojs = JsonConvert.DeserializeObject<List<DataPearlJson>>(File.ReadAllText(file));
                                CustomPearls.CustomPearls.LoadPearlData(ojs);
                                CustomPearls.CustomPearls.ApplyHooks();

                            }
                        }catch(Exception e)
                        {
                            if (!isDuringInit)
                            { // currently this text doesnt work as the screen isn't setup quite right.
                                CMOracleDebugUI.ModWarningText($"LoadOracleFiles(): Encountered an error while loading data file {file} from mod ${mod.name}.\n\n${e.Message}", rainWorld);
                            }
                            
                            Logger.LogError("LoadOracleFiles(): EXCEPTION");
                            Logger.LogError(e.ToString());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (!isDuringInit)
                { // currently this text doesnt work as the screen isn't setup quite right.
                    CMOracleDebugUI.ModWarningText($"LoadOracleFiles(): Encountered an error while loading oracle data.\n\n${e.Message}", rainWorld);
                }
                Logger.LogError("EXCEPTION");
                Logger.LogError(e.ToString());
            }
            return;
        }

        public void LoadOracleFile(string file)
        {
            List<OracleJSON> ojs = JsonConvert.DeserializeObject<List<OracleJSON>>(File.ReadAllText(file));
            
            //what i've learned:
            //the JSON deserializes into a format which is then handled by CMOracleBehavior
            //todo: interpret the key as a slugcat name, react accordingly
            //"for": in the JSON should be enough to deter unplanned slugcats, but if no events are present/slugcat is not a key, don't spawn the iterator

            foreach (OracleJSON oracleData in ojs)
            {
                
                switch (oracleData.id)
                { //todo: fix SL
                    case "SL":
                        IteratorKit.Logger.LogWarning("LoadOracleFile(): IF YOU SEE THIS, SL CONVOS DO NOT WORK YET. REMIND ME TO DO THIS LATER.");
                        //SLConversation slConvo = new SLConversation(oracleData, self.game.StoryCharacter);
                        //slConvo.ApplyHooks();
                        break;
                    case "SS": // includes DM
                        IteratorKit.Logger.LogInfo($"LoadOracleFile(): loading SS oracle data {file}");
                        SSOracleOverride.ssOracleJsonData.Add(oracleData);
                        break;
                    case "DM":
                        IteratorKit.Logger.LogInfo($"LoadOracleFile(): loading DM oracle data {file}");
                        SSOracleOverride.ssOracleJsonData.Add(oracleData);
                        break;
                    default:
                        IteratorKit.Logger.LogInfo($"LoadOracleFile(): loading custom oracle data {file}");
                        oracleJsonData.Add(oracleData);
                        oracleRoomIds.Add(oracleData.roomId);
                        break;
                }
                if (oracleData.overseers != null)
                {
                    CMOverseer.overseeerDataList.Add(oracleData.overseers);
                    CMOverseer.regionList.AddRange(oracleData.overseers.regions);
                }

            }
        }

        private void EnableDebugMode(RainWorld rainWorld)
        {
            if (IteratorKit.debugMode)
            {
                IteratorKit.Logger.LogInfo("EnableDebugMode(): Debug mode already enabled");
                return;  
            }
            IteratorKit.Logger.LogInfo("EnableDebugMode(): Iterator kit debug mode enabled");
            IteratorKit.debugMode = true;
            On.Menu.HoldButton.Update += HoldButton_Update;
            oracleDebugUI.EnableDebugUI(rainWorld, this);


        }

        private void ShelterDoor_Update(On.ShelterDoor.orig_Update orig, ShelterDoor self, bool eu)
        {
            
            orig(self, eu);
            self.openTime = 1;
            self.openUpTicks = 10;
        }

        private static void HoldButton_Update(On.Menu.HoldButton.orig_Update orig, Menu.HoldButton self)
        {
            orig(self);
            if (self.held)
            {
                self.Singal(self, self.signalText);
                self.hasSignalled = true;
                self.menu.ResetSelection();
            }
        }

        private void SpawnOracle(On.Room.orig_ReadyForAI orig, Room self)
        {
            orig(self);
            if (self.game == null)
            {
                return;
            }
            try
            {
                if (this.oracleRoomIds.Contains(self.roomSettings.name))
                {
                    IEnumerable<OracleJSON> oracleJsons = this.oracleJsonData.Where(x => x.roomId == self.roomSettings.name);
                    foreach (OracleJSON oracleJson in oracleJsons)
                    {

                        if (oracleJson.forSlugcats.Contains(self.game.StoryCharacter))
                        {
                            IteratorKit.Logger.LogWarning($"SpawnOracle(): Found matching room, spawning oracle {oracleJson.id} ");
                            self.loadingProgress = 3;
                            self.readyForNonAICreaturesToEnter = true;
                            WorldCoordinate worldCoordinate = new WorldCoordinate(self.abstractRoom.index, 15, 15, -1);
                            AbstractPhysicalObject abstractPhysicalObject = new AbstractPhysicalObject(
                                self.world,
                                global::AbstractPhysicalObject.AbstractObjectType.Oracle,
                                null,
                                worldCoordinate,
                                self.game.GetNewID());
                            IteratorKit.Logger.LogWarning($"SpawnOracle(): {oracleJson == null}");
                            oracle = new CMOracle.CMOracle(abstractPhysicalObject, self, oracleJson);
                            self.AddObject(oracle);
                            self.waitToEnterAfterFullyLoaded = Math.Max(self.waitToEnterAfterFullyLoaded, 20);
                            this.oracleList.Add(oracle);
                        }
                        else
                        {
                            Logger.LogWarning($"SpawnOracle(): {oracleJson.id} Oracle is not avalible for the current slugcat");
                        }
                    }

                }
            }catch (Exception e)
            {
                IteratorKit.Logger.LogError(e);
                CMOracleDebugUI.ModWarningText($"SpawnOracle(): Iterator Kit Initialization Error: {e}", self.game.rainWorld);
                
            }

            

        }

        public void DebugMouse_Update(On.DebugMouse.orig_Update orig, DebugMouse self, bool eu)
        {
            
            orig(self, eu);
            if (oracleHasSpawned)
            {
                //oracle.oracleBehavior.SetNewDestination(self.pos);
                oracle.oracleBehavior.lookPoint = self.pos;
            }
        }

        public static void LogVector2(Vector2 vector)
        {
            Logger.LogInfo($"LogVector2(): x: {vector.x} y: {vector.y}");
        }

        public void EncryptDialogFiles()
        {
            try
            {
                IteratorKit.Logger.LogWarning("EncryptDialogFiles(): Encrypting all dialog files");
                foreach (ModManager.Mod mod in ModManager.ActiveMods)
                {
                    string[] dirs = Directory.GetDirectories(mod.path);
                    foreach (string dir in dirs)
                    {
                        if (dir.EndsWith("text_raw"))
                        {
                            IteratorKit.Logger.LogInfo("EncryptDialogFiles(): got raw dir file");
                            ProcessUnencryptedTexts(dir, mod.path);
                        }
                    }
                    
                }
            }catch (Exception e)
            {
                Logger.LogWarning(e.Message);
            }
        }

        private void ProcessUnencryptedTexts(string dir, string modDir)
        {
            for (int i = 0; i < ExtEnum<InGameTranslator.LanguageID>.values.Count; i++)
            {
                IteratorKit.Logger.LogInfo("ProcessUnencryptedTexts(): Encypting text files");
                InGameTranslator.LanguageID languageID = InGameTranslator.LanguageID.Parse(i);
                string langDir = Path.Combine(dir, $"Text_{LocalizationTranslator.LangShort(languageID)}").ToLowerInvariant();
                //string langDir = string.Concat(new string[]
                //   {
                //    dir,
                //    Path.DirectorySeparatorChar.ToString(),
                //    "Text",
                //    Path.DirectorySeparatorChar.ToString(),
                //    "Text_",
                //    LocalizationTranslator.LangShort(languageID),
                //    Path.DirectorySeparatorChar.ToString()
                //   }).ToLowerInvariant();
                IteratorKit.Logger.LogInfo($"ProcessUnencryptedTexts(): Checking lang dir {langDir}");
                IteratorKit.Logger.LogWarning(Directory.Exists(langDir));
                if (Directory.Exists(langDir))
                {
                    string[] files = Directory.GetFiles(langDir, "*.txt", SearchOption.AllDirectories);
                    foreach (string file in files)
                    {
                        IteratorKit.Logger.LogInfo($"ProcessUnencryptedTexts(): Encrypting file at ${file}");
                        string result = InGameTranslator.EncryptDecryptFile(file, true, true);
                        IteratorKit.Logger.LogInfo(result);
                        SaveEncryptedText(modDir, languageID, result, Path.GetFileName(file));
                    }

                }
            }
        }

        private void SaveEncryptedText(string modDir, InGameTranslator.LanguageID langId, string encryptedText, string origFileName)
        {
            string modTexts = Path.Combine(modDir, "text", $"Text_{LocalizationTranslator.LangShort(langId)}").ToLowerInvariant();
            if (!Directory.Exists(modTexts))
            {
                Logger.LogWarning($"SaveEncryptedText: Creating texts directory for mod dir {modTexts}");
                Directory.CreateDirectory(modTexts);
            }
            string encryptedLangFilePath = Path.Combine(modTexts, origFileName).ToLowerInvariant();
            Logger.LogInfo($"SaveEncryptedText: Writing file to: {encryptedLangFilePath}");
            File.WriteAllText(encryptedLangFilePath, encryptedText, encoding: Encoding.UTF8);
            Logger.LogInfo("SaveEncryptedText: Wrote encrypted text file.");
            
        }

        private void InformOfInvalidShortcutError(On.ShortcutHandler.orig_Update orig, ShortcutHandler self)
        {
            try
            {
                orig(self);
            }
            catch (IndexOutOfRangeException e)
            {
                //aww; how nice. they thought about region makers.
                CMOracleDebugUI.ModWarningText("InformOfInvalidShortcutError(): ROOM SHORTCUTS ARE NOT SETUP CORRECTLY. this is a kind message just to let you know from iteratorkit :).", self.game.rainWorld);
                ExceptionDispatchInfo.Capture(e).Throw(); // re-throw the error
            }

        }

    }
}
