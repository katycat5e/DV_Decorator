using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DVDecorator
{
    public class TexturePackRoot
    {
        public readonly string PackName;
        public readonly HeirarchicalTextureSet TextureSet;

        public TexturePackRoot( string packName, HeirarchicalTextureSet textureSet )
        {
            PackName = packName;
            TextureSet = textureSet;
        }
    }

    public class ObjectGroup
    {
        public string ObjectName;
        public readonly List<TexturePackRoot> TexturePacks = new List<TexturePackRoot>();

        public ObjectGroup( string objName )
        {
            ObjectName = objName;
        }

        public void LoadTexturePackRoot( DirectoryInfo directory, string packName )
        {
            var texGroup = new HeirarchicalTextureSet(null);
            texGroup.LoadFolder(directory);
            TexturePacks.Add(new TexturePackRoot(packName, texGroup));
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

                TexturePacks[packIdx].TextureSet.AttemptRepaint(target);

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
}
