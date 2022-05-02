using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using DigitalRuby.AdvancedPolygonCollider;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;
using Object = System.Object;

public class PolygonDetector
{
    private const int closePixelsLength = 4;
    private static int[,] closePixels = new int[closePixelsLength, 2] { { 0, -1 }, { 1, 0 }, { 0, 1 }, { -1, 0 } };
    private const float hullTolerance = 0.9f;

    public byte[] solids;
    public Color32[] colors;
    public int alphaTolerance;
    private int solidsLength;
    public int width;
    public int height;
    private int texWidth;
    private int texHeight;
    public int arrayWidth;
    public bool finished;

    public Color[] pixelsDetected;
    public int[] idArray;
    public Color[] idArrayTex;
    private Color colorToShade = Color.green;
    Color blankColor = new Color(0f,0f,0f,0f);
    public int shapeCount = 0;
    public List<int> polygonCounts = new List<int>();
    public List<int> polygonStartIndices = new List<int>();
    public double tempTime = 0.0;
    private int currentID = -1;
    private Color32 currentColor;
    public float resolutionFactor;
    public List<Vector2> result;
    public int[] holeArray;
    public Color32[][] textureArray;
    public Color32[] allColsDebug;
    public int[] pixelCounts;
    public int totalPixels;
    private List<Vertices> allPolygons;
    
