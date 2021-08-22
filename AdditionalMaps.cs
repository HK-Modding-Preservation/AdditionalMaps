﻿using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Modding;
using UnityEngine;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using AdditionalMaps.Consts;
using AdditionalMaps.MonoBehaviours;
using SFCore.Generics;
using SFCore.Utils;
using UnityEngine.SceneManagement;
using Logger = Modding.Logger;
using UObject = UnityEngine.Object;

namespace AdditionalMaps
{
    public class AdditionalMaps : FullSettingsMod<AmSaveSettings, AmGlobalSettings>
    {
        internal static AdditionalMaps Instance;

        public Consts.LanguageStrings LangStrings { get; private set; }
        public TextureStrings SpriteDict { get; private set; }

        public static Sprite GetSprite(string name) => Instance.SpriteDict.Get(name);
        public static Material DefaultSpriteMaterial { get; private set; }

        private GameObject _shinyPrefab;
        private PlayMakerFSM _setCompassPointPrefab;
        private PlayMakerFSM _setCompassPointRoomPrefab;

        public override string GetVersion() => Util.GetVersion(Assembly.GetExecutingAssembly());

        public override List<ValueTuple<string, string>> GetPreloadNames()
        {
            return new()
            {
                new ValueTuple<string, string>("Crossroads_33", "scatter_map 1"), // 64
                new ValueTuple<string, string>("Crossroads_33", "scatter_map 2"), // 64
                new ValueTuple<string, string>("Crossroads_33", "scatter_map 3"), // 64
                new ValueTuple<string, string>("Grimm_Divine", "Charm Holder"),
                new ValueTuple<string, string>("Town", "_Props/Stag_station/open/door_station"),
                new ValueTuple<string, string>("Room_mapper", "_SceneManager")
            };
        }

        public AdditionalMaps() : base("Additional Maps")
        {
            Instance = this;

            LangStrings = new Consts.LanguageStrings();
            SpriteDict = new TextureStrings();

            On.PlayMakerFSM.Start += OnPlayMakerFSMStart;
        }

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            Log("Initializing");

            _shinyPrefab = preloadedObjects["Grimm_Divine"]["Charm Holder"];
            {
                UObject.Destroy(_shinyPrefab.transform.GetChild(2));
                UObject.Destroy(_shinyPrefab.transform.GetChild(1));
                UObject.Destroy(_shinyPrefab.transform.GetChild(0).gameObject.GetComponent<PersistentBoolItem>());
            }
            SetInactive(_shinyPrefab);
            _setCompassPointPrefab = preloadedObjects["Town"]["_Props/Stag_station/open/door_station"].LocateMyFSM("Set Compass Point");
            _setCompassPointRoomPrefab = preloadedObjects["Room_mapper"]["_SceneManager"].LocateMyFSM("map_isroom");

            InitGlobalSettings();
            InitCallbacks();

            DefaultSpriteMaterial = new Material(Shader.Find("Sprites/Default"));
            DefaultSpriteMaterial.SetColor(Shader.PropertyToID("_Color"), new Color(1.0f, 1.0f, 1.0f, 1.0f));
            DefaultSpriteMaterial.SetInt(Shader.PropertyToID("PixelSnap"), 0);
            DefaultSpriteMaterial.SetFloat(Shader.PropertyToID("_EnableExternalAlpha"), 0.0f);
            DefaultSpriteMaterial.SetInt(Shader.PropertyToID("_StencilComp"), 8);
            DefaultSpriteMaterial.SetInt(Shader.PropertyToID("_Stencil"), 0);
            DefaultSpriteMaterial.SetInt(Shader.PropertyToID("_StencilOp"), 0);
            DefaultSpriteMaterial.SetInt(Shader.PropertyToID("_StencilWriteMask"), 255);
            DefaultSpriteMaterial.SetInt(Shader.PropertyToID("_StencilReadMask"), 255);

            GameMapHooks.Init(GameMapCallback);

            Log("Initialized");
        }

        private IEnumerator MapCompleteRegion()
        {
            yield return new WaitWhile(() => !UObject.FindObjectOfType<GameMap>());

            const string sceneName = "scene_name"; // Whichever scene the player just entered
            
            var gameMap = UObject.FindObjectOfType<GameMap>();
            var ret = gameMap.areaAncientBasin.FindGameObjectInChildren(sceneName);
            ret = gameMap.areaCity.FindGameObjectInChildren(sceneName) ?? ret;
            ret = gameMap.areaCliffs.FindGameObjectInChildren(sceneName) ?? ret;
            ret = gameMap.areaCrossroads.FindGameObjectInChildren(sceneName) ?? ret;
            ret = gameMap.areaCrystalPeak.FindGameObjectInChildren(sceneName) ?? ret;
            ret = gameMap.areaDeepnest.FindGameObjectInChildren(sceneName) ?? ret;
            ret = gameMap.areaDirtmouth.FindGameObjectInChildren(sceneName) ?? ret;
            ret = gameMap.areaFogCanyon.FindGameObjectInChildren(sceneName) ?? ret;
            ret = gameMap.areaFungalWastes.FindGameObjectInChildren(sceneName) ?? ret;
            ret = gameMap.areaGreenpath.FindGameObjectInChildren(sceneName) ?? ret;
            ret = gameMap.areaKingdomsEdge.FindGameObjectInChildren(sceneName) ?? ret;
            ret = gameMap.areaQueensGardens.FindGameObjectInChildren(sceneName) ?? ret;
            ret = gameMap.areaRestingGrounds.FindGameObjectInChildren(sceneName) ?? ret;
            ret = gameMap.areaWaterways.FindGameObjectInChildren(sceneName) ?? ret;
            if (ret != null)
            {
                for (var i = 0; i < ret.transform.childCount; i++)
                {
                    var go = ret.transform.GetChild(i);
                    if (!PlayerData.instance.GetVariable<List<string>>(nameof(PlayerData.instance.scenesMapped)).Contains(go.name))
                    {
                        PlayerData.instance.GetVariable<List<string>>(nameof(PlayerData.instance.scenesMapped)).Add(go.name);
                    }
                }
            }
        }

