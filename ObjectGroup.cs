using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public void LoadTexturePackFiles( DirectoryInfo objDirectory, string packName )
        {
            var texGroup = new TexturePackGroup(ObjectName, packName);

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
}
