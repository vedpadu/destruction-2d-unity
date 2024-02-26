using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mouseScript : MonoBehaviour
{
    public Texture2DArray debug;
    public Texture2D debug2;

    public Texture2D[] fractureImages = new Texture2D[1];

    public float imageScale;
    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0; i < fractureImages.Length; i++)
        {
            if (fractureImages[i].format != TextureFormat.ARGB32)
            {
                Texture2D newTex = new Texture2D(fractureImages[i].width, fractureImages[i].height, TextureFormat.ARGB32, false);
                //Copy old texture pixels into new one
                newTex.SetPixels(fractureImages[0].GetPixels());
                //Apply
                newTex.Apply();
                fractureImages[i] = newTex;
            }
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            double start = Time.realtimeSinceStartup;
            var pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            //ObjectFracturer.DoCircularBreak((Vector2)pos, 0.5f, 0.5f);
            ObjectFracturer.DoVoronoiBreak((Vector2) pos , 10.0f, 5.0f, 0.5f, Random.Range(900,1000), 1);
            //ObjectFracturer.DoVoronoiBreak((Vector2) pos , 2f, 1f, 1f, Random.Range(10,20), 1);
            debug = ObjectFracturer.debugTexArray;
        }else if (Input.GetMouseButtonDown(0))
        {
            double start = Time.realtimeSinceStartup;
            var pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            //ObjectFracturer.DoCircularBreak((Vector2)pos, 0.5f, 0.5f);
            ObjectFracturer.DoImageBreakage(pos,  fractureImages, imageScale);
            //ObjectFracturer.DoVoronoiBreak((Vector2) pos , 0.5f, 0.25f, 2f, Random.Range(10,20), 1);
            debug = ObjectFracturer.debugTexArray;
        }else if (Input.GetMouseButtonDown(2))
        {
           /* Collider2D obj = Physics2D.OverlapCircle(Camera.main.ScreenToWorldPoint(Input.mousePosition),2f);
            if (obj != null)
            {
                //ObjectFracturer.AntiAliasTest(obj);
                debug2 = ObjectFracturer.debugTex;
            }*/
        }
    }
}
