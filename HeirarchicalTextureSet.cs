using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DVDecorator
{
    public class HeirarchicalTextureSet
    {
        public readonly string ObjectName;

        private Dictionary<string, Texture2D> textureList = null;
        public Dictionary<string, Texture2D> TextureList
        {
            get
            {
                if( textureList == null ) textureList = new Dictionary<string, Texture2D>();
                return textureList;
            }
        }

        public bool HasTextures => (textureList != null) && (textureList.Count > 0);

        private List<HeirarchicalTextureSet> childSets = null;
        public List<HeirarchicalTextureSet> ChildSets
        {
            get
            {
                if( childSets == null ) childSets = new List<HeirarchicalTextureSet>();
                return childSets;
            }
        }

        public bool HasChildSets => (childSets != null) && (childSets.Count > 0);

        public HeirarchicalTextureSet( string objName )
        {
            ObjectName = objName;
        }

        public HeirarchicalTextureSet( string objName, HeirarchicalTextureSet other )
        {
            ObjectName = objName;
            if( other.textureList != null ) textureList = new Dictionary<string, Texture2D>(other.textureList);
            if( other.childSets != null ) childSets = new List<HeirarchicalTextureSet>(other.childSets);
        }

        public void LoadFolder( DirectoryInfo objectDir )
        {
            // load textures
            foreach( var textureFile in objectDir.GetFiles("*.png") )
            {
                string texName = Path.GetFileNameWithoutExtension(textureFile.FullName);

                byte[] imgData = File.ReadAllBytes(textureFile.FullName);

                var tex = new Texture2D(2, 2); // dummy
                tex.LoadImage(imgData);
                tex.Apply(true, true);

                TextureList.Add(texName, tex);
            }

            // load textures for child objects
            foreach( var subDir in objectDir.GetDirectories() )
            {
                var subSet = new HeirarchicalTextureSet(subDir.Name);
                subSet.LoadFolder(subDir);
                ChildSets.Add(subSet);
            }
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

        public void AttemptRepaint( GameObject rootObject )
        {
            // if this is the root (no obj name) then just apply textures to the passed in root
            GameObject target;
            if( string.IsNullOrEmpty(ObjectName) )
            {
                target = rootObject;
            }
            else
            {
                target = rootObject.FindChildObject(ObjectName);
            }

            if( this.HasTextures )
            {
                // if there are subsets, then only look at meshes in the current level
                IEnumerable<MeshRenderer> renderers;
                if( HasChildSets )
                {
                    renderers = target.GetComponents<MeshRenderer>();
                }
                else
                {
                    renderers = target.GetComponentsInChildren<MeshRenderer>();
                }

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
                    Redecorator.ModEntry.Logger.Log($"Applied new texture to {target.name}");
                }
                else
                {
                    Redecorator.ModEntry.Logger.Log($"Found {target.name} but couldn't match any textures");
                }
            }
            // end if( HasTextures )

            // Apply textures of children as well
            if( this.HasChildSets )
            {
                foreach( HeirarchicalTextureSet child in ChildSets )
                {
                    child.AttemptRepaint(target);
                }
            }
        }
    }
}
