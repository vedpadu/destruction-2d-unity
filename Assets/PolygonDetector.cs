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
    /* PARAMETERS */
    public Color32[] colors; //original passed in color array, used to generate list of solids and also the final texures
    public int alphaTolerance; //at what alpha a pixel is not solid.
    public int width;//width and height adjusted later by resolution factor
    public int height;
    public int arrayWidth;//width of color array, as it may be longer than the tex width, as it may be collectively processed in the object fracturer
    public float resolutionFactor;//factor of how much smaller the processed texture is vs. the original texture, for performance on larger images.
    
    /* GENERAL VARIABLES */
    private byte[] solids;//array of bytes that tells us whether objects are transparent (alpha above the alpha tolerance)
    private int solidsLength;//length of solids array
    private int texWidth;//width of texture, unadjusted
    private int texHeight;//height of texture, unadjusted
    private Color colorToShade; //the color to shade to the debug texture, for differentiating polygons
    private int currentID = -1; //current polygon id
    private const int closePixelsLength = 4;
    private static int[,] closePixels = new int[closePixelsLength, 2] { { 0, -1 }, { 1, 0 }, { 0, 1 }, { -1, 0 } };
    private int leftMostPixel;
    private int rightMostPixel;
    private int topMostPixel;
    private int bottomMostPixel;
    
    /* OUTPUT VARIABLES */
    public bool finished; //tells the object script when the thread is finished
    public List<Vertices> allPolygons = new List<Vertices>(); //polygon list
    public Color[] debugPixels; //a debug texture of sorts
    public int[] idArray; //which pixels are which polygon, in a texture like array
    public Color32[] allColsDebug; //a secondary debug texture
    public List<Vector2> result; //every polygon vert in a traversable array
    public List<int> polygonCounts = new List<int>(); //guides for how to traverse the result array (when each polygon starts and how long they are in the array).
    public List<int> polygonStartIndices = new List<int>();
    public int[] holeArray; //an int array which tells which polygons are holes of which other polygons
    public Color32[][] textureArray; //output textures for each polygon
    public int[] pixelCounts; //the number of pixels in each polygon (for mass calculations)
    public int totalPixels; //the total number of pixels in the whole object
    public List<int[]> shapeExtremeties = new List<int[]>();

    //This function is not parameterized, as we can run it in a thread pool and pass parameters into class variables.
    public void DetectPolygons(Object stateInfo)
    {
        colorToShade = Color.black;
        polygonCounts.Clear();
        shapeExtremeties.Clear();
        polygonStartIndices.Clear();
        texWidth = width;
        texHeight = height;
        this.width = (int)(width * resolutionFactor);
        this.height = (int)(height * resolutionFactor);
        leftMostPixel = width;
        rightMostPixel = 0;
        topMostPixel = height;
        bottomMostPixel = 0;
        int len = (int) ((texWidth * resolutionFactor) * (texHeight * resolutionFactor));
        int n, s;

        solids = new byte[len];
        debugPixels = new Color[len];
        idArray = new int[len];
        solidsLength = solids.Length;
        // calculate solids once, this makes tolerance lookups much faster - creates bytes in which 0 = not solid.
        for (int x = 0; x < texWidth; x+=(int)(1/resolutionFactor))
        {
            for (int y = 0; y < texHeight; y+=(int)(1/resolutionFactor))
            {
                bool solid = false;
                int i = (int)((y * resolutionFactor) * width + (x * resolutionFactor));
                if (i >= len)
                {
                    continue;
                }
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
                        // Diff will be negative for visitable value, resulting in a sign multiply and value of 0.
                        // Sign multiply will be -1 for positive visitable values, resulting in non-zero value.
                        n = alphaTolerance - (int)(colors[(y + y1) * arrayWidth + (x + x1)].a * 255.0f);
                        s = ((int)((n & 0x80000000) >> 31)) - 1; // sign multiplier is -1 for positive numbers and 0 for negative numbers
                        n = n * s * s; // multiply n by sign squared to get 0 for negative numbers (solid) and > zero for positive numbers (pass through)
                        byte b = (byte)n;
                        if (b == 0)
                        {
                            solids[i] = b;
                            idArray[i] = -1;
                            debugPixels[i] = Color.blue;
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
                    debugPixels[i] = Color.black;
                }
            }
        }
        allPolygons.Clear();
        List<Vertices> detectedVerticesList = DoDetectPolygons();
        //TODO: OPTIMIZE by doing during the creation of polygons - takes 3ms at times
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
        //DetectHoles();
        //FindPixelCountsAndTextures();
        finished = true;
    }

    public List<Vertices> DoDetectPolygons()
    {
        List<Vertices> detectedPolygons = new List<Vertices>();
        Vector2? holeEntrance = null;
        Vector2? polygonEntrance = null;
        bool searchOn;
        bool firstPass = true;
        do
        {
            Vertices polygon;
            if (firstPass)
            {
                currentID = 0;
                // First pass / single polygon
                polygon = new Vertices(CreateSimplePolygon(Vector2.zero, Vector2.zero, true));
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
                // Multi pass / multiple polygons
                polygon = new Vertices(CreateSimplePolygon(polygonEntrance.Value, new Vector2(polygonEntrance.Value.x - 1f, polygonEntrance.Value.y), true));
            }
            else
            {
                break;
            }
            searchOn = false;
            detectedPolygons.Add(polygon);
            currentID++;
            colorToShade = new Color(colorToShade.r + 0.1f, colorToShade.g + 0.1f, colorToShade.b + 0.1f);
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

    private Vertices CreateSimplePolygon(Vector2 entrance, Vector2 last, bool newPolygonData)
    {
        bool entranceFound = false;
        bool endOfHull = false;

        Vertices polygon = new Vertices(32);
        Vertices hullArea = new Vertices(32);

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
            int[] extremities = new int[4] {width, 0, height, 0};
            polygon.Add(entrance);
            
            hullArea.Add(entrance);
            if (entrance.x < extremities[0])
            {
                extremities[0] = (int)entrance.x;
            }
            if (entrance.x > extremities[1])
            {
                extremities[1] = (int) entrance.x;
            }
            if (entrance.y < extremities[2])
            {
                extremities[2] = (int) entrance.y;
            }
            if (entrance.y > extremities[3])
            {
                extremities[3] = (int) entrance.y;
            }
            
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
                    if (next.x < extremities[0])
                    {
                        extremities[0] = (int)next.x;
                    }
                    if (next.x > extremities[1])
                    {
                        extremities[1] = (int) next.x;
                    }
                    if (next.y < extremities[2])
                    {
                        extremities[2] = (int) next.y;
                    }
                    if (next.y > extremities[3])
                    {
                        extremities[3] = (int) next.y;
                    }
                }
                else
                {
                    // Quit
                    break;
                }


            } while (true);
            shapeExtremeties.Add(extremities);
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
                    if (idArray[i] >= 0)
                    {
                        inPolygon = true;
                    }
                    else
                    {
                        idArray[i] = currentID;
                    }
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
                    if (idArray[x + y * width] < 0)
                    {
                        debugPixels[(x + y * width)] = colorToShade;
                        idArray[x + y * width] = currentID;
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