    public void DetectPolygons(Object stateInfo/*UnityEngine.Color[] colors, int width, int alphaTolerance, float resolutionFactor*/)
    {
        colorToShade = Color.black;
        polygonCounts.Clear();
        polygonStartIndices.Clear();
        this.resolutionFactor = resolutionFactor;
        texWidth = width;
        texHeight = height;
        this.width = (int)(width * resolutionFactor);
        this.height = (int)(height * resolutionFactor);
        int len = (int) ((texWidth * resolutionFactor) * (texHeight * resolutionFactor));
        int n, s;
        shapeCount = 0;

        solids = new byte[len];
        pixelsDetected = new Color[len];
        idArray = new int[len];
        idArrayTex = new Color[len];
        solidsLength = solids.Length;
        // calculate solids once, this makes tolerance lookups much faster
        for (int x = 0; x < texWidth; x+=(int)(1/resolutionFactor))
        {
            for (int y = 0; y < texHeight; y+=(int)(1/resolutionFactor))
            {
                // Diff will be negative for visitable value, resulting in a sign multiply and value of 0.
                // Sign multiply will be -1 for positive visitable values, resulting in non-zero value.
                bool solid = false;
                int i = (int)((y * resolutionFactor) * width + (x * resolutionFactor));
                if (i >= len)
                {
                    continue;
                }
                //Debug.Log((x) + " " + (y) + " " + i);
                int yIterationSteps = (int)(1 / resolutionFactor);
                int xIterationSteps = (int)(1 / resolutionFactor);
                if (y + yIterationSteps >= texHeight)
                {
                    yIterationSteps = texHeight - y;
                }

                if (x + xIterationSteps >= texWidth)
                {
                    xIterationSteps = texWidth - x;
                }
                for (int x1 = 0; x1 < xIterationSteps; x1++)
                {
                    for (int y1 = 0; y1 < yIterationSteps; y1++)
                    {
                        n = alphaTolerance - (int)(colors[(y + y1) * arrayWidth + (x + x1)].a * 255.0f);
                        s = ((int)((n & 0x80000000) >> 31)) - 1; // sign multiplier is -1 for positive numbers and 0 for negative numbers
                        n = n * s * s; // multiply n by sign squared to get 0 for negative numbers (solid) and > zero for positive numbers (pass through)
                        byte b = (byte)n;
                        if (b == 0)
                        {
                            solids[i] = b;
                            idArray[i] = -1;
                            idArrayTex[i] = new Color(1/255f, 0, 0, 0);
                            pixelsDetected[i] = Color.blue;
                            solid = true;
                        }
                    }
                }
                if (!solid)
                {
                    n = alphaTolerance - (int)(colors[(y) * arrayWidth + (x)].a * 255.0f);
                    s = ((int)((n & 0x80000000) >> 31)) - 1; // sign multiplier is -1 for positive numbers and 0 for negative numbers
                    n = n * s * s; // multiply n by sign squared to get 0 for negative numbers (solid) and > zero for positive numbers (pass through)
                    solids[i] = (byte)n;
                    idArray[i] = -2;
                    pixelsDetected[i] = Color.black;
                    idArrayTex[i] = new Color(0, 0, 0, 0);
                }
                /*n = alphaTolerance - (int)(colors[y * width + x].a * 255.0f);
                s = ((int)((n & 0x80000000) >> 31)) - 1; // sign multiplier is -1 for positive numbers and 0 for negative numbers
                n = n * s * s; // multiply n by sign squared to get 0 for negative numbers (solid) and > zero for positive numbers (pass through)
                int i = (int)((y * resolutionFactor) * (width * resolutionFactor) + (x * resolutionFactor));
                if (i >= solidsLength)
                {
                    i = solidsLength - 1;
                }
                solids[i] = (byte)n; // assign value back
                if (IsSolid(i))
                {
                    idArray[i] = -1;
                }
                else
                {
                    idArray[i] = -2;
                }*/
            }
        }
        /* for (int i = 0; i < solids.Length; i++)
         {
             // Diff will be negative for visitable value, resulting in a sign multiply and value of 0.
             // Sign multiply will be -1 for positive visitable values, resulting in non-zero value.
             n = alphaTolerance - (int)(colors[i].a * 255.0f);
             s = ((int)((n & 0x80000000) >> 31)) - 1; // sign multiplier is -1 for positive numbers and 0 for negative numbers
             n = n * s * s; // multiply n by sign squared to get 0 for negative numbers (solid) and > zero for positive numbers (pass through)
             solids[i] = (byte)n; // assign value back
             if (IsSolid(i))
             {
                 idArray[i] = -1;
                 pixelsDetected[i] = Color.black;
             }
             else
             {
                 pixelsDetected[i] = Color.blue;
                 idArray[i] = -2;
             }
                 
            
         }*/
        //double startTime = Time.realtimeSinceStartup;
        List<Vertices> detectedVerticesList = DoDetectPolygons();
        //tempTime = Time.realtimeSinceStartup - startTime;
        //TODO: OPTIMIZE by doing during the operation - takes 3ms at times
        
        result = new List<Vector2>();

        for (int i = 0; i < detectedVerticesList.Count; i++)
        {
            for (int j = 0; j < detectedVerticesList[i].Count; j++)
            {
                if (j == 0)
                {
                    polygonStartIndices.Add(result.Count);
                }
                result.Add(detectedVerticesList[i][j]);
            }
            polygonCounts.Add(detectedVerticesList[i].Count);
        }

        allPolygons = detectedVerticesList;
        textureArray = new Color32[allPolygons.Count][];
        for (int i = 0; i < allPolygons.Count; i++)
        {
            textureArray[i] = new Color32[texWidth * texHeight];
        }
        DetectHoles();
        FindPixelCountsAndTextures();
        finished = true;
    }

