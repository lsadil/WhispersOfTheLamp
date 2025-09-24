// Utils/ShuffleUtils.cs

using System;
using System.Collections.Generic;
using StardewValley;

namespace WhispersOfTheLamp.Utils
{
    public static class ShuffleUtils
    {
        public static void Shuffle<T>(IList<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = Game1.random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}