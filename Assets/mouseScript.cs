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
            ObjectFracturer.DoVoronoiBreak((Vector2) pos , 2f, 1f, 1f, Random.Range(10,20), 1);
            debug = ObjectFracturer.debugTexArray;
        }else if (Input.GetMouseButtonDown(0))
        {
            double start = Time.realtimeSinceStartup;
            var pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            ObjectFracturer.DoVoronoiBreak((Vector2) pos , 0.5f, 0.25f, 2f, Random.Range(10,20), 1);
            debug = ObjectFracturer.debugTexArray;
        }
    }
}