    public List<Vertices> DoDetectPolygons()
    {
        List<Vertices> detectedPolygons = new List<Vertices>();
        Vector2? holeEntrance = null;
        Vector2? polygonEntrance = null;
        List<Vector2> blackList = new List<Vector2>();
        bool searchOn;
        bool firstPass = true;
        do
        {
            Vertices polygon;
            if (firstPass)
            {
                currentID = 0;
                currentColor = new Color(2f/255f, 0, 0, 0);
                //colorToShade = new Color(System.Random(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
                // First pass / single polygon
                polygon = new Vertices(CreateSimplePolygon(Vector2.zero, Vector2.zero, true));
                shapeCount++;
                if (polygon.Count > 2)
                {
                    polygonEntrance = GetTopMostVertex(polygon);
                }
                else if(polygon.Count == 2)
                {
                    polygonEntrance = new Vector2(polygon[1].x, polygon[1].y);
                }else if (polygon.Count == 1)
                {
                    polygonEntrance = new Vector2(polygon[0].x, polygon[0].y);
                }
                firstPass = false;
            }
            else if (polygonEntrance.HasValue)
            {
                shapeCount++;
                // Multi pass / multiple polygons
                polygon = new Vertices(CreateSimplePolygon(polygonEntrance.Value, new Vector2(polygonEntrance.Value.x - 1f, polygonEntrance.Value.y), true));
            }
            else
            {
                break;
            }
            searchOn = false;
            
            /*if (polygon.Count > 2)
            {
                do
                {
                    holeEntrance = SearchHoleEntrance(polygon, holeEntrance);

                    if (holeEntrance.HasValue)
                    {
                        if (!blackList.Contains(holeEntrance.Value))
                        {
                            blackList.Add(holeEntrance.Value);
                            shapeCount++;
                            Vertices holePolygon = CreateSimplePolygon(holeEntrance.Value, new Vector2(holeEntrance.Value.x + 1, holeEntrance.Value.y), false);
                            if (holePolygon != null && holePolygon.Count > 2)
                            {
                                // Add first hole polygon vertex to close the hole polygon.
                                holePolygon.Add(holePolygon[0]);
                                SearchHoleEntrance(holePolygon, null);
                                int vertex1Index, vertex2Index;
                                if (SplitPolygonEdge(polygon, holeEntrance.Value, out vertex1Index, out vertex2Index))
                                {
                                    polygon.InsertRange(vertex2Index, holePolygon);
                                }
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                while (true);
            }*/
            detectedPolygons.Add(polygon);
            currentID++;
            int current = currentID + 20;
            int a = 0;
            int r = current;
            int g = 0;
            int b = 0;
            if (current > 255 && current <= 510)
            {
                r = 255;
                g = current - 255;
            }else if (current > 510 && current <= 765)
            {
                r = 255;
                g = 255;
                b = current - 510;
            }
            else if(current > 765)
            {
                r = 255;
                g = 255;
                b = 255;
                a = current - 765;
            }
            currentColor = new Color(r/255f, g/255f, b/255f, a/255f);
            colorToShade = new Color(colorToShade.r + 0.02f, colorToShade.g + 0.02f, colorToShade.b + 0.02f);
            if (polygonEntrance.HasValue && SearchNextHullEntrance(detectedPolygons, polygonEntrance.Value, out polygonEntrance))
            {
                searchOn = true;
            }
        }
        while (searchOn);

        if (detectedPolygons == null || (detectedPolygons != null && detectedPolygons.Count == 0))
        {
            //throw new WarningException("Couldn't detect verts");
            return new List<Vertices>();
        }
            
        return detectedPolygons;
    }

