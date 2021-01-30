using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DVDecorator
{
    public static class Extensions
    {
        public static void Shuffle<T>( this IList<T> list )
        {
            var rand = new System.Random();

            for( int i = list.Count - 1; i > 1; i-- )
            {
                int j = rand.Next(i + 1);

                T tempVal = list[j];
                list[j] = list[i];
                list[i] = tempVal;
            }
        }

        public static GameObject FindChildObject( this GameObject rootObject, string name )
        {
            return FindChildInTransform(rootObject.transform, name);
        }

        private static GameObject FindChildInTransform( Transform rootTform, string name )
        {
            // breadth-first search
            foreach( Transform child in rootTform )
            {
                if( name.Equals(child.gameObject.name) ) return child.gameObject;
            }

            // search next level
            foreach( Transform child in rootTform )
            {
                if( FindChildInTransform(child, name) is GameObject result ) return result;
            }

            return null;
        }
    }
}
