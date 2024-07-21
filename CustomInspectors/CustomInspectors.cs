using HarmonyLib;
using ResoniteModLoader;
using System;
using FrooxEngine.Store;
using System.Collections.Generic;
using FrooxEngine;
using System.IO;
using Elements.Core;
using SpecialItemsLib;

namespace CustomInspectors
{
    public class CustomInspectors : ResoniteMod
    {
        public override string Name => "CustomInspectors";
        public override string Author => "art0007i";
        public override string Version => "2.1.3";
        public override string Link => "https://github.com/art0007i/CustomInspectors/";

        [AutoRegisterConfigKey]
        public static ModConfigurationKey<float> KEY_INSPECTOR_SCALE = new("inspector_scale", "Changes the default inspector scale to this value.\n0.0005 by default, because that's what the actual inspectors default scale is.", () => 0.0005f);

        public static ModConfiguration config;

        public override void OnEngineInit()
        {
            config = GetConfiguration();
            Harmony harmony = new Harmony("me.art0007i.CustomInspectors");

            // Register the special item.
            // The readable name will be shown to users in the inventory in this format: "Set {0}"
            // In this case it will be "Set Inspector"
            OurItem = SpecialItemsLib.SpecialItemsLib.RegisterItem(INSPECTOR_TAG, "Inspector");
            harmony.PatchAll();
        }

        // This is the tag that the custom item will have.
        // It is recommended to have at least 1 _ character in the tag,
        // so that it's impossible to craft an item that will show up as favoritable without the required component
        private const string INSPECTOR_TAG = "custom_inspector_panel";

        // This is the interface that the SpecialItemLib gives us. We can use it to read the favorite url.
        private static CustomSpecialItem OurItem;

        // This is the function that determines whether an item should be favoritable.
        // Note: This is called recursively for every slot of an object whenever it is saved.
        [HarmonyPatch(typeof(SlotHelper), "GenerateTags", new Type[] { typeof(Slot), typeof(HashSet<string>) })]
        class SlotHelper_GenerateTags_Patch
        {
            static void Postfix(Slot slot, HashSet<string> tags)
            {
                if (slot.GetComponent<SceneInspector>() != null)
                {
                    tags.Add(INSPECTOR_TAG);
                }
            }
        }

        // This is where we add our code to replace the default spawned item with a custom one.
        [HarmonyPatch(typeof(SceneInspector), "OnAttach")]
        class SceneInspector_OnAttach_Patch
        {
            static bool Prefix(SceneInspector __instance)
            {
                // If no favorite item has been selected spawn the default one
                if (OurItem.Uri == null) return true;

                // If the url is a record url, it needs to be converted to an asset url
                var uri = OurItem.Uri;
                if ( uri.Scheme == Engine.Current.Cloud.Platform.RecordScheme)
                {
                    var ctask = Engine.Current.Cloud.Records.GetRecordCached<Record>(uri, null);
                    ctask.Wait();
                    SkyFrost.Base.CloudResult<Record> cloudResult = ctask.Result;
                    if (cloudResult.IsError)
                    {
                        return true;
                    }
                    uri = new Uri(cloudResult.Entity.AssetURI);
                }

                // Download the favorite item
                var ttask = Engine.Current.AssetManager.GatherAssetFile(uri, 20, null);
                ttask.AsTask().Wait();
                string text = ttask.Result;
                if (text == null || !File.Exists(text))
                {
                    return true;
                }

                // All the code under this is very implementation specific.
                // In this case it's actually quite complex because of how inspectors are generated.

                // this is where we load the item and parse it to merge guids with refids
                DataTreeDictionary node = DataTreeConverter.Load(text, uri);

                var isNewTypes = node?.TryGetDictionary("FeatureFlags")?.TryGetNode("TypeManagement") != null;

                var rootNode = node.TryGetDictionary("Object");
                if (rootNode.TryGetDictionary("Name").TryGetNode("Data").LoadString() == "Holder")
                {
                    rootNode = rootNode.TryGetList("Children").Children[0] as DataTreeDictionary;
                    node.Children["Object"] = rootNode;
                }
                var topLevel = rootNode.TryGetDictionary("Components").TryGetList("Data");
                var translator = new ReferenceTranslator();
                foreach (var dataNode in topLevel.Children)
                {
                    var dictNode = (dataNode as DataTreeDictionary);
                    var typeNode = dictNode.TryGetNode("Type");
                    var check = false;
                    if (isNewTypes)
                    {
                        var typeIdx = typeNode.LoadInt();
                        var typeList = node.TryGetList("Types");
                        if(typeList?.Count > typeIdx)
                        {
                            check = __instance.World.Types.DecodeType(typeList[typeIdx].LoadString()) == typeof(SceneInspector);
                        }
                    }
                    else
                    {
                        check = typeNode.LoadString() == typeof(SceneInspector).ToString();
                    }

                    if (check)
                    {
                        var dataDict = dictNode.TryGetDictionary("Data");

                        // Component guid merge
                        translator.Associate(__instance.ReferenceID, new Guid(dataDict.TryGetNode("ID").LoadString()));
                        dataDict.Children["ID"] = new DataTreeValue(Guid.NewGuid().ToString());

                        __instance.ForeachSyncMember<IWorldElement>((member) =>
                        {
                            var guidStr = dataDict.TryGetDictionary(member.Name)?.TryGetNode("ID").LoadString();
                            if (guidStr != null)
                            {   
                                // Sync member guids merge
                                translator.Associate(member.ReferenceID, new Guid(guidStr));
                                dataDict.TryGetDictionary(member.Name).Children["ID"] = new DataTreeValue(Guid.NewGuid().ToString());
                            }
                        });
                        break;
                    }
                }

                // now time to actually load the object
                if (!__instance.Slot.IsDestroyed)
                {
                    var pos = __instance.Slot.GlobalPosition;
                    var rot = __instance.Slot.GlobalRotation;
                    var scl = __instance.Slot.GlobalScale * config.GetValue(KEY_INSPECTOR_SCALE);

                    __instance.Slot.LoadObject(node, null, refTranslator: translator);
                    var old = __instance.Slot.GetComponent<SceneInspector>((insp) => insp != __instance);

                    var rt = AccessTools.Field(typeof(SceneInspector), "_rootText");
                    var ct = AccessTools.Field(typeof(SceneInspector), "_componentText");
                    var hcr = AccessTools.Field(typeof(SceneInspector), "_hierarchyContentRoot");
                    var ccr = AccessTools.Field(typeof(SceneInspector), "_componentsContentRoot");

                    (rt.GetValue(__instance) as SyncRef<Sync<string>>).Target = (rt.GetValue(old) as SyncRef<Sync<string>>).Target;
                    (ct.GetValue(__instance) as SyncRef<Sync<string>>).Target = (ct.GetValue(old) as SyncRef<Sync<string>>).Target;
                    (hcr.GetValue(__instance) as SyncRef<Slot>).Target = (hcr.GetValue(old) as SyncRef<Slot>).Target;
                    (ccr.GetValue(__instance) as SyncRef<Slot>).Target = (ccr.GetValue(old) as SyncRef<Slot>).Target;

                    old.Destroy(false);

                    __instance.Enabled = true;
                    __instance.Slot.GlobalPosition = pos;
                    __instance.Slot.GlobalRotation = rot;
                    __instance.Slot.GlobalScale = scl;

                }
                return false;
            }
        }
    }
}