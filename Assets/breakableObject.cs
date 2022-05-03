using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using DigitalRuby.AdvancedPolygonCollider;
using UnityEngine;
using Random = UnityEngine.Random;
using csDelaunay;
using UnityEngine.Rendering;
using System.Threading;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;
using Color = UnityEngine.Color;
using Object = System.Object;

public class breakableObject : MonoBehaviour
{
    public Texture2D spriteTx;
    
    [HideInInspector] public float ppu;
    [HideInInspector] public int texWidth;
    [HideInInspector] public int texHeight;
    [HideInInspector] public SpriteRenderer sR;
    [SerializeField] private GameObject destroyedPrefab;
    private Rigidbody2D rb;

    [HideInInspector] public Color32[] pixelData;
    [HideInInspector] public int pixelDataTextureWidth;

    [SerializeField] private Texture2D debugTexture;
    [SerializeField] private MeshRenderer debugQuad;
    private PolygonDetector detector;
    
    //can this be private??
    private bool isOrig = true;

    [HideInInspector] public bool dirty;
    private bool doBreak;
    //[SerializeField] [Range(1, 16)]private int resolutionFactor;
    public float resolutionFactorDecimal;
    private int[] idArray;
    private Color[] debugPixels;
    private Thread shapeFinderThread;
    public int pixelCount;

    private bool canBreak;
    [Range(1, 100)]
    public int distanceThreshold;

    private float startTimeTotal;

