using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DVDecorator
{
    public class ObjectGroup
    {
        public string ObjectName;
        public readonly List<TexturePackGroup> TexturePacks = new List<TexturePackGroup>();

        public ObjectGroup( string objName )
        {
            ObjectName = objName;
        }

        public void LoadTexturePackFiles( DirectoryInfo objDirectory )
        {
            var texGroup = new TexturePackGroup(ObjectName);

            foreach( var image in objDirectory.GetFiles("*.png") )
            {
                texGroup.LoadTexture(image.FullName);
            }

            TexturePacks.Add(texGroup);
        }

        public void AttemptRepaints()
        {
            if( TexturePacks.Count == 0 )
            {
                Redecorator.ModEntry.Logger.Warning($"Something's wrong - object {ObjectName} has no textures loaded");
                return;
            }

            IEnumerable<GameObject> targets = Redecorator.TargetCache.Where(obj => string.Equals(obj.name, ObjectName));
            
            bool targetFound = false;
            TexturePacks.Shuffle();
            int packIdx = 0;

            foreach( GameObject target in targets )
            {
                targetFound = true;

                TexturePacks[packIdx].AttemptRepaint(target);

                packIdx += 1;
                if( packIdx >= TexturePacks.Count )
                {
                    packIdx = 0;
                }
            }

            if( !targetFound )
            {
                Redecorator.ModEntry.Logger.Log("Failed to find any target object " + ObjectName);
            }
        }
    }

    public class TexturePackGroup
    {
        public readonly string ObjectName;
        public readonly string PackName;
        public readonly Dictionary<string, Texture2D> TextureList = new Dictionary<string, Texture2D>();

        public TexturePackGroup( string objName )
        {
            ObjectName = objName;
        }

        public void LoadTexture( string path )
        {
            string texName = Path.GetFileNameWithoutExtension(path);

            byte[] imgData = File.ReadAllBytes(path);

            var tex = new Texture2D(2, 2); // dummy
            tex.LoadImage(imgData);
            tex.Apply(true, true);

            TextureList.Add(texName, tex);
        }

        private const string DIFFUSE_PROP = "_MainTex";
        private const string NORMAL_PROP = "_BumpMap";
        private const string SPECULAR_PROP = "_MetallicGlossMap";
        private const string EMMISSION_PROP = "_EmissionMap";

        private bool TryGetPrefixTexture( string prefix, string suffix, out Texture2D texture )
        {
            string texName = string.Concat(prefix, suffix);
            return TextureList.TryGetValue(texName, out texture);
        }

        public void AttemptRepaint( GameObject target )
        {
            var renderers = target.GetComponentsInChildren<MeshRenderer>();
            bool texChanged = false;

            foreach( var renderer in renderers )
            {
                Material[] materials = renderer.materials;

                for( int i = 0; i < materials.Length; i++ )
                {
                    //bool materialChanged = false;
                    string prefix;
                    Texture2D newTex;

                    // diffuse
                    if( Redecorator.GetMaterialTexture(materials[i], DIFFUSE_PROP) is Texture2D diffuse )
                    {
                        var prefix_match = Regex.Match(diffuse.name, @"(.+?)(?:_(?:\d+)?d)");
                        prefix = prefix_match.Groups[1].Value;

                        //Redecorator.ModEntry.Logger.Log($"Modifying material, prefix {prefix}");

                        if( TextureList.TryGetValue(diffuse.name, out newTex) )
                        {
                            materials[i].SetTexture(DIFFUSE_PROP, newTex);

                            texChanged = true;
                            //materialChanged = true;
                        }
                    }
                    else continue; // if there's no diffuse then this material isn't useful

                    // normal
                    if( ((Redecorator.GetMaterialTexture(materials[i], NORMAL_PROP) is Texture2D normal) &&
                        TextureList.TryGetValue(normal.name, out newTex)) ||
                        TryGetPrefixTexture(prefix, "_n", out newTex) )
                    {
                        materials[i].SetTexture(NORMAL_PROP, newTex);

                        texChanged = true;
                        //materialChanged = true;
                    }

                    // specular/AO
                    if( ((Redecorator.GetMaterialTexture(materials[i], SPECULAR_PROP) is Texture2D specular) &&
                        TextureList.TryGetValue(specular.name, out newTex)) ||
                        TryGetPrefixTexture(prefix, "_s", out newTex) )
                    {
                        materials[i].SetTexture(SPECULAR_PROP, newTex);

                        texChanged = true;
                        //materialChanged = true;

                        materials[i].SetTexture("_OcclusionMap", newTex);
                    }

                    // emission ignore bleh
                }
            }

            if( texChanged )
            {
                Redecorator.ModEntry.Logger.Log($"Applied new texture to {ObjectName}");
            }
            else
            {
                Redecorator.ModEntry.Logger.Log($"Found {ObjectName} but couldn't match any textures");
            }
        }
    }
}
