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
    public Texture2D idArrayTex;
    [HideInInspector]
    public float ppu;
    [HideInInspector]
    public int texWidth;

    [HideInInspector] public int texHeight;
    [HideInInspector]
    public SpriteRenderer sR;
    public GameObject prefab;
    private Rigidbody2D rb;

    public MeshRenderer quadToRenderVoronoi;
    [HideInInspector]
    public Color32[] txPixels;
    [HideInInspector] public int arrayWidth;

    [HideInInspector] public byte[] txBytes;
    public Texture2D debugTexture;
    public MeshRenderer debugQuad;
    private PolygonDetector detector;
    private ComputeShader colliderGenerator;
    private ComputeShader fractureShader;
    
    private ComputeBuffer vertBuffer;
    private ComputeBuffer polyCountBuffer;
    private ComputeBuffer polyStartIndices;
    private ComputeBuffer processedVertBuffer;
    private ComputeBuffer processedPolyCountBuffer;
    private ComputeBuffer usePointBuffer;
    
    
    private ComputeBuffer idBuffer;
    private ComputeBuffer resultDebugBuffer;
    private ComputeBuffer allPolyEntrances;
    private ComputeBuffer nonRepeatingEntranceIds;
    private ComputeBuffer nonRepeatingEntranceIdsCounts;
    private ComputeBuffer allPolyEntrancesXCoords;
    private ComputeBuffer polyHoleResults;
    private ComputeBuffer isOnlyOnePixel;
    private ComputeBuffer pixelCounts;

    private int kiHoleFinder;
    private int kiHoleFinderViaVertices;
    private int kiVoronoiFracture;
    private int kiGetPixelCountsAndTextures;
    public RenderTexture polygonTextures;
    private RenderTexture holeOutput;
    private RenderTexture pixelCountsOutput;
    public int[] pixelCountsResults;
    [HideInInspector]
    public bool isOrig = true;

    private Camera mainCam;
    [HideInInspector]
    public bool dirty;
    [HideInInspector]
    public bool doBreak;
    private Color[] colorsToAssignDirty;
    private float startTimeVoronoi;
    public bool controller = false;
    public float resolutionFactor;

    [HideInInspector]
    public bool holesFinalized;
    [HideInInspector]
    public bool countsFinalized;
    [HideInInspector]
    public bool colorsFinalized;

    [HideInInspector]
    public int[] holes;
    [HideInInspector]
    public List<Vector2> hullVerticesUnSimp;
    public List<Texture2D> allPixelData;
    private int[] idArray;
    private Color[] debugPixels;
    [HideInInspector]
    public List<int> polygonCounts, polygonStartIndices;
    [HideInInspector]
    public int totalPixels;
    private Thread shapeFinderThread;

    [HideInInspector]
    public bool readyForHoleFind;

    private float startTimeNewThread;
    public int pixelCount;

    private bool canBreak;
    [Range(1, 100)]
    public int distanceThreshold;

    void Awake()
    {
    }
    // Start is called before the first frame update
    void Start()
    {
        mainCam = Camera.main;
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
            txPixels = sR.sprite.texture.GetPixels32();
            colliderGenerator = Resources.Load<ComputeShader>("ColliderGenerator");
            fractureShader = Resources.Load<ComputeShader>("VoronoiFractureShader");
            kiHoleFinder = colliderGenerator.FindKernel("FindHoles");
            kiHoleFinderViaVertices = colliderGenerator.FindKernel("FindHolesViaVertices");
            kiGetPixelCountsAndTextures = colliderGenerator.FindKernel("GetPixelCountsAndTextures");
            kiVoronoiFracture = fractureShader.FindKernel("VoronoiFracture");
            ppu = GetComponent<SpriteRenderer>().sprite.pixelsPerUnit;
            texWidth = spriteTx.width;
            texHeight = spriteTx.height;
            arrayWidth = texWidth;
            //approximation but it doesnt matter because the user is likely to want it to break on the first run.
            pixelCount = texWidth * texHeight;
            //DetectPolygons(false);
        }
        else
        {
            txPixels = sR.sprite.texture.GetPixels32();
            //print("len + " + txPixels.Length);
            spriteTx = sR.sprite.texture;
            spriteTx.SetPixels32(txPixels);
            readyForHoleFind = false;
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
        //DetectPolygons(false);
        if (dirty)
        {
            //spriteTx.SetPixels(txPixels);
            //spriteTx.Apply();
            DetectPolygons(true);
            //txPixels = spriteTx.GetPixels();
            dirty = false;
        }
    }

    private void FixedUpdate()
    {
    }

    public void DetectPolygons(bool breakObject)
    {
        readyForHoleFind = false;
        doBreak = breakObject;
        totalPixels = 0;
        double startTimeTotal = Time.realtimeSinceStartup;
        detector.colors = txPixels;
        detector.width = texWidth;
        detector.alphaTolerance = 1;
        detector.resolutionFactor = resolutionFactor;
        detector.height = texHeight;
        detector.arrayWidth = arrayWidth;
        startTimeNewThread = Time.realtimeSinceStartup;
       ThreadPool.QueueUserWorkItem(detector.DetectPolygons);
    }

    void WhenFinished()
    {
        detector.finished = false;
        debugPixels = detector.pixelsDetected;
        canBreak = true;
        holes = detector.holeArray;
        pixelCountsResults = detector.pixelCounts;
        if (debugQuad != null)
        {
            if (Application.isPlaying)
            {
                debugTexture = new Texture2D((int)(texWidth * resolutionFactor), (int)(texHeight * resolutionFactor));
                debugTexture.SetPixels(debugPixels);
                debugTexture.filterMode = FilterMode.Point;
                debugTexture.Apply();
                debugQuad.material.mainTexture = debugTexture;
            }
                    
        }
        spriteTx.SetPixels32(detector.allColsDebug);
        spriteTx.Apply();
        //BreakObject(detector.result, detector.polygonCounts, detector.polygonStartIndices, detector.holeArray, detector.textureArray, detector.pixelCounts, detector.totalPixels);
    }

    void DoCollidersAndFilling()
    {
        //polygonTextures = new Texture2DArray(texWidth, texHeight, polygonCounts.Count, TextureFormat.ARGB32, false);
        if (hullVerticesUnSimp.Count <= 0)
        {
            return;
        }
        vertBuffer = new ComputeBuffer(hullVerticesUnSimp.Count, 4 * 2);
        polyCountBuffer = new ComputeBuffer(polygonCounts.Count, 4 * 1);
        polyStartIndices = new ComputeBuffer(polygonStartIndices.Count, 4 * 1);
        processedVertBuffer = new ComputeBuffer(hullVerticesUnSimp.Count, 4 * 2);
        processedPolyCountBuffer = new ComputeBuffer(polygonCounts.Count, 4 * 1);
        usePointBuffer = new ComputeBuffer(hullVerticesUnSimp.Count, 4 * 1);
        idBuffer = new ComputeBuffer(idArray.Length, 4 * 1);
        resultDebugBuffer = new ComputeBuffer(idArray.Length, 4 * 1);
        allPolyEntrances = new ComputeBuffer(idArray.Length, 4 * 1);
        nonRepeatingEntranceIds = new ComputeBuffer(polygonCounts.Count * polygonCounts.Count, 4 * 1);
        nonRepeatingEntranceIdsCounts = new ComputeBuffer(polygonCounts.Count * polygonCounts.Count, 4 * 1);
        allPolyEntrancesXCoords = new ComputeBuffer(idArray.Length, 4 * 1);
        polyHoleResults = new ComputeBuffer(polygonCounts.Count, 4 * 1);
        isOnlyOnePixel = new ComputeBuffer(idArray.Length, 4 * 1);
        pixelCounts = new ComputeBuffer(polygonCounts.Count, 4 * 1);
        polygonTextures = new RenderTexture(texWidth, texHeight, 0, RenderTextureFormat.ARGB32);
        polygonTextures.enableRandomWrite = true;
        polygonTextures.filterMode = FilterMode.Point;
        polygonTextures.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        polygonTextures.volumeDepth = polygonCounts.Count;
        polygonTextures.Create();
        holeOutput = new RenderTexture(polygonCounts.Count, 1, 0, RenderTextureFormat.R16);
        holeOutput.enableRandomWrite = true;
        holeOutput.filterMode = FilterMode.Point;
        holeOutput.dimension = TextureDimension.Tex2D;
        holeOutput.volumeDepth = 1;
        holeOutput.Create();
        pixelCountsOutput = new RenderTexture(polygonCounts.Count, 1, 0, RenderTextureFormat.RG32);
        pixelCountsOutput.enableRandomWrite = true;
        pixelCountsOutput.filterMode = FilterMode.Point;
        pixelCountsOutput.dimension = TextureDimension.Tex2D;
        pixelCountsOutput.volumeDepth = 1;
        pixelCountsOutput.Create();
        idBuffer.SetData(idArray);
        pixelCounts.SetData(new int[polygonCounts.Count]);
        isOnlyOnePixel.SetData(new int[idArray.Length]);
        polyHoleResults.SetData(new int[polygonCounts.Count]);
        resultDebugBuffer.SetData(new int[idArray.Length]);
        allPolyEntrances.SetData(new int[idArray.Length]);
        //nonRepeatingEntranceIds.SetData(new int[idArray.Length]);
        //nonRepeatingEntranceIdsCounts.SetData(new int[idArray.Length]);
        int len = polygonCounts.Count * polygonCounts.Count;
        nonRepeatingEntranceIds.SetData(new int[len]);
        nonRepeatingEntranceIdsCounts.SetData(new int[len]);
        allPolyEntrancesXCoords.SetData(new int[idArray.Length]);
        vertBuffer.SetData(hullVerticesUnSimp);
        polyCountBuffer.SetData(polygonCounts);
        polyStartIndices.SetData(polygonStartIndices);
        processedVertBuffer.SetData(new Vector2[hullVerticesUnSimp.Count]);
        processedPolyCountBuffer.SetData(new int[polygonCounts.Count]);
        usePointBuffer.SetData(new int[hullVerticesUnSimp.Count]);
        colliderGenerator.SetInt("vertCount", hullVerticesUnSimp.Count);
        colliderGenerator.SetInt("totalPolygons", polygonCounts.Count);
        colliderGenerator.SetInt("texWidth", texWidth);
        colliderGenerator.SetInt("texHeight", texHeight);
        colliderGenerator.SetFloat("resolutionFactor", resolutionFactor);
        /*colliderGenerator.SetBuffer(kiHoleFinder, "idArray", idBuffer);
        colliderGenerator.SetBuffer(kiHoleFinder, "resultDebug", resultDebugBuffer);
        colliderGenerator.SetBuffer(kiHoleFinder, "allPolyEntrances", allPolyEntrances);
        colliderGenerator.SetBuffer(kiHoleFinder, "nonRepeatingEntranceIds", nonRepeatingEntranceIds);
        colliderGenerator.SetBuffer(kiHoleFinder, "nonRepeatingEntranceIdsCounts", nonRepeatingEntranceIdsCounts);
        colliderGenerator.SetBuffer(kiHoleFinder, "allPolyEntrancesXCoords", allPolyEntrancesXCoords);
        colliderGenerator.SetBuffer(kiHoleFinder, "polyHoleResults", polyHoleResults);
        colliderGenerator.SetBuffer(kiHoleFinder, "isOnlyOnePixel", isOnlyOnePixel);
        colliderGenerator.SetBuffer(kiHoleFinder, "pixelCounts", pixelCounts);
        colliderGenerator.SetTexture(kiHoleFinder, "allTextures", polygonTextures);
        colliderGenerator.SetBuffer(kiHoleFinder, "origTex", pixelBuffer);*/
        colliderGenerator.SetBuffer(kiHoleFinderViaVertices, "idArray", idBuffer);
        colliderGenerator.SetBuffer(kiHoleFinderViaVertices, "resultDebug", resultDebugBuffer);
        colliderGenerator.SetBuffer(kiHoleFinderViaVertices, "nonRepeatingEntranceIds", nonRepeatingEntranceIds);
        colliderGenerator.SetBuffer(kiHoleFinderViaVertices, "nonRepeatingEntranceIdsCounts", nonRepeatingEntranceIdsCounts);
        colliderGenerator.SetBuffer(kiHoleFinderViaVertices, "polyHoleResults", polyHoleResults);
        colliderGenerator.SetBuffer(kiHoleFinderViaVertices, "polyStartIndices", polyStartIndices);
        colliderGenerator.SetBuffer(kiHoleFinderViaVertices, "allVerts", vertBuffer);
        colliderGenerator.SetTexture(kiHoleFinderViaVertices, "testOutputTex", holeOutput);
        colliderGenerator.SetTexture(kiGetPixelCountsAndTextures, "pixelCountsOutput", pixelCountsOutput);
        colliderGenerator.SetBuffer(kiGetPixelCountsAndTextures, "idArray", idBuffer);
        colliderGenerator.SetBuffer(kiGetPixelCountsAndTextures, "pixelCounts", pixelCounts);
        colliderGenerator.SetBuffer(kiGetPixelCountsAndTextures, "resultDebug", resultDebugBuffer);
        colliderGenerator.SetBuffer(kiGetPixelCountsAndTextures, "polyHoleResults", polyHoleResults);
        colliderGenerator.SetTexture(kiGetPixelCountsAndTextures, "allTextures", polygonTextures);
        colliderGenerator.SetTexture(kiGetPixelCountsAndTextures, "origTex", spriteTx);
        //print("Overhead: " + (Time.realtimeSinceStartup - start));
        //colliderGenerator.Dispatch(kiSimplifyCollider, Mathf.CeilToInt(polygonCounts.Count/8f), 1, 1);
        //colliderGenerator.Dispatch(kiHoleFinder, Mathf.CeilToInt((texHeight * resolutionFactor) / 64f), 1, 1);
        colliderGenerator.Dispatch(kiHoleFinderViaVertices, Mathf.CeilToInt(polygonCounts.Count/8f), 1, 1);
        colliderGenerator.Dispatch(kiGetPixelCountsAndTextures, Mathf.CeilToInt((texHeight * resolutionFactor)/32f), 1, 1);
       // debugPixels = new Color[debugPixels.Length];
        //int[] debugResults = new int[idArray.Length];
        holes = new int[polygonCounts.Count];
        //pixelCounts.GetData(pixelCountsResults);
        //resultDebugBuffer.GetData(debugResults);
        //polyHoleResults.GetData(finalHoleResults);
        //print("Time getting data: " + (Time.realtimeSinceStartup - timeForgettingData) + " totalBreakable: " + polygonCounts.Count);
        //print("Time for reading data: " + (Time.realtimeSinceStartup - startTimeCompute));
       /* for (int i = 0; i < debugResults.Length; i++)
        {
            if (debugResults[i] == 1)
            {
                debugPixels[i] = Color.red;
            }
            else if (debugResults[i] == 2)
            {
                debugPixels[i] = Color.yellow;
            }
            else if (debugResults[i] == 3)
            {
                debugPixels[i] = Color.green;
            }
            else if (debugResults[i] == 4)
            {
                debugPixels[i] = Color.blue;
            }
            else if (debugResults[i] == 5)
            {
                debugPixels[i] = Color.magenta;
            }
            else if (debugResults[i] == 6)
            {
                debugPixels[i] = Color.cyan;
            }
            else if (debugResults[i] == 7)
            {
                debugPixels[i] = Color.white;
            }
            else if (debugResults[i] == 8)
            {
                debugPixels[i] = Color.black;
            }
            else if (debugResults[i] == 9)
            {
                debugPixels[i] = Color.gray;
            }
        }*/
        AsyncGPUReadback.Request(holeOutput, 0, 0, polygonCounts.Count, 0, 1, 0, holeOutput.volumeDepth,
            new Action<AsyncGPUReadbackRequest>
            (
                (AsyncGPUReadbackRequest request) =>
                {
                    if (!request.hasError)
                    {
                        Int16[] data = request.GetData<Int16>().ToArray();
                        for (int i = 0; i < data.Length; i++)
                        {
                            int x = (int) data[i];
                            if (x < 0)
                            {
                                x += 65536;
                            }

                            if (i < holes.Length)
                            {
                                holes[i] = x - 1;
                            }
                        }

                        holesFinalized = true;
                        holeOutput.Release();
                    }
                }
            ));
        AsyncGPUReadback.Request(pixelCountsOutput, 0, 0, polygonCounts.Count, 0, 1, 0, pixelCountsOutput.volumeDepth,
            new Action<AsyncGPUReadbackRequest>
            (
                (AsyncGPUReadbackRequest requestPixelCounts) =>
                {
                    if (!requestPixelCounts.hasError)
                    {
                        Int32[] dataPixelCounts = requestPixelCounts.GetData<Int32>().ToArray();
                        for (int j = 0; j < dataPixelCounts.Length; j++)
                        {
                            if (j < pixelCountsResults.Length)
                            {
                                pixelCountsResults[j] = (int) dataPixelCounts[j];
                                totalPixels += dataPixelCounts[j];
                            }
                        }

                        countsFinalized = true;
                        pixelCountsOutput.Release();
                    }
                }
            ));
        //allPixelData = new List<Color32[]>();

        AsyncGPUReadback.Request(polygonTextures, 0, 0, texWidth, 0, texHeight, 0, polygonTextures.volumeDepth,
            new Action<AsyncGPUReadbackRequest>
            (
                (AsyncGPUReadbackRequest request) =>
                {
                    if (!request.hasError)
                    {
                        // Copy the data
                        for (var i = 0; i < request.layerCount; i++)
                        {
                            //allPixelData.Add(request.GetData<Color32>(i).ToArray());
                        }

                        if (doBreak)
                        {
                            colorsFinalized = true;
                        }
                        polygonTextures.Release();
                    }
                }
            ));

        processedVertBuffer.Release();
        processedPolyCountBuffer.Release();
        vertBuffer.Release();
        polyCountBuffer.Release();
        polyStartIndices.Release();
        usePointBuffer.Release();
        idBuffer.Release();
        allPolyEntrances.Release();
        allPolyEntrancesXCoords.Release();
        nonRepeatingEntranceIds.Release();
        nonRepeatingEntranceIdsCounts.Release();
        isOnlyOnePixel.Release();
        resultDebugBuffer.Release();
        polyHoleResults.Release();
        pixelCounts.Release();
        //print("Time for post processing data: " + (Time.realtimeSinceStartup - startTimePostProcess));
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
                        if (polygonCounts[i] * (1/resolutionFactor) > 5)
                        {
                            if(allVerts.Count > polygonStartIndices[i] + j)
                                verts.Add(allVerts[polygonStartIndices[i] + j] * (1/resolutionFactor));
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

                if (prefab == null)
                {
                    return;
                }
                GameObject destroyedPart = GameObject.Instantiate(prefab, transform.position, transform.rotation);
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
                    //breakableScript.txPixels = allColors[i];
                    breakableScript.colliderGenerator = colliderGenerator;
                    breakableScript.fractureShader = fractureShader;
                    breakableScript.kiHoleFinder = kiHoleFinder;
                    breakableScript.kiVoronoiFracture = kiVoronoiFracture;
                    breakableScript.ppu = ppu;
                    breakableScript.texWidth = texWidth;
                    breakableScript.texHeight = texHeight;
                    breakableScript.resolutionFactor = resolutionFactor;
                    breakableScript.kiHoleFinderViaVertices = kiHoleFinderViaVertices;
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
                            verts.Add(allVerts[polygonStartIndices[i] + j] * (1/resolutionFactor));
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

    public Color32[] MergeTextures (Color32[] cols1, Color32[] cols2)
    {
        for(var i = 0; i < cols1.Length; ++i)
        {
            if (cols1[i].a == 0)
            {
                cols1[i] = cols2[i];
            }
        }

        return cols1;
    }
}