    private void DetectHoles()
    {
        holeArray = new int[allPolygons.Count];
        int[] nonRepeatingEntranceIds;
        int[] nonRepeatingEntranceIdsCounts;
        for (int i = 0; i < allPolygons.Count; i++)
        {
            int startX = Mathf.FloorToInt(allPolygons[i][0].x);
            int y = Mathf.RoundToInt(allPolygons[i][0].y);
            int polyIndex = i;
            pixelsDetected[y * width + startX] = Color.green;
            int insideTotal = 0;
            int totalLines = 0;
            nonRepeatingEntranceIds = new int[allPolygons.Count];
            nonRepeatingEntranceIdsCounts = new int[allPolygons.Count];
            for (int x = startX; x < width; x++)
            {
                //Debug.Log(x + " " + y + " "  + (y * width + x) + " " + pixelsDetected.Length);
                if (idArray[y * width + x] != -1 && idArray[y * width + x] != -2 &&
                    (y * width + x - 1 < 0 || idArray[y * width + x - 1] == -2 || idArray[y * width + x + 1] == -2 || y * width + x + 1 >= idArray.Length) &&
                    idArray[y * width + x] != polyIndex)
                {
                    int currentInd = idArray[y * width + x];
                    bool newPoly = true;
                    int polyInd = -1;
                    for(int j = 0;j < insideTotal;j++)
                    {
                        if(nonRepeatingEntranceIds[j] == currentInd)
                        {
                            newPoly = false;
                            polyInd = j;
                            break;
                        }
                    }
                    if(newPoly)
                    {
                        polyInd = insideTotal;
                        nonRepeatingEntranceIds[polyInd] = currentInd;
                        insideTotal += 1;
                    }
                    if(idArray[y * width + x - 1] == -2 && idArray[y * width + x + 1] == -2)
                    {
                        nonRepeatingEntranceIdsCounts[polyInd] += 2;
                        if (nonRepeatingEntranceIdsCounts[polyInd] % 2 == 1)
                        {
                            pixelsDetected[y * width + x] = Color.magenta;
                        }
                        else
                        {
                            pixelsDetected[y * width + x] = Color.red;
                        }
                        totalLines += 2;
                    }else
                    {
                        nonRepeatingEntranceIdsCounts[polyInd] += 1;
                        if (nonRepeatingEntranceIdsCounts[polyInd] % 2 == 1)
                        {
                            pixelsDetected[y * width + x] = Color.magenta;
                        }
                        else
                        {
                            pixelsDetected[y * width + x] = Color.red;
                        }
                        totalLines += 1;
                    }
                }
            }
            bool isHole = false;
            if(totalLines % 2 == 1)
            {
                for(int k = 0;k < insideTotal;k++)
                {
                    if(nonRepeatingEntranceIdsCounts[k] % 2 == 1)
                    {
                        isHole = true;
                        pixelsDetected[y * width + startX] = Color.blue;
                        holeArray[polyIndex] = nonRepeatingEntranceIds[k];
                        break;
                    }

                    if (k == insideTotal - 1)
                    {
                        pixelsDetected[y * width + startX] = Color.cyan;
                    }
                }
            }
            if(!isHole)
            {
                holeArray[polyIndex] = polyIndex;
            }
        }
    }

    private void FindPixelCountsAndTextures()
    {
        pixelCounts = new int[allPolygons.Count];
        allColsDebug = new Color32[texWidth * texHeight];
        for (int y = 0; y < height; y++)
        {
            int mostRecentID = -1;
            for (int x = 0; x < width; x++)
            {
                if (IsSolid(x, y) && idArray[y * width + x] != -1)
                {
                    mostRecentID = idArray[y * width + x];
                }

                Color pixelDetect = pixelsDetected[(int) ((y) * (width) + (x))];
                for (int x1 = 0; x1 < (1 / resolutionFactor); x1++)
                {
                    for (int y1 = 0; y1 < (1 / resolutionFactor); y1++)
                    {
                        if (IsSolid(x, y) && mostRecentID != -1)
                        {
                            textureArray[holeArray[mostRecentID]][
                                (int) ((y * (1 / resolutionFactor) + y1) * (texWidth) +
                                       (x * (1 / resolutionFactor) + x1))] = colors[
                                (int) ((y * (1 / resolutionFactor) + y1) * (arrayWidth) +
                                       (x * (1 / resolutionFactor) + x1))];
                            pixelCounts[holeArray[mostRecentID]] += 1;
                        }

                        if (pixelDetect.a > 0)
                        {
                            allColsDebug[
                                (int) ((y * (1 / resolutionFactor) + y1) * (texWidth) +
                                       (x * (1 / resolutionFactor) + x1))] = pixelDetect;
                        }
                        else
                        {
                            allColsDebug[
                                (int) ((y * (1 / resolutionFactor) + y1) * (texWidth) +
                                       (x * (1 / resolutionFactor) + x1))] = colors[
                                (int) ((y * (1 / resolutionFactor) + y1) * (arrayWidth) +
                                       (x * (1 / resolutionFactor) + x1))];
                        }

                    }
                }
                //pixelCounts[holeArray[mostRecentID]] += (int) (1 / resolutionFactor) * (int) (1 / resolutionFactor);
            }
        }

        for (int i = 0; i < pixelCounts.Length; i++)
        {
            totalPixels += pixelCounts[i];
        }
    }
    
