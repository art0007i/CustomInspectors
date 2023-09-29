using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
        public override string Version => "2.0.0";
        public override string Link => "https://github.com/art0007i/CustomInspectors/";
        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony("me.art0007i.CustomInspectors");
            OurItem = SpecialItemsLib.SpecialItemsLib.RegisterItem(INSPECTOR_TAG, "Inspector");
            harmony.PatchAll();
        }
        private static string INSPECTOR_TAG { get { return "custom_inspector_panel"; } }
        private static CustomSpecialItem OurItem;

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
        [HarmonyPatch(typeof(SceneInspector), "OnAttach")]
        class SceneInspector_OnAttach_Patch
        {
            static bool Prefix(SceneInspector __instance)
            {
                if (OurItem.Uri == null) return true;
                var translator = new ReferenceTranslator();
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

                var ttask = Engine.Current.AssetManager.GatherAssetFile(uri, 20, null);
                ttask.AsTask().Wait();
                string text = ttask.Result;
                if (text == null || !File.Exists(text))
                {
                    return false;
                }

                // this is where we load the json and parse it to merge guids with refids
                DataTreeDictionary node = DataTreeConverter.Load(text, uri);
                var rootNode = node.TryGetDictionary("Object");
                if (rootNode.TryGetDictionary("Name").TryGetNode("Data").LoadString() == "Holder")
                {
                    rootNode = rootNode.TryGetList("Children").Children[0] as DataTreeDictionary;
                    node.Children["Object"] = rootNode;
                }
                var topLevel = rootNode.TryGetDictionary("Components").TryGetList("Data");
                foreach (var dataNode in topLevel.Children)
                {
                    var dictNode = (dataNode as DataTreeDictionary);
                    var str = dictNode.TryGetNode("Type").LoadString();
                    if (str == typeof(SceneInspector).ToString())
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
                    var scl = __instance.Slot.GlobalScale;

                    __instance.Slot.LoadObject(node, refTranslator: translator);
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