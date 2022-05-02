using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DigitalRuby.AdvancedPolygonCollider;
using UnityEngine;
using Random = UnityEngine.Random;
using csDelaunay;


public class breakableBuildingScript : MonoBehaviour
{
    private Texture2D spriteTx;
    private float ppu;
    private int texWidth;
    private int texHeight;
    private SpriteRenderer sR;
    
    private AdvancedPolygonCollider advancedCollider;
    public GameObject prefab;
    private Rigidbody2D rb;

    public MeshRenderer quadToRenderVoronoi;
    private Color[] txPixels;

    void Awake()
    {
        SpriteRenderer sR = GetComponent<SpriteRenderer>();
        spriteTx = new Texture2D(sR.sprite.texture.width, sR.sprite.texture.height);
        spriteTx.SetPixels(sR.sprite.texture.GetPixels());
        spriteTx.Apply();
        spriteTx.filterMode = FilterMode.Point;
        sR.sprite = Sprite.Create(spriteTx, new Rect(0.0f, 0.0f, spriteTx.width, spriteTx.height), new Vector2(0.5f, 0.5f),
            sR.sprite.pixelsPerUnit);
    }
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        advancedCollider = GetComponent<AdvancedPolygonCollider>();
        sR = GetComponent<SpriteRenderer>();
        txPixels = sR.sprite.texture.GetPixels();
        /*tex = new Texture2D(sR.sprite.texture.width, sR.sprite.texture.height);
        tex.SetPixels(sR.sprite.texture.GetPixels());
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        sR.sprite = Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0.5f, 0.5f),
            sR.sprite.pixelsPerUnit);*/
        ppu = GetComponent<SpriteRenderer>().sprite.pixelsPerUnit;
        texWidth = spriteTx.width;
        texHeight = spriteTx.height;
        //GetStructureBlocks();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.U))
        {
            if (advancedCollider.polygonTextures.Count > 1)
            {
                BreakObject();
            }
        }
        

