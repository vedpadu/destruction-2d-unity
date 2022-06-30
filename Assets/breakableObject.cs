using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using DigitalRuby.AdvancedPolygonCollider;
using UnityEngine;
using Random = UnityEngine.Random;
using csDelaunay;
using UnityEngine.Rendering;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;
using Color = UnityEngine.Color;
using Object = System.Object;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class breakableObject : MonoBehaviour
{
    public Texture2D spriteTx;

    public bool isKinematic;
    [Tooltip("Updates this object on destruction to avoid one instantiate call. Only possible if the new destroyed prefab is the same as " +
             "this object, as this object will preserve all its properties other than its collider and texture.")]
    public bool preserveThisObject;
    
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
    
    private bool isOrig = true;

    [HideInInspector] public bool dirty;
    private bool doBreak;
    //[SerializeField] [Range(1, 16)]private int resolutionFactor;
    public float resolutionFactorDecimal;
    private Color[] debugPixels;
    private Thread shapeFinderThread;
    public int pixelCount;

    private bool canBreak;
    [Range(1, 100)]
    public int distanceThreshold;

    private float startTimeTotal;

    private ComputeShader colliderGenerator;
    private int kiHoleFinderViaVertices;
    private int kiGetPixelCountsAndTextures;
    
    [HideInInspector]
    public bool holesFinalized;
    [HideInInspector]
    public bool countsFinalized;
    [HideInInspector]
    public bool colorsFinalized;

    private int[] holes;
    private int[] pixelCountsResults;
    private int totalPixels;
    private Color32[][] allPixelData;
    [SerializeField]private Texture2D[] allTextures;
    private bool isWorking = false;

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
            colliderGenerator = Resources.Load<ComputeShader>("ColliderGenerator");
            kiHoleFinderViaVertices = colliderGenerator.FindKernel("FindHolesViaVertices");
            kiGetPixelCountsAndTextures = colliderGenerator.FindKernel("GetPixelCountsAndTextures");
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
            //spriteTx.SetPixels32(pixelData);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.U) && canBreak)
        {
            BreakObject(detector.allPolygons, detector.holeArray, detector.pixelCounts, detector.totalPixels, detector.shapeExtremeties);
        }
        if (detector.finished)
        {
            WhenFinished();
        }
        if (dirty && !isWorking)
        {
            DetectPolygons(true);
            dirty = false;
        }
    }

    private void FixedUpdate()
    {
        if (doBreak && colorsFinalized && holesFinalized && countsFinalized)
        {
            BreakObject(detector.allPolygons, holes, pixelCountsResults, totalPixels, detector.shapeExtremeties);
            isWorking = false;
        }
    }

    public void DetectPolygons(bool breakObject)
    {
        isWorking = true;
        doBreak = breakObject;
        colorsFinalized = false;
        countsFinalized = false;
        holesFinalized = false;
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
        if (!isKinematic)
        {
            DoCollidersAndFilling(detector.result, detector.polygonCounts, detector.polygonStartIndices, detector.idArray);
        }
        if (doBreak)
        {
           // BreakObject(detector.result, detector.polygonCounts, detector.polygonStartIndices, detector.holeArray, detector.textureArray, detector.pixelCounts, detector.totalPixels);
        }
    }
    
    void DoCollidersAndFilling(List<Vector2> hullVerticesUnSimp, List<int> polygonCounts, List<int> polygonStartIndices, int[] idArray)
    {
        //polygonTextures = new Texture2DArray(texWidth, texHeight, polygonCounts.Count, TextureFormat.ARGB32, false);
        if (hullVerticesUnSimp.Count <= 0)
        {
            return;
        }
        ComputeBuffer vertBuffer = new ComputeBuffer(hullVerticesUnSimp.Count, 4 * 2);
        ComputeBuffer polyCountBuffer = new ComputeBuffer(polygonCounts.Count, 4 * 1);
        ComputeBuffer polyStartIndices = new ComputeBuffer(polygonStartIndices.Count, 4 * 1);
        ComputeBuffer idBuffer = new ComputeBuffer(idArray.Length, 4 * 1);
        ComputeBuffer resultDebugBuffer = new ComputeBuffer(idArray.Length, 4 * 1);
        ComputeBuffer nonRepeatingEntranceIds = new ComputeBuffer(polygonCounts.Count * polygonCounts.Count, 4 * 1);
        ComputeBuffer nonRepeatingEntranceIdsCounts = new ComputeBuffer(polygonCounts.Count * polygonCounts.Count, 4 * 1);
        ComputeBuffer polyHoleResults = new ComputeBuffer(polygonCounts.Count, 4 * 1);
        ComputeBuffer pixelCounts = new ComputeBuffer(polygonCounts.Count, 4 * 1);
        RenderTexture polygonTextures = new RenderTexture(texWidth, texHeight, 0, RenderTextureFormat.ARGB32);
        polygonTextures.enableRandomWrite = true;
        polygonTextures.filterMode = FilterMode.Point;
        polygonTextures.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        polygonTextures.volumeDepth = polygonCounts.Count;
        polygonTextures.Create();
        RenderTexture holeOutput = new RenderTexture(polygonCounts.Count, 1, 0, RenderTextureFormat.R16);
        holeOutput.enableRandomWrite = true;
        holeOutput.filterMode = FilterMode.Point;
        holeOutput.dimension = TextureDimension.Tex2D;
        holeOutput.volumeDepth = 1;
        holeOutput.Create();
        RenderTexture pixelCountsOutput = new RenderTexture(polygonCounts.Count, 1, 0, RenderTextureFormat.RG32);
        pixelCountsOutput.enableRandomWrite = true;
        pixelCountsOutput.filterMode = FilterMode.Point;
        pixelCountsOutput.dimension = TextureDimension.Tex2D;
        pixelCountsOutput.volumeDepth = 1;
        pixelCountsOutput.Create();
        idBuffer.SetData(idArray);
        pixelCounts.SetData(new int[polygonCounts.Count]);
        polyHoleResults.SetData(new int[polygonCounts.Count]);
        resultDebugBuffer.SetData(new int[idArray.Length]);
        int len = polygonCounts.Count * polygonCounts.Count;
        nonRepeatingEntranceIds.SetData(new int[len]);
        nonRepeatingEntranceIdsCounts.SetData(new int[len]);
        vertBuffer.SetData(hullVerticesUnSimp);
        polyCountBuffer.SetData(polygonCounts);
        polyStartIndices.SetData(polygonStartIndices);
        colliderGenerator.SetInt("vertCount", hullVerticesUnSimp.Count);
        colliderGenerator.SetInt("totalPolygons", polygonCounts.Count);
        colliderGenerator.SetInt("texWidth", texWidth);
        colliderGenerator.SetInt("texHeight", texHeight);
        colliderGenerator.SetFloat("resolutionFactor", resolutionFactorDecimal);
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
        colliderGenerator.Dispatch(kiGetPixelCountsAndTextures, Mathf.CeilToInt((texHeight * resolutionFactorDecimal)/32f), 1, 1);
        // debugPixels = new Color[debugPixels.Length];
        //int[] debugResults = new int[idArray.Length];
        holes = new int[polygonCounts.Count];
        //pixelCounts.GetData(pixelCountsResults);
        //resultDebugBuffer.GetData(debugResults);
        //polyHoleResults.GetData(finalHoleResults);
        //print("Time getting data: " + (Time.realtimeSinceStartup - timeForgettingData) + " totalBreakable: " + polygonCounts.Count);
        //print("Time for reading data: " + (Time.realtimeSinceStartup - startTimeCompute));
        /*for (int i = 0; i < debugResults.Length; i++)
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
        pixelCountsResults = new int[polygonCounts.Count];
        totalPixels = 0;
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
        //allPixelData = new Color32[polygonTextures.volumeDepth][];
        allTextures = new Texture2D[polygonTextures.volumeDepth];
        AsyncGPUReadback.Request(polygonTextures, 0, 0, texWidth, 0, texHeight, 0, polygonTextures.volumeDepth,
            new Action<AsyncGPUReadbackRequest>
            (
                (AsyncGPUReadbackRequest request) =>
                {
                    if (!request.hasError)
                    {
                        float s = Time.realtimeSinceStartup;
                        // Copy the data
                        for (var i = 0; i < request.layerCount; i++)
                        {
                            if (i < detector.shapeExtremeties.Count)
                            {
                                int[] extremities = detector.shapeExtremeties[i];
                                extremities[1] += 1;
                                extremities[3] += 1;
                                // allPixelData[i] = request.GetData<Color32>(i).ToArray();
                                int wid = (extremities[1] - extremities[0]) * (int) (1 / resolutionFactorDecimal);
                                int hei = (extremities[3] - extremities[2]) * (int) (1 / resolutionFactorDecimal);
                                if (extremities[1] * (1/resolutionFactorDecimal) > polygonTextures.width)
                                {
                                    wid = polygonTextures.width - (extremities[0] * (int) (1 / resolutionFactorDecimal));
                                }
                                if (extremities[3] * (1/resolutionFactorDecimal) > polygonTextures.height)
                                {
                                    hei = polygonTextures.height - (extremities[2] * (int) (1 / resolutionFactorDecimal));
                                }
                                allTextures[i] = new Texture2D(wid, hei, TextureFormat.ARGB32, false);
                                allTextures[i].filterMode = FilterMode.Point;
                                Graphics.CopyTexture(polygonTextures, i, 0, extremities[0] * (int)(1/resolutionFactorDecimal), extremities[2] * (int)(1/resolutionFactorDecimal), allTextures[i].width, allTextures[i].height, allTextures[i], 0, 0, 0, 0);
                            }
                        }
                        
                        if (doBreak)
                        {
                            colorsFinalized = true;
                        }
                        polygonTextures.Release();
                    }
                }
            ));
        vertBuffer.Release();
        polyCountBuffer.Release();
        polyStartIndices.Release();
        idBuffer.Release();
        nonRepeatingEntranceIds.Release();
        nonRepeatingEntranceIdsCounts.Release();
        resultDebugBuffer.Release();
        polyHoleResults.Release();
        pixelCounts.Release();
        //print("Time for post processing data: " + (Time.realtimeSinceStartup - startTimePostProcess));
    }

    void UpdateCollidersKinematic()
    {
        GetComponent<PolygonCollider2D>().pathCount = 0;
    }

    void BreakObject(List<Vertices> allVerts, int[] finalHoleResults, int[] pixelCounts, int totalPixels, List<int[]> extremities)
    {
        PolygonCollider2D[] colliderArray = new PolygonCollider2D[allVerts.Count];
        PolygonCollider2D col = GetComponent<PolygonCollider2D>();
        col.pathCount = 0;
        float mass = rb.mass;
        bool firstObject = preserveThisObject;
        Vector3 origPos = transform.position;
        int origWidth = texWidth;
        int origHeight = texHeight;
        for (int i = 0; i < allVerts.Count; i++)
        {
            if (firstObject)
            {
                if (i < finalHoleResults.Length && finalHoleResults[i] == i && i < extremities.Count && i < pixelCounts.Length && allTextures[i] != null)
                {
                    print("e");
                    UpdateObject(allVerts[i], pixelCounts[i], ref colliderArray, i, mass, extremities[i], origPos, origWidth, origHeight);
                    firstObject = false;
                }
            }
            else
            {
                if (i < finalHoleResults.Length && finalHoleResults[i] == i && i < extremities.Count && i < pixelCounts.Length && allTextures[i] != null)
                {
                    InstantiateNewObject(allVerts[i], pixelCounts[i], ref colliderArray, i, mass, extremities[i], origPos, origWidth, origHeight);
                }
            }
            
        }
        for (int i = 0; i < allVerts.Count; i++)
        {
            if (i < finalHoleResults.Length && finalHoleResults[i] != i && finalHoleResults[i] < extremities.Count && finalHoleResults[i] < colliderArray.Length)
            {
                ProcessHoleObject(allVerts[i], colliderArray[finalHoleResults[i]], extremities[finalHoleResults[i]]);
            }
        }
        if (!preserveThisObject)
        {
            Destroy(this.gameObject);
        }
        else
        {
            if (col.pathCount < 2)
            {
                if (col.pathCount > 0)
                {
                    if (col.GetPath(0).Length <= 2)
                    {
                        col.enabled = false;
                        Destroy(this.gameObject, 3f);
                    }
                }
                else
                {
                    col.enabled = false;
                    Destroy(this.gameObject, 3f);
                }
            }
        }
    }

    void ProcessVerts(ref Vertices verts, int pixelCountObject, int[] extremities)
    {
        if (pixelCountObject <= 5 && pixelCountObject >= 0)
        {
            verts = new Vertices();
        }
        verts = SimplifyTools.DouglasPeuckerSimplify(verts, distanceThreshold);
        for (int j = 0; j < verts.Count; j++)
        {
            float xValue = ((verts[j].x - (float) ((extremities[1] + extremities[0])/2f))/ppu) * (1/resolutionFactorDecimal);
            float yValue = ((verts[j].y - (float) ((extremities[3] + extremities[2])/2f))/ppu) * (1/resolutionFactorDecimal);
            verts[j] = new Vector2(xValue, yValue);
        }
    }

    void UpdateObject(Vertices verts, int pixelCountObject, ref PolygonCollider2D[] colliderArray, int i, float totalMass, int[] extremities, Vector3 origPos, int origWidth, int origHeight)
    {
        ProcessVerts(ref verts, pixelCountObject, extremities);
        PolygonCollider2D poly = GetComponent<PolygonCollider2D>();
        float diffX = ((extremities[1] * (1/resolutionFactorDecimal) + extremities[0] * (1/resolutionFactorDecimal))  / 2f - (origWidth / 2f)) / ppu;
        float diffY = ((extremities[3] * (1/resolutionFactorDecimal) + extremities[2] * (1/resolutionFactorDecimal)) / 2f - (origHeight / 2f)) / ppu;
        float angle = transform.rotation.eulerAngles.z * Mathf.Deg2Rad;
        Vector2 diff = new Vector2(Mathf.Cos(angle) * diffX - Mathf.Sin(angle) * diffY, Mathf.Sin(angle) * diffX + Mathf.Cos(angle) * diffY);
        Vector3 pos = origPos;
        pos = new Vector3(pos.x + (diff.x * transform.localScale.x), pos.y + (diff.y * transform.localScale.y), pos.z);
        transform.position = pos;
        colliderArray[i] = poly;
        spriteTx = allTextures[i];
        texWidth = allTextures[i].width;
        texHeight = allTextures[i].height;
        canBreak = false;
        doBreak = false;
        colorsFinalized = false;
        countsFinalized = false;
        holesFinalized = false;
        poly.pathCount += 1;
        print( poly.pathCount + " " + verts.Count + " regulah");
        poly.SetPath(poly.pathCount - 1, verts.ToArray());
        isOrig = false;
        pixelCount = pixelCountObject;
        if (totalPixels > 0)
        {
            rb.mass = totalMass * ((float)pixelCountObject / totalPixels);
        }
        if (extremities[1] == extremities[0])
        {
            extremities[1] += 1;
        }
        if (extremities[2] == extremities[3])
        {
            extremities[3] += 1;
        }
        float pivotX = (((texWidth * resolutionFactorDecimal) / 2f) - (extremities[0]))/(extremities[1] - extremities[0]);
        float pivotY = (((texHeight * resolutionFactorDecimal) / 2f) - (extremities[2]))/(extremities[3] - extremities[2]);
        // print(i + ": left extreme: " + extremities[i][0] + ", right extreme: " + extremities[i][1] + ", bottom extreme: " + extremities[i][2] + ", top extreme: " + extremities[i][3] + ", pivotX: " + pivotX + ", pivot Y: " + pivotY);
        sR.sprite = Sprite.Create(spriteTx, new Rect(0, 0, spriteTx.width, spriteTx.height), new Vector2(0.5f, 0.5f),
            sR.sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    void InstantiateNewObject(Vertices verts, int pixelCountObject, ref PolygonCollider2D[] colliderArray, int i, float totalMass, int[] extremities, Vector3 origPos, int origWidth, int origHeight)
    {
        ProcessVerts(ref verts, pixelCountObject, extremities);

        if (destroyedPrefab == null)
        {
            print("null prefab");
            return;
        }

        float diffX = ((extremities[1] * (1/resolutionFactorDecimal) + extremities[0] * (1/resolutionFactorDecimal))  / 2f - (origWidth / 2f)) / ppu;
        float diffY = ((extremities[3] * (1/resolutionFactorDecimal) + extremities[2] * (1/resolutionFactorDecimal)) / 2f - (origHeight / 2f)) / ppu;
        float angle = transform.rotation.eulerAngles.z * Mathf.Deg2Rad;
        Vector2 diff = new Vector2(Mathf.Cos(angle) * diffX - Mathf.Sin(angle) * diffY, Mathf.Sin(angle) * diffX + Mathf.Cos(angle) * diffY);
        Vector3 pos = origPos;
        pos = new Vector3(pos.x + (diff.x * transform.localScale.x), pos.y + (diff.y * transform.localScale.y), pos.z);
        GameObject destroyedPart = GameObject.Instantiate(destroyedPrefab, pos, transform.rotation);
        destroyedPart.transform.localScale = transform.localScale;
        PolygonCollider2D poly = destroyedPart.GetComponent<PolygonCollider2D>();
        breakableObject breakableScript = destroyedPart.GetComponent<breakableObject>();
        colliderArray[i] = poly;
        //Texture2D tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        //tex.SetPixels32(colors);
        //tex.Apply();
        //tex.filterMode = FilterMode.Point;
        if (verts.Count > 2)
        {
            poly.pathCount = 1;
            poly.SetPath(0, verts.ToArray());
            breakableScript.isOrig = false;
            //TODO: do txPixels and spriteTx set here? though i suppose it has to be done after the sprite.create?
            breakableScript.ppu = ppu;
            breakableScript.texWidth = allTextures[i].width;
            breakableScript.texHeight = allTextures[i].height;
            breakableScript.colliderGenerator = colliderGenerator;
            breakableScript.kiHoleFinderViaVertices = kiHoleFinderViaVertices;
            breakableScript.kiGetPixelCountsAndTextures = kiGetPixelCountsAndTextures;
            breakableScript.resolutionFactorDecimal = resolutionFactorDecimal;
            breakableScript.pixelCount = pixelCountObject;
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
            breakableScript.rb.mass = totalMass * ((float)pixelCountObject / totalPixels);
        }
        
        //TODO: figure out how to make this sprite mesh generation quicker or eliminate it by passing in a mesh.
        if (extremities[1] == extremities[0])
        {
            extremities[1] += 1;
        }
        if (extremities[2] == extremities[3])
        {
            extremities[3] += 1;
        }
        float pivotX = (((texWidth * resolutionFactorDecimal) / 2f) - (extremities[0]))/(extremities[1] - extremities[0]);
        float pivotY = (((texHeight * resolutionFactorDecimal) / 2f) - (extremities[2]))/(extremities[3] - extremities[2]);
       // print(i + ": left extreme: " + extremities[i][0] + ", right extreme: " + extremities[i][1] + ", bottom extreme: " + extremities[i][2] + ", top extreme: " + extremities[i][3] + ", pivotX: " + pivotX + ", pivot Y: " + pivotY);
        destroyedPart.GetComponent<SpriteRenderer>().sprite = Sprite.Create(allTextures[i], new Rect(0, 0, allTextures[i].width, allTextures[i].height), new Vector2(0.5f, 0.5f),
           sR.sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    void ProcessHoleObject(Vertices verts, PolygonCollider2D holeCollider, int[] holePolyExtremities)
    {
        ProcessVerts(ref verts, -1, holePolyExtremities);
        if (holeCollider != null)
        {
            holeCollider.pathCount += 1;
            holeCollider.SetPath(holeCollider.pathCount - 1, verts.ToArray());
        }
        else
        {
            print("null collider for hole");
        }
    }
}
