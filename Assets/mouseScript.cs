using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mouseScript : MonoBehaviour
{
    public Texture2DArray debug;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            double start = Time.realtimeSinceStartup;
            var pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            ObjectFracturer.DoVoronoiBreak((Vector2) pos , 3f, 0.5f, 5f, Random.Range(5,10), 1);
            print("voronoi time" + ": " + (Time.realtimeSinceStartup - start));
            debug = ObjectFracturer.debugTexArray;
        }
    }
}