    private Vertices CreateSimplePolygon(Vector2 entrance, Vector2 last, bool newPolygonData)
    {
        bool entranceFound = false;
        bool endOfHull = false;

        Vertices polygon = new Vertices(32);
        Vertices hullArea = new Vertices(32);
        Vertices endOfHullArea = new Vertices(32);

        Vector2 current = Vector2.zero;

        #region Entrance check

        // Get the entrance point.
        if (entrance == Vector2.zero || !InBounds(ref entrance))
        {
            entranceFound = SearchHullEntrance(out entrance);
            if (entranceFound)
            {
                current = new Vector2(entrance.x - 1f, entrance.y);
            }
        }
        else
        {
            if (IsSolid(ref entrance))
            {
                if (IsNearPixel(ref entrance, ref last))
                {
                    current = last;
                    entranceFound = true;
                }
                else
                {
                    Vector2 temp;
                    if (SearchNearPixels(false, ref entrance, out temp))
                    {
                        current = temp;
                        entranceFound = true;
                    }
                    else
                    {
                        entranceFound = false;
                    }
                }
            }
        }
        #endregion

        
        if (entranceFound)
        {
            polygon.Add(entrance);
            
            hullArea.Add(entrance);

            Vector2 next = entrance;

            do
            {
                // Last point gets current and current gets next. Our little spider is moving forward on the hull ;).
                last = current;
                current = next;

                // Get the next point on hull.
                // TODO: Fix infinite loop here sometimes...
                if (GetNextHullPoint(ref last, ref current, out next) && next != entrance)
                {
                    // Add the vertex to a hull pre vision list.
                    polygon.Add(next);
                }
                else
                {
                    // Quit
                    break;
                }


            } while (true);
        }
        return polygon;
    }
    
    private bool SearchHullEntrance(out Vector2 entrance)
    {
        // Search for first solid pixel.
        for (int y = 0; y <= height; y++)
        {
            for (int x = 0; x <= width; x++)
            {
                if (IsSolid(x, y))
                {
                    //pixelsDetected[y * width + x] = Color.magenta;
                    entrance = new Vector2(x, y);
                    return true;
                }
            }
        }

        // If there are no solid pixels.
        entrance = Vector2.zero;
        return false;
    }

    /// <summary>
    /// Searches for the next shape.
    /// </summary>
    /// <param name="detectedPolygons">Already detected polygons.</param>
    /// <param name="start">Search start coordinate.</param>
    /// <param name="entrance">Returns the found entrance coordinate. Null if no other shapes found.</param>
    /// <returns>True if a new shape was found.</returns>
    private bool SearchNextHullEntrance(List<Vertices> detectedPolygons, Vector2 start, out Vector2? entrance)
    {
        int x;

        bool foundTransparent = false;
        bool inPolygon = false;

        for (int i = (int)start.x + (int)start.y * width; i <= solidsLength; i++)
        {
            if (IsSolid(i))
            {
                if (foundTransparent)
                {
                    x = i % width;
                    entrance = new Vector2(x, (i - x) / (float)width);

                    inPolygon = false;
                    if (idArray[i] >= 0) /*!pixelsDetected[i].Equals(blankColor)*/
                    {
                        inPolygon = true;
                    }
                    else
                    {
                        //pixelsDetected[i] = Color.magenta;
                        idArray[i] = currentID;
                        idArrayTex[i] = currentColor;
                    }
                    /*inPolygon = false;
                    for (int polygonIdx = 0; polygonIdx < detectedPolygons.Count; polygonIdx++)
                    {
                        if (InPolygon(detectedPolygons[polygonIdx], entrance.Value))
                        {
                            inPolygon = true;
                            break;
                        }
                    }*/

                    if (inPolygon)
                        foundTransparent = false;
                    else
                        return true;
                }
            }
            else
                foundTransparent = true;
        }

        entrance = null;
        return false;
    }
    
