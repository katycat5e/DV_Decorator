﻿using System;
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

        public static readonly List<RepaintGroup> ObjectRepaints = new List<RepaintGroup>();
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

            foreach( var subDir in texDir.GetDirectories() )
            {
                var newGroup = new RepaintGroup(subDir.Name);
                ModEntry.Logger.Log($"Found scheme for object {subDir.Name}");

                foreach( var image in subDir.GetFiles("*.png") )
                {
                    newGroup.LoadTexture(image.FullName);
                    ModEntry.Logger.Log($"Added texture {image.Name} to object {subDir.Name}");
                }

                ObjectRepaints.Add(newGroup);
                TargetNames.Add(newGroup.ObjectName);
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

            foreach( var group in Redecorator.ObjectRepaints )
            {
                group.AttemptRepaint();
            }
        }
    }
}