        if (Input.GetMouseButtonDown(0))
        {
            var pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            pos.z = transform.position.z;
            pos = transform.InverseTransformPoint(pos);
     
            int xPixel = Mathf.RoundToInt(pos.x * ppu);
            int yPixel = Mathf.RoundToInt(pos.y * ppu);
            yPixel += (sR.sprite.texture.height/2);
            xPixel += (sR.sprite.texture.width / 2);
            DoVoronoiBreakage((float)xPixel, (float)yPixel, 25f, 6f, Random.Range(5,20), 0, quadToRenderVoronoi);
            //BreakObject();
        }
    }

    void BreakObject()
    {
        Texture2D[] textures = advancedCollider.polygonTextures.ToArray();
        int[] pixelCounts = advancedCollider.polygonPixels.ToArray();
        int[] doCollider = advancedCollider.doCollider.ToArray();
        PolygonCollider2D polygonCol = GetComponent<PolygonCollider2D>();
        print(polygonCol.pathCount);
        int texIndex = -1;
        int pathIndex = 0;
        for (var i = 0; i < doCollider.Length; i++)
        {
            if (doCollider[i] == 0 || doCollider[i] == 1)
            {
                texIndex += 1;
                if (doCollider[i] == 0)
                {
                    GameObject destroyedPart = GameObject.Instantiate(prefab, transform.position, transform.rotation);
                    destroyedPart.transform.localScale = transform.localScale;
                    PolygonCollider2D poly = destroyedPart.GetComponent<PolygonCollider2D>();
                    poly.pathCount = 1;
                    poly.SetPath(0, polygonCol.GetPath(pathIndex));

                    destroyedPart.GetComponent<Rigidbody2D>().mass = 2 * rb.mass * ((float)pixelCounts[texIndex] / (float)advancedCollider.totalPixels);
                    destroyedPart.GetComponent<SpriteRenderer>().sprite = Sprite.Create(textures[texIndex], new Rect(0.0f, 0.0f, texWidth, texHeight), new Vector2(0.5f, 0.5f),
                        sR.sprite.pixelsPerUnit);
                    pathIndex += 1;
                }
                
            }
            
        }
        print(texIndex);
        
        Destroy(this.gameObject);
    }

    void DoVoronoiBreakage(float aroundX, float aroundY, float force, float density, float shatterCount, int lloydRelaxation, MeshRenderer mR)
    {
        print(aroundX + " " + aroundY);
        List<Vector2f> points = CreateVoronoiPoints(shatterCount, aroundX, aroundY, force, density);
        Rectf bounds = new Rectf(0,0,spriteTx.width,spriteTx.height - 1);
        Voronoi voronoi = new Voronoi(points,bounds, lloydRelaxation);
        Dictionary<Vector2f, Site> sites = voronoi.SitesIndexedByLocation;
        List<Edge> edges = voronoi.Edges;
        DisplayVoronoiDiagram(mR, sites, edges, new Vector2f(aroundX, aroundY), force);
        advancedCollider.dirty = true;
    }
    
    private void DisplayVoronoiDiagram(MeshRenderer mR, Dictionary<Vector2f, Site> sites, List<Edge> edges, Vector2f epicenter, float dist) {
        if (mR == null)
        {
            return;
        }
        Texture2D tx = new Texture2D(this.spriteTx.width,this.spriteTx.height - 1);
        foreach (KeyValuePair<Vector2f,Site> kv in sites) {
            tx.SetPixel((int)kv.Key.x, (int)kv.Key.y, Color.red);
        }
        foreach (Edge edge in edges) {
            // if the edge doesn't have clippedEnds, if was not within the bounds, dont draw it
            if (edge.ClippedEnds == null) continue;

            DrawLine(edge.ClippedEnds[LR.LEFT], edge.ClippedEnds[LR.RIGHT], tx, Color.black, epicenter, dist);

            /*if ((edge.ClippedEnds[LR.LEFT] - epicenter).magnitude < dist && (edge.ClippedEnds[LR.RIGHT] - epicenter).magnitude < dist)
            {
                DrawLine(edge.ClippedEnds[LR.LEFT], edge.ClippedEnds[LR.RIGHT], tx, Color.black, epicenter, dist);
            }*/
            
            //DrawLine(edge.ClippedEnds[LR.LEFT] + new Vector2f(0f, 1f), edge.ClippedEnds[LR.RIGHT ]+ new Vector2f(0f, 1f), tx, Color.black);
            //DrawLine(edge.ClippedEnds[LR.LEFT] + new Vector2f(-1f, 1f), edge.ClippedEnds[LR.RIGHT ]+ new Vector2f(-1f, 1f), tx, Color.black);
            //DrawLine(edge.ClippedEnds[LR.LEFT] + new Vector2f(-1f, 0f), edge.ClippedEnds[LR.RIGHT ]+ new Vector2f(-1f, 0f), tx, Color.black);
        }
        tx.Apply();
        spriteTx.Apply();
        mR.material.mainTexture = tx;
    }

    private Vector2f randomPointWeighted(float aroundX, float aroundY, float scale, float density)
    {
        float angle = Random.Range(0f, 1f) * 2f * Mathf.PI;

        float x = Random.Range(0f, 1f);
        if (Mathf.Approximately(x, 0f))
        {
            x = 0.0000001f;
        }

        float distance = scale * (Mathf.Pow(x, -1.0f / density) - 1);
        return new Vector2f(aroundX + distance * Mathf.Sin(angle),
            aroundY + distance * Mathf.Cos(angle));
    }

    private List<Vector2f> CreateVoronoiPoints(float polygonNumber, float aroundX, float aroundY, float scale, float density) {
        // Use Vector2f, instead of Vector2
        // Vector2f is pretty much the same than Vector2, but like you could run Voronoi in another thread
        List<Vector2f> points = new List<Vector2f>();
        
        int i = 0;
        while (i < polygonNumber)
        {
            Vector2f random = randomPointWeighted(aroundX, aroundY, scale, density);
            points.Add(random);
            i++;
        }

        return points;
    }

    // Bresenham line algorithm
    private void DrawLine(Vector2f p0, Vector2f p1, Texture2D tx, Color c, Vector2f epicenter, float dist, int offset = 0) {
        int x0 = (int)p0.x;
        int y0 = (int)p0.y;
        int x1 = (int)p1.x;
        int y1 = (int)p1.y;
       
        int dx = Mathf.Abs(x1-x0);
        int dy = Mathf.Abs(y1-y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx-dy;
        bool hasDoneStart = false;
        bool startingCon = false;
        bool edgeClipped = false;
        while (true) {
            /*if ((x0 + ((y0) * this.spriteTx.width)) < txPixels.Length && (x0 + ((y0) * this.spriteTx.width)) >= 0 && !Mathf.Approximately(txPixels[(x0 + ((y0) * this.spriteTx.width))].a, 0))
            {
                if (!hasDoneStart)
                {
                    startingCon = true;
                    hasDoneStart = true;
                }
                if (hasDoneStart && !edgeClipped && !startingCon)
                {
                    tx.SetPixel(x0+offset,y0+offset,Color.green);
                    edgeClipped = true;
                }
                spriteTx.SetPixel(x0+offset,y0+offset, new Color(0f,0f,0f,0f));
                tx.SetPixel(x0+offset,y0+offset,c);
            }
            else
            {
                if (hasDoneStart && !edgeClipped && startingCon)
                {
                    tx.SetPixel(x0+offset,y0+offset,Color.green);
                    edgeClipped = true;
                }
                if (!hasDoneStart)
                {
                    startingCon = false;
                    hasDoneStart = true;
                }
            }*/
            
                spriteTx.SetPixel(x0+offset,y0+offset, new Color(0f,0f,0f,0f));
                tx.SetPixel(x0+offset,y0+offset,c);
            

            
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2*err;
            if (e2 > -dy) {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx) {
                err += dx;
                y0 += sy;
            }
        }
    }
    
    
    
}