    private bool GetNextHullPoint(ref Vector2 last, ref Vector2 current, out Vector2 next)
    {
        int x;
        int y;

        int indexOfFirstPixelToCheck = GetIndexOfFirstPixelToCheck(ref last, ref current);
        int indexOfPixelToCheck;

        for (int i = 0; i < closePixelsLength; i++)
        {
            indexOfPixelToCheck = (indexOfFirstPixelToCheck + i) % closePixelsLength;

            x = (int)current.x + closePixels[indexOfPixelToCheck, 0];
            y = (int)current.y + closePixels[indexOfPixelToCheck, 1];
                
                
            if (x >= 0 && x < width && y >= 0 && y <= height)
            {
                if (IsSolid(x, y))
                {
                    Vector2 v = new Vector2(x, y);
                    if (idArray[x + y * width] < 0) /*pixelsDetected[(int) v.x + (int) v.y * width].Equals(blankColor)*/
                    {
                        pixelsDetected[(x + y * width)] = colorToShade;
                        idArray[x + y * width] = currentID;
                        idArrayTex[x + y * width] = currentColor;
                    }
                    next = new Vector2(x, y);
                    return true;
                }
            }
        }

        next = Vector2.zero;
        return false;
    }


    private Vector2? GetTopMostVertex(Vertices vertices)
    {
        float topMostValue = float.MaxValue;
        Vector2? topMost = null;

        for (int i = 0; i < vertices.Count; i++)
        {
            if (topMostValue > vertices[i].y)
            {
                topMostValue = vertices[i].y;
                topMost = vertices[i];
            }
        }

        return topMost;
    }
    
    public bool InBounds(ref Vector2 coord)
     {
         return (coord.x >= 0f && coord.x < width && coord.y >= 0f && coord.y < height);
     }
     
     public bool IsSolid(ref Vector2 v)
     {
         return IsSolid((int)v.x + ((int)v.y * width));
     }

     public bool IsSolid(int x, int y)
     {
         return IsSolid(x + (y * width));
     }

     public bool IsSolid(int index)
     {
         if (index >= 0 && index < solids.Length && solids[index] == 0)
         {
             return true;
         }

         return false;
         //return (index >= 0 && index < solids.Length && solids[index] == 0);
     }

     private bool IsNearPixel(ref Vector2 current, ref Vector2 near)
    {
        for (int i = 0; i < closePixelsLength; i++)
        {
            int x = (int)current.x + closePixels[i, 0];
            int y = (int)current.y + closePixels[i, 1];

            if (x >= 0 && x <= width && y >= 0 && y <= height)
            {
                if (x == (int)near.x && y == (int)near.y)
                {
                    return true;
                }
            }
        }

        return false;
    }
    
    private bool SearchNearPixels(bool searchingForSolidPixel, ref Vector2 current, out Vector2 foundPixel)
    {
        for (int i = 0; i < closePixelsLength; i++)
        {
            int x = (int)current.x + closePixels[i, 0];
            int y = (int)current.y + closePixels[i, 1];

            if (!searchingForSolidPixel ^ IsSolid(x, y))
            {
                Vector2 v = new Vector2(x, y);
                //TODO: next line necessary?
                /*idArray[(int) v.x + (int) v.y * width] = currentID;
                pixelsDetected[(int)v.x + (int)v.y * width] = colorToShade;*/
                foundPixel = new Vector2(x, y);
                return true;
            }
        }

        // Nothing found.
        foundPixel = Vector2.zero;
        return false;
    }
    
    private int GetIndexOfFirstPixelToCheck(ref Vector2 last, ref Vector2 current)
    {
        // .: pixel
        // l: last position
        // c: current position
        // f: first pixel for next search

        // f . .
        // l c .
        // . . .

        //Calculate in which direction the last move went and decide over the next pixel to check.
        switch ((int)(current.x - last.x))
        {
            case 1:
                switch ((int)(current.y - last.y))
                {
                    case 1:
                        return 1;

                    case 0:
                        return 0;

                    case -1:
                        return 7;
                }
                break;

            case 0:
                switch ((int)(current.y - last.y))
                {
                    case 1:
                        return 1;

                    case -1:
                        return 3;
                }
                break;

            case -1:
                switch ((int)(current.y - last.y))
                {
                    case 1:
                        return 3;

                    case 0:
                        return 2;

                    case -1:
                        return 5;
                }
                break;
        }

        return 0;
    }



}
