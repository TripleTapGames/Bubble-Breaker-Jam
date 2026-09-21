using System;
using System.Collections.Generic;
using UnityEngine;

namespace PopSort
{
    [Serializable]
    public class ColorConfig
    {
        public int id;
        public string displayName = "Color";

        public Sprite popHolder, popBalls, trayAsset, blockAsset, pressedAsset, trayCoverAsset;
    }

    [CreateAssetMenu(fileName = "ColorConfigPool", menuName = "PopSort/Color Config Pool")]
    public class ColorConfigPool : ScriptableObject
    {
        public List<ColorConfig> colors = new List<ColorConfig>();

        public bool TryGet(int id, out ColorConfig config)
        {
            if (colors != null)
            {
                foreach (ColorConfig candidate in colors)
                {
                    if (candidate != null && candidate.id == id)
                    {
                        config = candidate;
                        return true;
                    }
                }
            }

            config = null;
            return false;
        }

        public bool ContainsId(int id) => TryGet(id, out _);
    }
}
