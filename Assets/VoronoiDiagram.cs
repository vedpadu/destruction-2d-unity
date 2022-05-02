using UnityEngine;
using System.Collections.Generic;
 
using csDelaunay;
using UnityEngine.Serialization;

public class VoronoiDiagram : MonoBehaviour {
 
    // The number of polygons/sites we want
    public int polygonNumber = 200;
 
    // This is where we will store the resulting data
    private Dictionary<Vector2f, Site> sites;
    private List<Edge> edges;
    public int lloydTimes;
    public SpriteRenderer sR;
    private Texture2D spriteTx;
    private Color[] txPixels;
 
    void Start()
    {
        spriteTx = new Texture2D(sR.sprite.texture.width, sR.sprite.texture.height);
        spriteTx.SetPixels(sR.sprite.texture.GetPixels());
        spriteTx.Apply();
        spriteTx.filterMode = FilterMode.Point;
        sR.sprite = Sprite.Create(spriteTx, new Rect(0.0f, 0.0f, spriteTx.width, spriteTx.height), new Vector2(0.5f, 0.5f),
            sR.sprite.pixelsPerUnit);
        txPixels = spriteTx.GetPixels();
        // Create your sites (lets call that the center of your polygons)
        List<Vector2f> points = CreateRandomPoint();
       
        // Create the bounds of the voronoi diagram
        // Use Rectf instead of Rect; it's a struct just like Rect and does pretty much the same,
        // but like that it allows you to run the delaunay library outside of unity (which mean also in another thread)
        Rectf bounds = new Rectf(0,0,spriteTx.width,spriteTx.height - 1);
       
        // There is a two ways you can create the voronoi diagram: with or without the lloyd relaxation
        // Here I used it with 2 iterations of the lloyd relaxation
        Voronoi voronoi = new Voronoi(points,bounds,lloydTimes);
 
        // But you could also create it without lloyd relaxtion and call that function later if you want
        //Voronoi voronoi = new Voronoi(points,bounds);
        //voronoi.LloydRelaxation(5);
 
        // Now retreive the edges from it, and the new sites position if you used lloyd relaxtion
        sites = voronoi.SitesIndexedByLocation;
        edges = voronoi.Edges;
 
        DisplayVoronoiDiagram();
    }
   
    private List<Vector2f> CreateRandomPoint() {
        // Use Vector2f, instead of Vector2
        // Vector2f is pretty much the same than Vector2, but like you could run Voronoi in another thread
        List<Vector2f> points = new List<Vector2f>();
        
        int i = 0;
        while (i < polygonNumber)
        {
            Vector2f random = new Vector2f(Random.Range(0, spriteTx.width), Random.Range(0, spriteTx.height - 1));
            if (!Mathf.Approximately(txPixels[Mathf.RoundToInt(random.x + (random.y * spriteTx.width))].a,0))
            {
                points.Add(random);
                i += 1;
            }
        }

        return points;
    }
 
    // Here is a very simple way to display the result using a simple bresenham line algorithm
    // Just attach this script to a quad
    private void DisplayVoronoiDiagram() {
        Texture2D tx = new Texture2D(this.spriteTx.width,this.spriteTx.height - 1);
        foreach (KeyValuePair<Vector2f,Site> kv in sites) {
            tx.SetPixel((int)kv.Key.x, (int)kv.Key.y, Color.red);
        }
        foreach (Edge edge in edges) {
            // if the edge doesn't have clippedEnds, if was not within the bounds, dont draw it
            if (edge.ClippedEnds == null) continue;
 
            DrawLine(edge.ClippedEnds[LR.LEFT], edge.ClippedEnds[LR.RIGHT], tx, Color.black);
            //DrawLine(edge.ClippedEnds[LR.LEFT] + new Vector2f(0f, 1f), edge.ClippedEnds[LR.RIGHT ]+ new Vector2f(0f, 1f), tx, Color.black);
            //DrawLine(edge.ClippedEnds[LR.LEFT] + new Vector2f(-1f, 1f), edge.ClippedEnds[LR.RIGHT ]+ new Vector2f(-1f, 1f), tx, Color.black);
            //DrawLine(edge.ClippedEnds[LR.LEFT] + new Vector2f(-1f, 0f), edge.ClippedEnds[LR.RIGHT ]+ new Vector2f(-1f, 0f), tx, Color.black);
        }
        tx.Apply();
        spriteTx.Apply();
        //this.GetComponent<Renderer>().material.mainTexture = tx;
    }
 
    // Bresenham line algorithm
    private void DrawLine(Vector2f p0, Vector2f p1, Texture2D tx, Color c, int offset = 0) {
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
            if ((x0 + ((y0) * this.spriteTx.width)) < txPixels.Length && (x0 + ((y0) * this.spriteTx.width)) >= 0 && !Mathf.Approximately(txPixels[(x0 + ((y0) * this.spriteTx.width))].a, 0))
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
            }
            
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
 
