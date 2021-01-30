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

                // check for aliases file
                string aliasFile = Path.Combine(packDir.FullName, "aliases.txt");
                if( File.Exists(aliasFile) )
                {
                    ProcessAliases(aliasFile, packDir.Name);
                }
            }
        }

        private static readonly Regex AliasRegex = new Regex(@"([^=]+)=([^=]+)", RegexOptions.Compiled);
        public static void ProcessAliases( string aliasFilePath, string packName )
        {
            string[] aliases = File.ReadAllLines(aliasFilePath);
            int lineNum = 1;

            foreach( string line in aliases )
            {
                if( string.IsNullOrWhiteSpace(line) ) continue;

                var match = AliasRegex.Match(line);
                if( match.Success )
                {
                    string aliasedName = match.Groups[1].Value;
                    string sourceName = match.Groups[2].Value;

                    if( ObjectsToTexture.TryGetValue(sourceName, out ObjectGroup sourceGroup) )
                    {
                        TexturePackRoot sourceTextures = sourceGroup.TexturePacks.Find(tpg => tpg.PackName.Equals(packName));
                        if( sourceTextures != null )
                        {
                            // found source
                            if( !ObjectsToTexture.TryGetValue(aliasedName, out ObjectGroup aliasedGroup) )
                            {
                                // create new group if it doesn't exist yet
                                aliasedGroup = new ObjectGroup(aliasedName);
                                ObjectsToTexture.Add(aliasedName, aliasedGroup);
                                TargetNames.Add(aliasedName);
                            }

                            // copy the texture references from the source to the aliased object
                            var newGroup = new HeirarchicalTextureSet(aliasedName, sourceTextures.TextureSet);
                            aliasedGroup.TexturePacks.Add(new TexturePackRoot(packName, newGroup));

                            ModEntry.Logger.Log($"Object {aliasedName} will use same textures as {sourceName}");
                        }
                        else
                        {
                            ModEntry.Logger.Error($"Pack does not contain textures for source object {sourceName}, line {lineNum} of alias file {aliasFilePath}");
                        }
                    }
                    else
                    {
                        ModEntry.Logger.Error($"Source object {sourceName} not found on line {lineNum} of alias file {aliasFilePath}");
                    }
                }
                else
                {
                    ModEntry.Logger.Error($"Invalid line {lineNum} of alias file {aliasFilePath}");
                }

                lineNum++;
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
