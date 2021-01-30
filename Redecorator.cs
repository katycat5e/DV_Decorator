using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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

        // Folder structure:

        //  Textures
        //      |- <Pack Directory>
        //          |- <Parent Object 1>
        //          |   |- <Child Object A>
        //          |   |   |- Texture_1.png
        //          |   |   |- Texture_2.png
        //          |   |-------------------
        //          |   |
        //          |   |- <Child Object B>
        //          |   |   |- Texture_3.png
        //          |-----------------------
        //          |
        //          |- <Parent Object 2> ...

        static void LoadTextures()
        {
            var texDir = new DirectoryInfo(Path.Combine(ModEntry.Path, "Textures"));

            if( !texDir.Exists ) return;

            // we need to swap the texture pack and root object level

            // iterate each texture pack folder
            foreach( var packDir in texDir.GetDirectories() )
            {
                string packName = packDir.Name;
                ModEntry.Logger.Log($"Loading texture pack {packName}");

                // in each pack, iterate each object group
                foreach( var rootObjectDir in packDir.GetDirectories() )
                {
                    string rootObjectName = rootObjectDir.Name;
                    if( !ObjectsToTexture.TryGetValue(rootObjectName, out ObjectGroup objectTypeGroup) )
                    {
                        // no existing textures for this object, create new group
                        objectTypeGroup = new ObjectGroup(rootObjectName);
                        ObjectsToTexture.Add(rootObjectName, objectTypeGroup);
                    }

                    // now we load the contents of the object directory recursively
                    objectTypeGroup.LoadTexturePackRoot(rootObjectDir, packName);

                    TargetNames.Add(rootObjectName);
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
