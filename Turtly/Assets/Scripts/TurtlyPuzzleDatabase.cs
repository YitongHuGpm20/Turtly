using System.Collections.Generic;
using UnityEngine;

namespace Turtly
{
    [CreateAssetMenu(
        fileName = "TurtlyPuzzleDatabase",
        menuName = "Turtly/Puzzle Database",
        order = 10)]
    public class TurtlyPuzzleDatabase : ScriptableObject
    {
        public List<TurtlyPuzzle> puzzles = new List<TurtlyPuzzle>();

        public int Count => puzzles.Count;

        public TurtlyPuzzle GetPuzzleByIndex(int index)
        {
            if (puzzles == null || puzzles.Count == 0)
                return null;

            if (index < 0) index = 0;
            if (index >= puzzles.Count) index = puzzles.Count - 1;
            return puzzles[index];
        }
    }
}