using System;
using UnityEngine;

namespace Turtly
{
    [Serializable]
    public class TurtlyPuzzle
    {
        public string id;
        public string title;

        [TextArea(3, 5)]
        public string opening;

        [TextArea(3, 10)]
        public string answer;

        [TextArea(1, 3)] 
        public string[] facts;

        [TextArea(1, 3)]
        public string[] hints;

        [Range(1, 5)]
        public int difficulty = 3;
    }
}