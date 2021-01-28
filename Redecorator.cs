using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace DVDecorator
{
    public static class Redecorator
    {
        public static UnityModManager.ModEntry ModEntry = null;

        public static readonly Dictionary<string, ObjectGroup> ObjectsToTexture = new Dictionary<string, ObjectGroup>();
        public static readonly HashSet<string> TargetNames = new HashSet<string>();

        public static List<GameObject> TargetCache = null;

        public static bool Load( UnityModManager.ModEntry modEntry )
        {
            ModEntry = modEntry;

            try
            {
                LoadTextures();
            }
            catch( Exception ex )
            {
                modEntry.Logger.Error(ex.Message);
                return false;
            }

            var harmony = new Harmony("cc.foxden.decorator");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            return true;
        }

        static void LoadTextures()
        {
            var texDir = new DirectoryInfo(Path.Combine(ModEntry.Path, "Textures"));

            if( !texDir.Exists ) return;

            // iterate each texture pack folder
            foreach( var packDir in texDir.GetDirectories() )
            {
                ModEntry.Logger.Log($"Loading texture pack {packDir.Name}");

                // we'll swap the object and texture pack level so it goes:
                // - Object 1
                //     |- Pack A files
                //     |- Pack B files
                // - Object 2
                //     |- Pack A files
                //     |- Pack C files

                // in each pack, iterate each object group
                foreach( var objectDir in packDir.GetDirectories() )
                {
                    string objectName = objectDir.Name;
                    if( !ObjectsToTexture.TryGetValue(objectName, out ObjectGroup objectTypeGroup) )
                    {
                        // no existing textures for this object, create new group
                        objectTypeGroup = new ObjectGroup(objectName);
                        ObjectsToTexture.Add(objectName, objectTypeGroup);
                    }

                    objectTypeGroup.LoadTexturePackFiles(objectDir);
                    TargetNames.Add(objectName);
                }
            }
        }

        public static Texture2D GetMaterialTexture( Material mat, string texName )
        {
            if( mat == null || !mat.HasProperty(texName) )
            {
                return null;
            }

            return mat.GetTexture(texName) as Texture2D;
        }
    }

    [HarmonyPatch(typeof(StationController), "Update")]
    public static class StationController_Update_Patch
    {
        static bool Initialized = false;

        static void Postfix()
        {
            // attempt repaint one time after loading finished
            if( !SaveLoadController.carsAndJobsLoadingFinished || Initialized ) return;

            Initialized = true;

            // Cache all targets
            Redecorator.TargetCache = GameObject.FindObjectsOfType<GameObject>()
                .Where(obj => Redecorator.TargetNames.Contains(obj.name)).ToList();

            foreach( var objectGroupKVP in Redecorator.ObjectsToTexture )
            {
                objectGroupKVP.Value.AttemptRepaints();
            }
        }
    }
}
