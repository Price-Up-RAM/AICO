using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomFoodSpriteSelector : MonoBehaviour
{
    public List<Sprite> foodSprites; // Assign in Inspector
    public SpriteRenderer spriteRenderer; // Assign in Inspector
    // Start is called before the first frame update
    void Start()
    {
        if (foodSprites != null && foodSprites.Count > 0 && spriteRenderer != null)
        {
            int randomIndex = Random.Range(0, foodSprites.Count);
            spriteRenderer.sprite = foodSprites[randomIndex];
        }
    }
}