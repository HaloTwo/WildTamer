using System.Collections.Generic;
using UnityEngine;

public class SpritePool : Singleton<SpritePool>
{
    private readonly Dictionary<string, Sprite> cache = new();

    public Sprite Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        if (cache.TryGetValue(name, out Sprite sprite)) return sprite;

        sprite = Resources.Load<Sprite>($"Sprite/{name}");

        if (sprite == null)
        {
            Debug.LogError($"[SpritePool] Sprite/{name} ¸ø Ã£À½");
            return null;
        }

        cache.Add(name, sprite);
        return sprite;
    }
}