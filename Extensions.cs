using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVDecorator
{
    public static class Extensions
    {
        public static void Shuffle<T>( this IList<T> list )
        {
            var rand = new Random();

            for( int i = list.Count - 1; i > 1; i-- )
            {
                int j = rand.Next(i + 1);

                T tempVal = list[j];
                list[j] = list[i];
                list[i] = tempVal;
            }
        }
    }
}