    void Awake()
    {
    }
    // Start is called before the first frame update
    void Start()
    {
        detector = new PolygonDetector();
        sR = GetComponent<SpriteRenderer>();
        if (isOrig)
        {
            spriteTx = new Texture2D(sR.sprite.texture.width, sR.sprite.texture.height);
            spriteTx.SetPixels(sR.sprite.texture.GetPixels());
            spriteTx.Apply();
            spriteTx.filterMode = FilterMode.Point;
            sR.sprite = Sprite.Create(spriteTx, new Rect(0.0f, 0.0f, spriteTx.width, spriteTx.height), new Vector2(0.5f, 0.5f),
                sR.sprite.pixelsPerUnit);
            rb = GetComponent<Rigidbody2D>();
            pixelData = sR.sprite.texture.GetPixels32();
            ppu = GetComponent<SpriteRenderer>().sprite.pixelsPerUnit;
            texWidth = spriteTx.width;
            texHeight = spriteTx.height;
            pixelDataTextureWidth = texWidth;
            //approximation but it doesnt matter because the user is likely to want it to break on the first run.
            pixelCount = texWidth * texHeight;
            //DetectPolygons(false);
        }
        else
        {
            //TODO: do this in break object??
            pixelData = sR.sprite.texture.GetPixels32();
            spriteTx = sR.sprite.texture;
            spriteTx.SetPixels32(pixelData);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.U) && canBreak)
        {
            BreakObject(detector.result, detector.polygonCounts, detector.polygonStartIndices, detector.holeArray, detector.textureArray, detector.pixelCounts, detector.totalPixels);
        }
        if (detector.finished)
        {
            WhenFinished();
        }
        if (dirty)
        {
            DetectPolygons(true);
            dirty = false;
        }
    }

    private void FixedUpdate()
    {
    }

    public void DetectPolygons(bool breakObject)
    {
        doBreak = breakObject;
        startTimeTotal = Time.realtimeSinceStartup;
        detector.colors = pixelData;
        detector.width = texWidth;
        detector.alphaTolerance = 1;
        detector.resolutionFactor = resolutionFactorDecimal;
        detector.height = texHeight;
        detector.arrayWidth = pixelDataTextureWidth;
        ThreadPool.QueueUserWorkItem(detector.DetectPolygons);
    }

    void WhenFinished()
    {
        print("Time for thread: " + (Time.realtimeSinceStartup - startTimeTotal));
        detector.finished = false;
        debugPixels = detector.debugPixels;
        canBreak = true;
        if (debugQuad != null)
        {
            if (Application.isPlaying)
            {
                debugTexture = new Texture2D((int)(texWidth * resolutionFactorDecimal), (int)(texHeight * resolutionFactorDecimal));
                debugTexture.SetPixels(debugPixels);
                debugTexture.filterMode = FilterMode.Point;
                debugTexture.Apply();
                debugQuad.material.mainTexture = debugTexture;
            }
                    
        }
        //spriteTx.SetPixels32(detector.allColsDebug);
        //spriteTx.Apply();
        if (doBreak)
        {
            BreakObject(detector.result, detector.polygonCounts, detector.polygonStartIndices, detector.holeArray, detector.textureArray, detector.pixelCounts, detector.totalPixels);
        }
    }

    void BreakObject(List<Vector2> allVerts, List<int> polygonCounts, List<int> polygonStartIndices, int[] finalHoleResults, Color32[][] allColors, int[] pixelCountsResults, int totalPixels)
    {
        GameObject[] destroyedObjects = new GameObject[polygonCounts.Count];
        PolygonCollider2D[] colliderArray = new PolygonCollider2D[polygonCounts.Count];
        float mass = rb.mass;
        for (int i = 0; i < polygonCounts.Count; i++)
        {
            if (i < finalHoleResults.Length && finalHoleResults[i] == i)
            {
                Vertices verts = new Vertices();
                if (allVerts.Count > 0)
                {
                    for (int j = 0; j < polygonCounts[i]; j++)
                    {
                        if (polygonCounts[i] * (1/resolutionFactorDecimal) > 5)
                        {
                            if(allVerts.Count > polygonStartIndices[i] + j)
                                verts.Add(allVerts[polygonStartIndices[i] + j] * (1/resolutionFactorDecimal));
                        }
                    }
                }
                

                verts = SimplifyTools.DouglasPeuckerSimplify(verts, distanceThreshold);
                for (int j = 0; j < verts.Count; j++)
                {
                    float xValue = ((verts[j].x - (float)texWidth/2)/ppu);
                    float yValue = ((verts[j].y - (float)texHeight/2)/ppu);
                    verts[j] = new Vector2(xValue, yValue);
                }

                if (destroyedPrefab == null)
                {
                    return;
                }
                GameObject destroyedPart = GameObject.Instantiate(destroyedPrefab, transform.position, transform.rotation);
                destroyedPart.transform.localScale = transform.localScale;
                destroyedObjects[i] = destroyedPart;
                PolygonCollider2D poly = destroyedPart.GetComponent<PolygonCollider2D>();
                breakableObject breakableScript = destroyedPart.GetComponent<breakableObject>();
                colliderArray[i] = poly;
                Texture2D tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
                tex.SetPixels32(allColors[i]);
                tex.Apply();
                tex.filterMode = FilterMode.Point;
                if (verts.Count > 2)
                {
                    poly.pathCount = 1;
                    poly.SetPath(0, verts.ToArray());
                    breakableScript.isOrig = false;
                    //TODO: do txPixels and spriteTx set here? though i suppose it has to be done after the sprite.create?
                    breakableScript.ppu = ppu;
                    breakableScript.texWidth = texWidth;
                    breakableScript.texHeight = texHeight;
                    breakableScript.resolutionFactorDecimal = resolutionFactorDecimal;
                    breakableScript.pixelCount = pixelCountsResults[i];
                    breakableScript.distanceThreshold = distanceThreshold;
                }
                else
                {
                    breakableScript.enabled = false;
                    poly.enabled = false;
                    Destroy(destroyedPart, 3f);
                }
        
                if (totalPixels > 0)
                {
                    breakableScript.rb = destroyedPart.GetComponent<Rigidbody2D>();
                    breakableScript.rb.mass = mass * ((float)pixelCountsResults[i] / totalPixels);
                }
                
                //TODO: figure out how to make this sprite mesh generation quicker or eliminate it by passing in a mesh.
                destroyedPart.GetComponent<SpriteRenderer>().sprite = Sprite.Create(tex, new Rect(0.0f, 0.0f, texWidth, texHeight), new Vector2(0.5f, 0.5f),
                    sR.sprite.pixelsPerUnit);
            }
        }
        for (int i = 0; i < polygonCounts.Count; i++)
        {
            if (i < finalHoleResults.Length && finalHoleResults[i] != i)
            {
                Vertices verts = new Vertices();
                for (int j = 0; j < polygonCounts[i]; j++)
                {
                    if (polygonCounts[i] > 5)
                    {
                        if(allVerts.Count > polygonStartIndices[i] + j)
                            verts.Add(allVerts[polygonStartIndices[i] + j] * (1/resolutionFactorDecimal));
                    }
                }

                verts = SimplifyTools.DouglasPeuckerSimplify(verts, distanceThreshold);
                for (int j = 0; j < verts.Count; j++)
                {
                    float xValue = ((verts[j].x - (float)texWidth/2)/ppu);
                    float yValue = ((verts[j].y - (float)texHeight/2)/ppu);
                    verts[j] = new Vector2(xValue, yValue);
                }

                int holePoly = finalHoleResults[i];
                PolygonCollider2D poly = colliderArray[holePoly];
                if (poly != null)
                {
                    poly.pathCount += 1;
                    poly.SetPath(poly.pathCount - 1, verts.ToArray());
                }
                else
                {
                    print("null collider for hole");
                }
            }
        }
        Destroy(this.gameObject);
    }
}