        private Dictionary<string, SCustomArea> GameMapCallback(GameMap gameMapBetter)
        {
            Log("!gameMapCallback");

            #region Prefabs

            var areaNamePrefab = UObject.Instantiate(gameMapBetter.areaCliffs.transform.GetChild(0).gameObject);
            areaNamePrefab.SetActive(false);
            var subAreaPrefab = UObject.Instantiate(gameMapBetter.areaCliffs.transform.GetChild(6).GetChild(0).gameObject);
            subAreaPrefab.SetActive(false);
            var roomMat = UObject.Instantiate(gameMapBetter.areaCliffs.transform.GetChild(1).GetComponent<SpriteRenderer>().material);
            DefaultSpriteMaterial = roomMat;
            var benchPrefab = UObject.Instantiate(gameMapBetter.areaCliffs.transform.GetChild(3).GetChild(2).gameObject);
            benchPrefab.SetActive(false);

            var tmpDict = new Dictionary<string, SCustomArea>();

            #endregion

            #region White Palace Map

            var areaWhitePalace = UObject.Instantiate(gameMapBetter.areaCliffs, gameMapBetter.transform);
            areaWhitePalace.SetActive(true);

            for (var i = 0; i < areaWhitePalace.transform.childCount; i++)
            {
                UObject.Destroy(areaWhitePalace.transform.GetChild(i).gameObject);
            }

            areaWhitePalace.name = "WHITE_PALACE";
            areaWhitePalace.layer = 5;
            areaWhitePalace.transform.localScale = Vector3.one;
            areaWhitePalace.transform.localPosition = new Vector3(-2.0f, 15f, gameMapBetter.areaCliffs.transform.localPosition.z);

            var wpScenes = new List<GameObject>() {
                new("White_Palace_01", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
                new("White_Palace_02", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
                new("White_Palace_03_hub", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
                new("White_Palace_04", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
                new("White_Palace_05", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
                new("White_Palace_06", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
                new("White_Palace_07", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
                new("White_Palace_08", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
                new("White_Palace_09", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
                new("White_Palace_12", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
                new("White_Palace_13", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
                new("White_Palace_14", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
                new("White_Palace_15", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
                new("White_Palace_16", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
                new("White_Palace_17", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
                new("White_Palace_18", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
                new("White_Palace_19", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
                new("White_Palace_20", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
            };
            foreach (var sceneGo in wpScenes)
            {
                sceneGo.transform.SetParent(areaWhitePalace.transform);
                sceneGo.layer = 5;
                sceneGo.transform.localScale = Vector3.one;
                sceneGo.SetActive(true);
                var sr = sceneGo.GetComponent<SpriteRenderer>();
                sr.material = roomMat;
                sr.sprite = SpriteDict.Get(sceneGo.name);
                sr.sortingLayerID = 629535577;
                sr.sortingOrder = 0;
                var rmr = sceneGo.GetComponent<RoughMapRoom>();
                rmr.fullSprite = sr.sprite;
            }
            var tmpChildZ = gameMapBetter.areaCliffs.transform.GetChild(6).localPosition.z;
            var sceneDivider = 500.0f + (100.0f / 3.0f);
            wpScenes[0].transform.localPosition = new Vector3(-375 / sceneDivider, -2510 / sceneDivider, tmpChildZ);
            wpScenes[1].transform.localPosition = new Vector3(856 / sceneDivider, -2687 / sceneDivider, tmpChildZ);
            wpScenes[2].transform.localPosition = new Vector3(-30 / sceneDivider, -1496 / sceneDivider, tmpChildZ);
            wpScenes[3].transform.localPosition = new Vector3(-1110 / sceneDivider, -1647 / sceneDivider, tmpChildZ);
            wpScenes[4].transform.localPosition = new Vector3(1112 / sceneDivider, -1093 / sceneDivider, tmpChildZ);
            wpScenes[5].transform.localPosition = new Vector3(52 / sceneDivider, -717 / sceneDivider, tmpChildZ);
            wpScenes[6].transform.localPosition = new Vector3(36 / sceneDivider, 67 / sceneDivider, tmpChildZ);
            wpScenes[7].transform.localPosition = new Vector3(1971 / sceneDivider, 850 / sceneDivider, tmpChildZ);
            wpScenes[8].transform.localPosition = new Vector3(-391 / sceneDivider, 1804 / sceneDivider, tmpChildZ);
            wpScenes[9].transform.localPosition = new Vector3(-117 / sceneDivider, 456 / sceneDivider, tmpChildZ);
            wpScenes[10].transform.localPosition = new Vector3(773 / sceneDivider, 572 / sceneDivider, tmpChildZ);
            wpScenes[11].transform.localPosition = new Vector3(-1094 / sceneDivider, -1302 / sceneDivider, tmpChildZ);
            wpScenes[12].transform.localPosition = new Vector3(592 / sceneDivider, -1201 / sceneDivider, tmpChildZ);
            wpScenes[13].transform.localPosition = new Vector3(1407 / sceneDivider, -1147 / sceneDivider, tmpChildZ);
            wpScenes[14].transform.localPosition = new Vector3(-2187 / sceneDivider, -283 / sceneDivider, tmpChildZ);
            wpScenes[15].transform.localPosition = new Vector3(-1507 / sceneDivider, -640 / sceneDivider, tmpChildZ);
            wpScenes[16].transform.localPosition = new Vector3(-1717 / sceneDivider, -93 / sceneDivider, tmpChildZ);
            wpScenes[17].transform.localPosition = new Vector3(-1292 / sceneDivider, 40 / sceneDivider, tmpChildZ);

            wpScenes[17].transform.localScale = new Vector3(0.93f, 1.04f, 1.0f);

            var wpRoomSprites = new List<GameObject>() {
                new("RWP01", typeof(SpriteRenderer)),
                new("RWP02", typeof(SpriteRenderer)),
                new("RWP03", typeof(SpriteRenderer)),
                new("RWP04", typeof(SpriteRenderer)),
                new("RWP05", typeof(SpriteRenderer)),
                new("RWP06", typeof(SpriteRenderer)),
                new("RWP07", typeof(SpriteRenderer)),
                new("RWP08", typeof(SpriteRenderer)),
                new("RWP09", typeof(SpriteRenderer)),
                new("RWP12", typeof(SpriteRenderer)),
                new("RWP13", typeof(SpriteRenderer)),
                new("RWP14", typeof(SpriteRenderer)),
                new("RWP15", typeof(SpriteRenderer)),
                new("RWP16", typeof(SpriteRenderer)),
                new("RWP17", typeof(SpriteRenderer)),
                new("RWP18", typeof(SpriteRenderer)),
                new("RWP19", typeof(SpriteRenderer)),
                new("RWP20", typeof(SpriteRenderer))
            };
            var roomDivider = 5.333333f;
            wpRoomSprites[0].transform.SetParent(wpScenes[0].transform);
            wpRoomSprites[0].transform.localPosition = new Vector3(0.23f / roomDivider, 0.03333334f / roomDivider);
            wpRoomSprites[1].transform.SetParent(wpScenes[1].transform);
            wpRoomSprites[1].transform.localPosition = new Vector3(0.64f / roomDivider, -0.144f / roomDivider);
            wpRoomSprites[2].transform.SetParent(wpScenes[2].transform);
            wpRoomSprites[2].transform.localPosition = new Vector3(0.035f / roomDivider, 0f / roomDivider);
            wpRoomSprites[3].transform.SetParent(wpScenes[3].transform);
            wpRoomSprites[3].transform.localPosition = new Vector3(0.166f / roomDivider, -0.037f / roomDivider);
            wpRoomSprites[4].transform.SetParent(wpScenes[4].transform);
            wpRoomSprites[4].transform.localPosition = new Vector3(1.76f / roomDivider, 0.21f / roomDivider);
            wpRoomSprites[5].transform.SetParent(wpScenes[5].transform);
            wpRoomSprites[5].transform.localPosition = new Vector3(1.293333f / roomDivider, -0.03f / roomDivider);
            wpRoomSprites[6].transform.SetParent(wpScenes[6].transform);
            wpRoomSprites[6].transform.localPosition = new Vector3(0.8666667f / roomDivider, 0.05f / roomDivider);
            wpRoomSprites[7].transform.SetParent(wpScenes[7].transform);
            wpRoomSprites[7].transform.localPosition = new Vector3(0.3166667f / roomDivider, 0.03333334f / roomDivider);
            wpRoomSprites[8].transform.SetParent(wpScenes[8].transform);
            wpRoomSprites[8].transform.localPosition = new Vector3(0.175f / roomDivider, -1.347f / roomDivider);
            wpRoomSprites[9].transform.SetParent(wpScenes[9].transform);
            wpRoomSprites[9].transform.localPosition = new Vector3(0.024f / roomDivider, 4.4f / roomDivider);
            wpRoomSprites[10].transform.SetParent(wpScenes[10].transform);
            wpRoomSprites[10].transform.localPosition = new Vector3(0.08333334f / roomDivider, 3.96f / roomDivider);
            wpRoomSprites[11].transform.SetParent(wpScenes[11].transform);
            wpRoomSprites[11].transform.localPosition = new Vector3(0.168f / roomDivider, 2.326667f / roomDivider);
            wpRoomSprites[12].transform.SetParent(wpScenes[12].transform);
            wpRoomSprites[12].transform.localPosition = new Vector3(0.32f / roomDivider, 0.5433334f / roomDivider);
            wpRoomSprites[13].transform.SetParent(wpScenes[13].transform);
            wpRoomSprites[13].transform.localPosition = new Vector3(4.514f / roomDivider, -0.1033333f / roomDivider);
            wpRoomSprites[14].transform.SetParent(wpScenes[14].transform);
            wpRoomSprites[14].transform.localPosition = new Vector3(0.05777778f / roomDivider, -0.05f / roomDivider);
            wpRoomSprites[15].transform.SetParent(wpScenes[15].transform);
            wpRoomSprites[15].transform.localPosition = new Vector3(4.403333f / roomDivider, -0.1866667f / roomDivider);
            wpRoomSprites[16].transform.SetParent(wpScenes[16].transform);
            wpRoomSprites[16].transform.localPosition = new Vector3(-0.696f / roomDivider, 0.957f / roomDivider);
            wpRoomSprites[17].transform.SetParent(wpScenes[17].transform);
            wpRoomSprites[17].transform.localPosition = new Vector3(0.04333333f / roomDivider, 4.23f / roomDivider);
            foreach (var sprite in wpRoomSprites)
            {
                sprite.layer = 5;
                sprite.transform.localScale = Vector3.one;
                sprite.SetActive(true);
                var sr = sprite.GetComponent<SpriteRenderer>();
                sr.material = roomMat;
                sr.sprite = SpriteDict.Get(sprite.name);
                sr.sortingLayerID = 629535577;
                sr.sortingOrder = 0;
            }
            wpRoomSprites[17].transform.localScale = new Vector3(1.0f / 0.93f, 1.0f / 1.04f, 1.0f);

            var pathOfPainArea = UObject.Instantiate(subAreaPrefab, wpScenes[15].transform);
            pathOfPainArea.SetActive(true);
            pathOfPainArea.transform.localPosition = new Vector3(5.875f, -0.8f, pathOfPainArea.transform.localPosition.z);
            pathOfPainArea.GetComponent<SetTextMeshProGameText>().convName = Consts.LanguageStrings.PathOfPainKey;

            var workshopArea = UObject.Instantiate(subAreaPrefab, wpScenes[7].transform);
            workshopArea.SetActive(true);
            workshopArea.transform.localPosition = new Vector3(5f, -1.25f, workshopArea.transform.localPosition.z);
            workshopArea.GetComponent<SetTextMeshProGameText>().convName = Consts.LanguageStrings.WorkshopKey;

            var creditsArea = UObject.Instantiate(subAreaPrefab, wpScenes[6].transform);
            creditsArea.SetActive(true);
            creditsArea.transform.localPosition = new Vector3(7f, -1.5f, creditsArea.transform.localPosition.z);
            creditsArea.GetComponent<SetTextMeshProGameText>().convName = Consts.LanguageStrings.CreditsKey;
            var rectT = creditsArea.GetComponent<RectTransform>();
            rectT.sizeDelta = new Vector2(rectT.sizeDelta.x + 1, rectT.sizeDelta.y);

            #region Benches

            var tmp = UObject.Instantiate(benchPrefab, wpRoomSprites[0].transform);
            tmp.transform.localPosition = new Vector3(-0.4f, -0.5f, -0.013f);
            tmp.SetActive(true);
            var tmp2 = UObject.Instantiate(benchPrefab, wpRoomSprites[2].transform);
            tmp2.transform.localPosition = new Vector3(0.05f, -0.15f, -0.013f);
            tmp2.SetActive(true);
            var tmp3 = UObject.Instantiate(benchPrefab, wpRoomSprites[5].transform);
            tmp3.transform.localPosition = new Vector3(-0.1f, 0.45f, -0.013f);
            tmp3.SetActive(true);

            #endregion

            #region Area Name

            var wpAreaNameArea = UObject.Instantiate(areaNamePrefab, areaWhitePalace.transform);
            wpAreaNameArea.transform.localPosition = new Vector3(6.433125f, 1.6825f, wpAreaNameArea.transform.localPosition.z);
            wpAreaNameArea.GetComponent<SetTextMeshProGameText>().convName = "WHITE_PALACE";
            wpAreaNameArea.SetActive(true);

            #endregion

            areaWhitePalace.SetActive(true);
            tmpDict.Add(
                "WHITE_PALACE",
                new SCustomArea()
                {
                    areaGameObject = areaWhitePalace,
                    //                                  -24.5f
                    cameraPosition = new Vector3(3.07f, -23f, 18f),
                    mapZoneStrings = new List<string>() { "WHITE_PALACE" },
                    //playerDataBoolGotAreaMap = "AdditionalMapsGotWpMap"
                    playerDataBoolGotAreaMap = "AdditionalMapsGotWpMap"
                }
            );

            #endregion

            #region Godhome Map

            var areaGodhome = UObject.Instantiate(gameMapBetter.areaCliffs, gameMapBetter.transform);
            areaGodhome.SetActive(true);

            for (var i = 0; i < areaGodhome.transform.childCount; i++)
            {
                UObject.Destroy(areaGodhome.transform.GetChild(i).gameObject);
            }

            areaGodhome.name = "GODS_GLORY";
            areaGodhome.layer = 5;
            areaGodhome.transform.localScale = Vector3.one;
            areaGodhome.transform.localPosition = new Vector3(5.5f, 14.5f, gameMapBetter.areaCliffs.transform.localPosition.z);

            var ghScenes = new List<GameObject>() {
                new("GG_Atrium", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
                new("GG_Atrium_Roof", typeof(SpriteRenderer), typeof(RoughMapRoom), typeof(MappedCustomRoom)),
            };
            foreach (var sceneGo in ghScenes)
            {
                sceneGo.transform.SetParent(areaGodhome.transform);
                sceneGo.layer = 5;
                sceneGo.transform.localScale = Vector3.one;
                sceneGo.SetActive(true);
                var sr = sceneGo.GetComponent<SpriteRenderer>();
                sr.material = roomMat;
                sr.sprite = SpriteDict.Get(sceneGo.name);
                sr.sortingLayerID = 629535577;
                sr.sortingOrder = 0;
                var rmr = sceneGo.GetComponent<RoughMapRoom>();
                rmr.fullSprite = sr.sprite;
            }
            ghScenes[0].transform.localPosition = new Vector3(0.3687f, -2.678f, tmpChildZ);
            ghScenes[1].transform.localPosition = new Vector3(-0.708f, 0.65f, tmpChildZ);

            var ghRoomSprites = new List<GameObject>() {
                new("RGH0", typeof(SpriteRenderer)),
                new("RGH1", typeof(SpriteRenderer)),
            };
            ghRoomSprites[0].transform.SetParent(ghScenes[0].transform);
            ghRoomSprites[0].transform.localPosition = new Vector3(-0.408678f, 0.138f);
            ghRoomSprites[1].transform.SetParent(ghScenes[1].transform);
            ghRoomSprites[1].transform.localPosition = new Vector3(0.6480024f, -0.187f);
            foreach (var sprite in ghRoomSprites)
            {
                sprite.layer = 5;
                sprite.transform.localScale = Vector3.one;
                sprite.SetActive(true);
                var sr = sprite.GetComponent<SpriteRenderer>();
                sr.material = roomMat;
                sr.sprite = SpriteDict.Get(sprite.name);
                sr.sortingLayerID = 629535577;
                sr.sortingOrder = 0;
            }

            var creditsArea2 = UObject.Instantiate(subAreaPrefab, ghScenes[0].transform);
            creditsArea2.SetActive(true);
            creditsArea2.transform.localPosition = new Vector3(8f, 1.5f, creditsArea2.transform.localPosition.z);
            creditsArea2.GetComponent<SetTextMeshProGameText>().convName = Consts.LanguageStrings.CreditsKey;
            var rectT2 = creditsArea2.GetComponent<RectTransform>();
            rectT2.sizeDelta = new Vector2(rectT2.sizeDelta.x + 1, rectT2.sizeDelta.y);

            #region Benches

            var tmpBench = UObject.Instantiate(benchPrefab, ghRoomSprites[0].transform);
            tmpBench.transform.localPosition = new Vector3(0.925f, 0.5f, -0.013f);
            tmpBench.SetActive(true);
            var tmpBench2 = UObject.Instantiate(benchPrefab, ghRoomSprites[1].transform);
            tmpBench2.transform.localPosition = new Vector3(0.8f, -0.1f, -0.013f);
            tmpBench2.SetActive(true);

            #endregion

            #region Area Name

            var ghAreaNameArea = UObject.Instantiate(areaNamePrefab, areaGodhome.transform);
            ghAreaNameArea.transform.localPosition = new Vector3(5.208f, 0.65f, ghAreaNameArea.transform.localPosition.z);
            ghAreaNameArea.GetComponent<SetTextMeshProGameText>().convName = "GODS_GLORY";
            ghAreaNameArea.SetActive(true);

            #endregion

            areaGodhome.SetActive(true);
            tmpDict.Add(
                "GODS_GLORY",
                new SCustomArea()
                {
                    areaGameObject = areaGodhome,
                    cameraPosition = new Vector3(-8.5f, -22f, 18f),
                    mapZoneStrings = new List<string>() { "GODS_GLORY" },
                    playerDataBoolGotAreaMap = "AdditionalMapsGotGhMap"
                }
            );

            #endregion

            UObject.Destroy(subAreaPrefab);
            Log("~gameMapCallback");
            return tmpDict;
        }

        private void InitGlobalSettings()
        {
            // Found in a project, might help saving, don't know, but who cares
        }

        private void InitSaveSettings(SaveGameData data)
        {
            // Found in a project, might help saving, don't know, but who cares
        }

        private void InitCallbacks()
        {
            // Hooks
            ModHooks.LanguageGetHook += OnLanguageGetHook;
            ModHooks.GetPlayerBoolHook += OnGetPlayerBoolHook;
            ModHooks.SetPlayerBoolHook += OnSetPlayerBoolHook;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneManagerActiveSceneChanged;
        }

        private readonly List<string> _sceneList = new()
        {
            "White_Palace_03_hub",
            "GG_Atrium",
            "GG_Workshop",
            "GG_Blue_Room"
        };
        private void OnSceneManagerActiveSceneChanged(Scene from, Scene to)
        {
            if (!_sceneList.Contains(to.name)) return;

            // ToDo make this optional
            if (to.name.Equals("White_Palace_03_hub"))
            {
                //var sm = to.Find("_SceneManager").GetComponent<SceneManager>();
                //sm.mapZone = MapZone.WHITE_PALACE;

                MakeSpriteGo("sm1", GetSprite(TextureStrings.Sm1Key), new Vector3(55.2f, 45.1f, 0.1f), Vector3.zero);
                MakeSpriteGo("sm2", GetSprite(TextureStrings.Sm2Key), new Vector3(56.3f, 45.1f, 0.1f), new Vector3(0, 0, -1.374f));
                MakeSpriteGo("sm2", GetSprite(TextureStrings.Sm2Key), new Vector3(57.5f, 45.1f, 0.17f), new Vector3(0, 0, 14.291f));
                MakeSpriteGo("sm3", GetSprite(TextureStrings.Sm3Key), new Vector3(58.6f, 45.2f, 0.1f), Vector3.zero);
                MakeSpriteGo("sm3", GetSprite(TextureStrings.Sm3Key), new Vector3(59.7f, 45.2f, 0.1f), new Vector3(0, 0, -5.748f));

                #region Shiny FSM

                var shinyParent = UObject.Instantiate(_shinyPrefab);
                shinyParent.name = "Map";
                shinyParent.SetActive(false);
                shinyParent.transform.GetChild(0).gameObject.SetActive(true);
                shinyParent.transform.position = new Vector3(57.5f, 45, 0.05f);

                var shinyFsm = shinyParent.transform.GetChild(0).gameObject.LocateMyFSM("Shiny Control");
                var shinyFsmVars = shinyFsm.FsmVariables;
                shinyFsmVars.FindFsmInt("Charm ID").Value = 0;
                shinyFsmVars.FindFsmInt("Type").Value = 0;
                shinyFsmVars.FindFsmBool("Activated").Value = false;
                shinyFsmVars.FindFsmBool("Charm").Value = false;
                shinyFsmVars.FindFsmBool("Dash Cloak").Value = false;
                shinyFsmVars.FindFsmBool("Exit Dream").Value = false;
                shinyFsmVars.FindFsmBool("Fling L").Value = false;
                shinyFsmVars.FindFsmBool("Fling On Start").Value = false;
                shinyFsmVars.FindFsmBool("Journal").Value = false;
                shinyFsmVars.FindFsmBool("King's Brand").Value = false;
                shinyFsmVars.FindFsmBool("Mantis Claw").Value = false;
                shinyFsmVars.FindFsmBool("Pure Seed").Value = false;
                shinyFsmVars.FindFsmBool("Quake").Value = false;
                shinyFsmVars.FindFsmBool("Show Charm Tute").Value = false;
                shinyFsmVars.FindFsmBool("Slug Fling").Value = false;
                shinyFsmVars.FindFsmBool("Super Dash").Value = false;
                shinyFsmVars.FindFsmString("Item Name").Value = Consts.LanguageStrings.WpMapKey;
                shinyFsmVars.FindFsmString("PD Bool Name").Value = "AdditionalMapsGotWpMap";

                var isAction = shinyFsm.GetAction<IntSwitch>("Trinket Type", 0);
                var tmpCompareTo = new List<FsmInt>(isAction.compareTo);
                tmpCompareTo.Add(tmpCompareTo.Count + 1);
                isAction.compareTo = tmpCompareTo.ToArray();
                shinyFsmVars.FindFsmInt("Trinket Num").Value = tmpCompareTo.Count;
                var tmpSendEvent = new List<FsmEvent>(isAction.sendEvent);
                tmpSendEvent.Add(FsmEvent.FindEvent("PURE SEED"));
                isAction.sendEvent = tmpSendEvent.ToArray();

                shinyFsm.CopyState("Love Key", "Necklace");

                shinyFsm.GetAction<SetPlayerDataBool>("Necklace", 0).boolName = "AdditionalMapsGotWpMap";
                shinyFsm.GetAction<SetSpriteRendererSprite>("Necklace", 1).sprite = GetSprite(TextureStrings.MapKey);
                shinyFsm.GetAction<GetLanguageString>("Necklace", 2).convName = Consts.LanguageStrings.WpMapKey;

                shinyFsm.AddTransition("Trinket Type", "PURE SEED", "Necklace");

                shinyParent.SetActive(true);

                #endregion
            }
            else if (to.name.Equals("GG_Atrium"))
            {
                var blueDoor = to.Find("door1_blueRoom");
                _setCompassPointPrefab.CopyOnto(blueDoor);
                var fsm = blueDoor.LocateMyFSM("Set Compass Point");
                fsm.Preprocess();
                var workshopDoor = to.Find("Door_Workshop");
                _setCompassPointPrefab.CopyOnto(workshopDoor);
                var fsm2 = workshopDoor.LocateMyFSM("Set Compass Point");
                fsm2.Preprocess();
                
                MakeSpriteGo("sm1", GetSprite(TextureStrings.Sm1Key), new Vector3(115.7f, 60.1f, 0.1f), Vector3.zero);
                MakeSpriteGo("sm2", GetSprite(TextureStrings.Sm2Key), new Vector3(116.7f, 60.1f, 0.1f), new Vector3(0, 0, -1.374f));
                MakeSpriteGo("sm2", GetSprite(TextureStrings.Sm2Key), new Vector3(118.0f, 60.1f, 0.17f), new Vector3(0, 0, 14.291f));
                MakeSpriteGo("sm3", GetSprite(TextureStrings.Sm3Key), new Vector3(119.1f, 60.2f, 0.1f), Vector3.zero);
                MakeSpriteGo("sm3", GetSprite(TextureStrings.Sm3Key), new Vector3(120.2f, 60.2f, 0.1f), new Vector3(0, 0, -5.748f));

                #region Shiny FSM

                var shinyParent = UObject.Instantiate(_shinyPrefab);
                shinyParent.name = "Map";
                shinyParent.SetActive(false);
                shinyParent.transform.GetChild(0).gameObject.SetActive(true);
                shinyParent.transform.position = new Vector3(118f, 60.5f, 0.05f);

                var shinyFsm = shinyParent.transform.GetChild(0).gameObject.LocateMyFSM("Shiny Control");
                var shinyFsmVars = shinyFsm.FsmVariables;
                shinyFsmVars.FindFsmInt("Charm ID").Value = 0;
                shinyFsmVars.FindFsmInt("Type").Value = 0;
                shinyFsmVars.FindFsmBool("Activated").Value = false;
                shinyFsmVars.FindFsmBool("Charm").Value = false;
                shinyFsmVars.FindFsmBool("Dash Cloak").Value = false;
                shinyFsmVars.FindFsmBool("Exit Dream").Value = false;
                shinyFsmVars.FindFsmBool("Fling L").Value = false;
                shinyFsmVars.FindFsmBool("Fling On Start").Value = false;
                shinyFsmVars.FindFsmBool("Journal").Value = false;
                shinyFsmVars.FindFsmBool("King's Brand").Value = false;
                shinyFsmVars.FindFsmBool("Mantis Claw").Value = false;
                shinyFsmVars.FindFsmBool("Pure Seed").Value = false;
                shinyFsmVars.FindFsmBool("Quake").Value = false;
                shinyFsmVars.FindFsmBool("Show Charm Tute").Value = false;
                shinyFsmVars.FindFsmBool("Slug Fling").Value = false;
                shinyFsmVars.FindFsmBool("Super Dash").Value = false;
                shinyFsmVars.FindFsmString("Item Name").Value = Consts.LanguageStrings.GhMapKey;
                shinyFsmVars.FindFsmString("PD Bool Name").Value = "AdditionalMapsGotGhMap";

                var isAction = shinyFsm.GetAction<IntSwitch>("Trinket Type", 0);
                var tmpCompareTo = new List<FsmInt>(isAction.compareTo);
                tmpCompareTo.Add(tmpCompareTo.Count + 1);
                isAction.compareTo = tmpCompareTo.ToArray();
                shinyFsmVars.FindFsmInt("Trinket Num").Value = tmpCompareTo.Count;
                var tmpSendEvent = new List<FsmEvent>(isAction.sendEvent);
                tmpSendEvent.Add(FsmEvent.FindEvent("PURE SEED"));
                isAction.sendEvent = tmpSendEvent.ToArray();

                shinyFsm.CopyState("Love Key", "Necklace");

                shinyFsm.GetAction<SetPlayerDataBool>("Necklace", 0).boolName = "AdditionalMapsGotGhMap";
                shinyFsm.GetAction<SetSpriteRendererSprite>("Necklace", 1).sprite = GetSprite(TextureStrings.MapKey);
                shinyFsm.GetAction<GetLanguageString>("Necklace", 2).convName = Consts.LanguageStrings.GhMapKey;

                shinyFsm.AddTransition("Trinket Type", "PURE SEED", "Necklace");

                shinyParent.SetActive(true);

                #endregion
            }
            else if (to.name.Equals("GG_Workshop"))
            {
                var smGo = to.Find("_SceneManager");
                _setCompassPointRoomPrefab.CopyOnto(smGo);
                var fsm = smGo.LocateMyFSM("map_isroom");
                fsm.Preprocess();
                //var fsmVars = fsm.FsmVariables;
                //fsmVars.GetFsmString("Map Zone").Value = "GODS_GLORY";
                //fsmVars.GetFsmString("Scene Name").Value = "GG_Atrium";
            }
            else if (to.name.Equals("GG_Blue_Room"))
            {
                var smGo = to.Find("_SceneManager");
                _setCompassPointRoomPrefab.CopyOnto(smGo);
                var fsm = smGo.LocateMyFSM("map_isroom");
                fsm.Preprocess();
                //var fsmVars = fsm.FsmVariables;
                //fsmVars.GetFsmString("Map Zone").Value = "GODS_GLORY";
                //fsmVars.GetFsmString("Scene Name").Value = "GG_Atrium";
            }
        }

        private void MakeSpriteGo(string name, Sprite sprite, Vector3 pos, Vector3 angles)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.eulerAngles = angles;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.material = new Material(Shader.Find("Sprites/Default"));
            sr.sprite = sprite;
        }

        private void OnPlayMakerFSMStart(On.PlayMakerFSM.orig_Start orig, PlayMakerFSM self)
        {
            orig(self);
            if (self.FsmName.Equals("UI Control") && self.gameObject.name.Equals("World Map"))
            {
                ChangeWpMap(self.gameObject, self.transform.GetChild(4).gameObject);
                ChangeGhMap(self.gameObject, self.transform.GetChild(4).gameObject);
            }
        }

        private void SaveTotGlobalSettings()
        {
            SaveGlobalSettings();
        }

        #region Get/Set Hooks
        private string OnLanguageGetHook(string key, string sheet, string orig)
        {
            //Log($"Sheet: {sheet}; Key: {key}");
            if (LangStrings.ContainsKey(key, sheet))
            {
                return LangStrings.Get(key, sheet);
            }
            return orig;
        }

        private bool HasGetSettingsValue<T>(string target)
        {
            var tmpField = ReflectionHelper.GetFieldInfo(typeof(AmSaveSettings), target);
            return tmpField != null && tmpField.FieldType == typeof(T);
        }
        private T GetSettingsValue<T>(string target)
        {
            return ReflectionHelper.GetField<AmSaveSettings, T>(SaveSettings, target);
        }
        private void SetSettingsValue<T>(string target, T val)
        {
            ReflectionHelper.SetField(SaveSettings, target, val);
        }

        private bool OnGetPlayerBoolHook(string target, bool orig)
        {
            if (HasGetSettingsValue<bool>(target))
            {
                return GetSettingsValue<bool>(target);
            }
            return orig;
        }
        private bool OnSetPlayerBoolHook(string target, bool orig)
        {
            if (HasGetSettingsValue<bool>(target))
            {
                SetSettingsValue(target, orig);
                // trigger map updated thing
                GameManager.instance.UpdateGameMap();
                UObject.FindObjectOfType<GameMap>().SetupMap();

                Resources.FindObjectsOfTypeAll<Transform>().First(t => t.gameObject.name.Equals("Map Update Msg"))
                    .gameObject.Spawn(Vector3.zero);
            }
            return orig;
        }

        private int OnGetPlayerIntHook(string target)
        {
            if (HasGetSettingsValue<int>(target))
            {
                return GetSettingsValue<int>(target);
            }
            return PlayerData.instance.GetIntInternal(target);
        }
        private void OnSetPlayerIntHook(string target, int val)
        {
            if (HasGetSettingsValue<int>(target))
            {
                SetSettingsValue(target, val);
                return;
            }
            PlayerData.instance.SetIntInternal(target, val);
        }
        #endregion

        #region Custom Area Test

        private void ChangeWpMap(GameObject worldMap, GameObject wideMap)
        {
            DebugLog($"!ChangeWpMap: \"{wideMap}\"");

            var customAreaName = "White_Palace";
            var boolName = "AdditionalMapsGotWpMap";
            var cameraZoomPosition = new Vector3(2.07f, -20f, -22f);
            var mapAreaPosition = new Vector3(1.02f, -1.75f, -2.3f);

            var tmpActive = wideMap.activeSelf;
            wideMap.SetActive(false);

            #region temporary Variables

            var caState = $"{customAreaName} State";
            var caLeftState = $"{customAreaName} State Left";
            var caRightState = $"{customAreaName} State Right";
            var caDownState = $"{customAreaName} State Down";
            var caZoomState = $"{customAreaName} State Zoom";

            var extraUpState = "T Up";

            #endregion

            #region Add sprite and text for custom area
            var customPart = UObject.Instantiate(wideMap.transform.GetChild(0).gameObject, wideMap.transform, true);
            customPart.SetActive(false);
            customPart.name = customAreaName;
            customPart.transform.localPosition = mapAreaPosition;
            customPart.GetComponent<SpriteRenderer>().sprite = GetSprite(TextureStrings.CustomAreaKey);
            customPart.GetComponentInChildren<SetTextMeshProGameText>().convName = customAreaName.ToUpper();
            customPart.transform.Find("Area Name").localPosition += new Vector3(-1.0f, 0, 0);
            customPart.LocateMyFSM("deactivate").FsmVariables.GetFsmString("playerData bool").Value = boolName;
            #endregion

            #region Edit World Map - UI Control FSM
            var worldMapFsm = worldMap.LocateMyFSM("UI Control");

            if (worldMapFsm.GetState("Mines").Fsm == null)
            {
                worldMapFsm.Preprocess();
            }

            var wmUcFsmVars = worldMapFsm.FsmVariables;

            #region Create Custom States
            worldMapFsm.CopyState("Mines", caState);

            worldMapFsm.CopyState("T Left", caLeftState);
            worldMapFsm.CopyState("T Right", caRightState);

            worldMapFsm.CopyState("Mi Left", caDownState);
            worldMapFsm.CopyState("To Zoom 10", caZoomState);

            worldMapFsm.CopyState("CR Up", extraUpState);
            #endregion

            #region Add Custom FSM Variable
            worldMapFsm.AddGameObjectVariable(customAreaName);
            #endregion
            #region Add FindChild Action to store Custom Area Sprite
            var tmpActionFindChild = new FindChild();
            tmpActionFindChild.gameObject = worldMapFsm.GetAction<FindChild>("Init", 10).gameObject;
            tmpActionFindChild.childName = customAreaName;
            tmpActionFindChild.storeResult = wmUcFsmVars.GetFsmGameObject(customAreaName);
            worldMapFsm.InsertAction("Init", tmpActionFindChild, 11);
            #endregion

            #region Add Custom Global Transition
            var customGlobalEvent = worldMapFsm.AddGlobalTransition($"{customAreaName.ToUpper()}_GLOBAL", caState);
            #endregion

            DebugLog("... Added Custom Global Transition...");

            // Reference to GameManager for FSM actions
            var tmpGameObject = worldMapFsm.GetAction<PlayerDataBoolTest>("D Up", 2).gameObject;

            DebugLog("... Create Custom States...");

            #region Create Custom States
            worldMapFsm.GetAction<SendEventByName>(caState, 2).eventTarget = new FsmEventTarget() { target = FsmEventTarget.EventTarget.GameObject, gameObject = new FsmOwnerDefault() { OwnerOption = OwnerDefaultOption.SpecifyGameObject, GameObject = customPart } };
            worldMapFsm.GetAction<GetLanguageString>(caState, 3).convName = $"MAP_NAME_{customAreaName.ToUpper()}";
            worldMapFsm.GetAction<SetStringValue>(caState, 5).stringValue = customAreaName.ToUpper();
            worldMapFsm.GetAction<SetVector3Value>(caState, 6).vector3Value = cameraZoomPosition;

            worldMapFsm.InsertAction(extraUpState, new PlayerDataBoolTest() { gameObject = tmpGameObject, boolName = boolName, isTrue = customGlobalEvent }, 0);
            #endregion

            worldMapFsm.ChangeTransition(caState, "UI LEFT", caLeftState);
            worldMapFsm.ChangeTransition(caState, "UI RIGHT", caRightState);
            worldMapFsm.ChangeTransition(caState, "UI DOWN", caDownState);
            worldMapFsm.ChangeTransition(caState, "UI CONFIRM", caZoomState);
            worldMapFsm.ChangeTransition(caLeftState, "FINISHED", caState);
            worldMapFsm.ChangeTransition(caRightState, "FINISHED", caState);
            worldMapFsm.ChangeTransition(caDownState, "FINISHED", caState);

            worldMapFsm.AddTransition("Town", "UI UP", extraUpState);
            worldMapFsm.ChangeTransition("Town", "UI UP", extraUpState);
            worldMapFsm.AddTransition(extraUpState, "FINISHED", "Town");
            worldMapFsm.ChangeTransition(extraUpState, "FINISHED", "Town");

            #endregion
            wideMap.SetActive(tmpActive);

            DebugLog("~ChangeWpMap");
        }
        
        private void ChangeGhMap(GameObject worldMap, GameObject wideMap)
        {
            DebugLog($"!ChangeWpMap: \"{wideMap}\"");

            var customAreaName = "Godhome";
            var boolName = "AdditionalMapsGotGhMap";
            var cameraZoomPosition = new Vector3(-8.07f, -16f, -22f);
            var mapAreaPosition = new Vector3(6.02f, -2f, -2.3f);

            var tmpActive = wideMap.activeSelf;
            wideMap.SetActive(false);

            #region temporary Variables

            var caState = $"{customAreaName} State";
            var caLeftState = $"{customAreaName} State Left";
            var caRightState = $"{customAreaName} State Right";
            var caUpState = $"{customAreaName} State Up";
            var caDownState = $"{customAreaName} State Down";
            var caZoomState = $"{customAreaName} State Zoom";

            var extraUpState = "Mi Up";

            #endregion

            #region Add sprite and text for custom area
            var customPart = UObject.Instantiate(wideMap.transform.GetChild(0).gameObject, wideMap.transform, true);
            customPart.SetActive(false);
            customPart.name = customAreaName;
            customPart.transform.localPosition = mapAreaPosition;
            customPart.GetComponent<SpriteRenderer>().sprite = GetSprite(TextureStrings.CustomAreaKey);
            customPart.GetComponentInChildren<SetTextMeshProGameText>().convName = customAreaName.ToUpper();
            customPart.transform.Find("Area Name").localPosition += new Vector3(-1.0f, 0, 0);
            customPart.LocateMyFSM("deactivate").FsmVariables.GetFsmString("playerData bool").Value = boolName;
            #endregion

            #region Edit World Map - UI Control FSM
            var worldMapFsm = worldMap.LocateMyFSM("UI Control");

            if (worldMapFsm.GetState("Mines").Fsm == null)
            {
                worldMapFsm.Preprocess();
            }

            var wmUcFsmVars = worldMapFsm.FsmVariables;

            #region Create Custom States
            worldMapFsm.CopyState("Mines", caState);

            worldMapFsm.CopyState("Mi Left", caLeftState);
            worldMapFsm.CopyState("Mi Right", caRightState);

            worldMapFsm.CopyState("T Right", caDownState);
            worldMapFsm.CopyState("To Zoom 11", caZoomState);

            worldMapFsm.CopyState("RG Up", extraUpState);
            #endregion

            #region Add Custom FSM Variable
            worldMapFsm.AddGameObjectVariable(customAreaName);
            #endregion
            #region Add FindChild Action to store Custom Area Sprite
            var tmpActionFindChild = new FindChild();
            tmpActionFindChild.gameObject = worldMapFsm.GetAction<FindChild>("Init", 10).gameObject;
            tmpActionFindChild.childName = customAreaName;
            tmpActionFindChild.storeResult = wmUcFsmVars.GetFsmGameObject(customAreaName);
            worldMapFsm.InsertAction("Init", tmpActionFindChild, 11);
            #endregion

            #region Add Custom Global Transition
            var customGlobalEvent = worldMapFsm.AddGlobalTransition($"{customAreaName.ToUpper()}_GLOBAL", caState);
            #endregion

            DebugLog("... Added Custom Global Transition...");

            // Reference to GameManager for FSM actions
            var tmpGameObject = worldMapFsm.GetAction<PlayerDataBoolTest>("D Up", 2).gameObject;

            DebugLog("... Create Custom States...");

            #region Create Custom States
            worldMapFsm.GetAction<SendEventByName>(caState, 2).eventTarget = new FsmEventTarget() { target = FsmEventTarget.EventTarget.GameObject, gameObject = new FsmOwnerDefault() { OwnerOption = OwnerDefaultOption.SpecifyGameObject, GameObject = customPart } };
            worldMapFsm.GetAction<GetLanguageString>(caState, 3).convName = $"MAP_NAME_{customAreaName.ToUpper()}";
            worldMapFsm.GetAction<SetStringValue>(caState, 5).stringValue = customAreaName.ToUpper();
            worldMapFsm.GetAction<SetVector3Value>(caState, 6).vector3Value = cameraZoomPosition;

            worldMapFsm.InsertAction(extraUpState, new PlayerDataBoolTest() { gameObject = tmpGameObject, boolName = boolName, isTrue = customGlobalEvent }, 0);
            #endregion

            worldMapFsm.ChangeTransition(caState, "UI LEFT", caLeftState);
            worldMapFsm.ChangeTransition(caState, "UI RIGHT", caRightState);
            worldMapFsm.ChangeTransition(caState, "UI DOWN", caDownState);
            worldMapFsm.ChangeTransition(caState, "UI CONFIRM", caZoomState);
            worldMapFsm.ChangeTransition(caLeftState, "FINISHED", caState);
            worldMapFsm.ChangeTransition(caRightState, "FINISHED", caState);
            worldMapFsm.ChangeTransition(caDownState, "FINISHED", caState);

            worldMapFsm.AddTransition("Mines", "UI UP", extraUpState);
            worldMapFsm.ChangeTransition("Mines", "UI UP", extraUpState);
            worldMapFsm.AddTransition(extraUpState, "FINISHED", "Mines");
            worldMapFsm.ChangeTransition(extraUpState, "FINISHED", "Mines");
            #endregion
            wideMap.SetActive(tmpActive);

            DebugLog("~ChangeWpMap");
        }

        #endregion

        private static void DebugLog(String msg)
        {
            Logger.Log($"[AdditionalMaps] - {msg}");
            Debug.Log($"[AdditionalMaps] - {msg}");
        }
        private static void DebugLog(object msg)
        {
            DebugLog($"{msg}");
        }
        private static void SetInactive(GameObject go)
        {
            if (go != null)
            {
                UObject.DontDestroyOnLoad(go);
                go.SetActive(false);
            }
        }
        private static void SetInactive(UnityEngine.Object go)
        {
            if (go != null)
            {
                UObject.DontDestroyOnLoad(go);
            }
        }
    }
}
