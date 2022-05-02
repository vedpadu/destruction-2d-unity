using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using csDelaunay;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

public class ObjectFracturer : MonoBehaviour
{
    private static ComputeShader voronoiFractureShader = Resources.Load<ComputeShader>("VoronoiFractureShader");
    private static int kiVoronoiFractureKernel = voronoiFractureShader.FindKernel("VoronoiFracture");
    private static int kiApplyTexture = voronoiFractureShader.FindKernel("ApplyTexture");
    public static Texture2DArray debugTexArray;
        
    public static void DoVoronoiBreak(Vector2 origin, float blastRadius, LayerMask lM)
    {
        Collider2D[] allObjects = Physics2D.OverlapCircleAll(origin, blastRadius, lM);
    }
    
    //TODO: force is a misnomer, its actually point scale
    public static void DoVoronoiBreak(Vector2 origin, float blastRadius, float force, float density, float shatterCount, int lloydRelaxation)
    {
        float overlapCircleTime = Time.realtimeSinceStartup;
        Collider2D[] allObjects = Physics2D.OverlapCircleAll(origin, blastRadius);
        if (allObjects.Length == 0)
        {
            return;
        }
        overlapCircleTime = Time.realtimeSinceStartup - overlapCircleTime;
        float voronoiTime = Time.realtimeSinceStartup;
        int[] isBreakable = new int[allObjects.Length];
        breakableObject[] allScripts = new breakableObject[allObjects.Length];
        List<Vector2f> points = CreateVoronoiPoints(shatterCount, origin.x, origin.y, force, density);
        Rectf bounds = new Rectf(origin.x - blastRadius,origin.y-blastRadius,2 * blastRadius, 2 * blastRadius); //TODO: not actually a circular blast radius :skull:
        Voronoi voronoi = new Voronoi(points,bounds, lloydRelaxation);
        Dictionary<Vector2f, Site> sites = voronoi.SitesIndexedByLocation;
        List<Edge> edges = voronoi.Edges;
        List<Vector2> allLeftEdgeEnds = new List<Vector2>();
        List<Vector2> allRightEdgeEnds = new List<Vector2>();
        voronoiTime = Time.realtimeSinceStartup - voronoiTime;
        float processingTime = Time.realtimeSinceStartup;
        for (int i = 0; i < edges.Count; i++)
        {
            if (edges[i].ClippedEnds == null)
            {
                continue;
            }
            
            allLeftEdgeEnds.Add(new Vector2(edges[i].ClippedEnds[LR.LEFT].x, edges[i].ClippedEnds[LR.LEFT].y));
            allRightEdgeEnds.Add(new Vector2(edges[i].ClippedEnds[LR.RIGHT].x, edges[i].ClippedEnds[LR.RIGHT].y));
        }
        if (allLeftEdgeEnds.Count == 0)
        {
            return;
        }
        int totalBreakable = 0;
        for (int i = 0; i < allObjects.Length; i++)
        {
            breakableObject b;
            if (allObjects[i].gameObject.TryGetComponent(out b))
            {
                //TODO: make not a constant
                if (b.pixelCount > 100)
                {
                    isBreakable[i] = 1;
                    allScripts[i] = b;
                    totalBreakable++;
                }
            }
        }
        breakableObject[] scripts = new breakableObject[totalBreakable];
        int currentIndex = 0;
        int[] textureCounts = new int[totalBreakable];
        int[] textureStartIndices = new int[totalBreakable];
        Vector3[] objectTransforms = new Vector3[totalBreakable];
        Vector2[] objectScales = new Vector2[totalBreakable];
        float[] ppus = new float[totalBreakable];
        SpriteRenderer[] sRs = new SpriteRenderer[totalBreakable];
        Vector2Int[] spriteBounds = new Vector2Int[totalBreakable];
        int currentStartIndex = 0;
        int longestWidth = 0;
        int longestHeight = 0;
        for (int i = 0; i < allScripts.Length; i++)
        {
            if (isBreakable[i] == 1)
            {
                //bool accDrew;
                //allScripts[i].DoVoronoiBreakage(edges, origin, force, out accDrew);
                Transform t = allObjects[i].gameObject.transform;
                objectTransforms[currentIndex] = new Vector3(t.position.x, t.position.y, t.rotation.eulerAngles.z * Mathf.Deg2Rad);
                objectScales[currentIndex] = new Vector2(t.localScale.x, t.localScale.y);
                SpriteRenderer sR = allScripts[i].sR;
                sRs[currentIndex] = sR;
                scripts[currentIndex] = allScripts[i];
                ppus[currentIndex] = allScripts[i].ppu;
                textureCounts[currentIndex] = allScripts[i].texWidth * allScripts[i].texHeight;
                textureStartIndices[currentIndex] = currentStartIndex;
                currentStartIndex += allScripts[i].texWidth * allScripts[i].texHeight;
                spriteBounds[currentIndex] = new Vector2Int(allScripts[i].texWidth, allScripts[i].texHeight);
                if (allScripts[i].texWidth > longestWidth)
                {
                    longestWidth = allScripts[i].texWidth;
                }

                if (allScripts[i].texHeight > longestHeight)
                {
                    longestHeight = allScripts[i].texHeight;
                }
                currentIndex++;
            }
        }
        if (totalBreakable == 0)
        {
            return;
        }
        processingTime = Time.realtimeSinceStartup - processingTime;

        float gettingDataPart2 = Time.realtimeSinceStartup;
        //currentStartIndex becomes the total count after the previous loop
        Color32[] allColors = new Color32[longestWidth * longestHeight * totalBreakable];
        /*Parallel.For((int)0, totalBreakable, i => {        
            Graphics.CopyTexture(scripts[i].spriteTx, 0, 0, 0, 0, scripts[i].texWidth, scripts[i].texHeight, allTex, 0, 0, 0, longestHeight * i);
            //System.Buffer.BlockCopy(scripts[i].txPixels, 0, allColors, longestHeight * longestWidth * i * size, scripts[i].txPixels.Length * size);
            //Array.Copy(scripts[i].txPixels, 0, allColors, longestHeight * longestWidth * i, scripts[i].txPixels.Length);
        });*/
        RenderTexture renderTex = new RenderTexture(longestWidth, longestHeight, 0, RenderTextureFormat.ARGB32);
        renderTex.enableRandomWrite = true;
        renderTex.filterMode = FilterMode.Point;
        renderTex.volumeDepth = totalBreakable;
        renderTex.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        renderTex.Create();
        RenderTexture hasBeenFilledTex = new RenderTexture(scripts.Length, 1, 0, RenderTextureFormat.R16);
        hasBeenFilledTex.enableRandomWrite = true;
        hasBeenFilledTex.filterMode = FilterMode.Point;
        hasBeenFilledTex.dimension = TextureDimension.Tex2D;
        hasBeenFilledTex.Create();
        for (int i = 0; i < totalBreakable; i++)
        {
            //Graphics.CopyTexture(scripts[i].spriteTx, 0, 0, 0, 0, scripts[i].texWidth, scripts[i].texHeight, allTex, 0, 0, 0, (longestHeight * totalBreakable - (longestHeight * (i + 1))) + (longestHeight - scripts[i].texHeight));
            Graphics.CopyTexture(scripts[i].spriteTx, 0, 0, 0, 0, scripts[i].texWidth, scripts[i].texHeight, renderTex, i, 0, 0, 0);
            //Array.Copy(scripts[i].txPixels, 0, allColors, longestHeight * longestWidth * i, scripts[i].txPixels.Length);
            //Graphics.CopyTexture(scripts[i].spriteTx, 0, 0, 0, 0, longestWidth, Mathf.CeilToInt(scripts[i].txPixels.Length/(float)longestWidth), allTex, 0, 0, 0, longestHeight * i);
            //allTex.SetPixels(0, longestHeight * i, longestWidth, scripts[i].txPixels.Length/longestWidth, scripts[i].txPixels);
        }
        gettingDataPart2 = Time.realtimeSinceStartup - gettingDataPart2;
        float computeShaderTime = Time.realtimeSinceStartup;
        
        ComputeBuffer leftEndsBuffer = new ComputeBuffer(allLeftEdgeEnds.Count, 4 * 2);
        ComputeBuffer rightEndsBuffer = new ComputeBuffer(allRightEdgeEnds.Count, 4 * 2);
        ComputeBuffer transformBuffer = new ComputeBuffer(objectTransforms.Length, 4 * 3);
        ComputeBuffer ppuBuffer = new ComputeBuffer(ppus.Length, 4 * 1);
        ComputeBuffer spriteBoundsBuffer = new ComputeBuffer(spriteBounds.Length, 4 * 2);
        ComputeBuffer scaleBuffer = new ComputeBuffer(objectScales.Length, 4 * 2);
        leftEndsBuffer.SetData(allLeftEdgeEnds.ToArray());
        rightEndsBuffer.SetData(allRightEdgeEnds.ToArray());
        transformBuffer.SetData(objectTransforms);
        spriteBoundsBuffer.SetData(spriteBounds);
        ppuBuffer.SetData(ppus);
        scaleBuffer.SetData(objectScales);
        voronoiFractureShader.SetInt("totalObjects", totalBreakable);
        voronoiFractureShader.SetInt("totalEdges", allLeftEdgeEnds.Count);
        voronoiFractureShader.SetInt("longestHeight", longestHeight);
        voronoiFractureShader.SetInt("longestWidth", longestWidth);
        voronoiFractureShader.SetFloat("resolutionFactor", 0.0625f);
        voronoiFractureShader.SetBuffer(kiVoronoiFractureKernel, "leftEdgeEnds", leftEndsBuffer);
        voronoiFractureShader.SetBuffer(kiVoronoiFractureKernel, "rightEdgeEnds", rightEndsBuffer);
        voronoiFractureShader.SetBuffer(kiVoronoiFractureKernel, "transforms", transformBuffer);
        voronoiFractureShader.SetBuffer(kiVoronoiFractureKernel, "scales", scaleBuffer);
        voronoiFractureShader.SetBuffer(kiVoronoiFractureKernel, "ppus", ppuBuffer);
        voronoiFractureShader.SetBuffer(kiVoronoiFractureKernel, "spriteBounds", spriteBoundsBuffer);
        voronoiFractureShader.SetTexture(kiVoronoiFractureKernel, "outputTex", renderTex);
        voronoiFractureShader.SetTexture(kiVoronoiFractureKernel, "hasBeenDrawnTex", hasBeenFilledTex);
        voronoiFractureShader.SetTexture(kiApplyTexture, "outputTex", renderTex);
        computeShaderTime = Time.realtimeSinceStartup - computeShaderTime;
        float computePart1 = Time.realtimeSinceStartup;
        //voronoiFractureShader.Dispatch(kiApplyTexture, Mathf.CeilToInt(longestWidth / 32f), Mathf.CeilToInt((longestHeight * totalBreakable) / 32f), 1);
        computePart1 = Time.realtimeSinceStartup - computePart1;
        float computePart2 = Time.realtimeSinceStartup;
        voronoiFractureShader.Dispatch(kiVoronoiFractureKernel, Mathf.CeilToInt(totalBreakable / 8f), Mathf.CeilToInt(allLeftEdgeEnds.Count / 8f), 1);
        computePart2 = Time.realtimeSinceStartup - computePart2;
        float getDataTime = Time.realtimeSinceStartup;
        //allColors = new Color[allColors.Length];
        //colorBuffer.GetData(allColors);
        getDataTime = Time.realtimeSinceStartup - getDataTime;
        int startInd = 0;
        float processDataTime = Time.realtimeSinceStartup;
        /*new Thread(() => 
        {
            Thread.CurrentThread.IsBackground = true;
            startInd = Thread.CurrentThread.ManagedThreadId;
        }).Start();*/
       /* for (int i = 0; i < totalBreakable; i++)
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                int ind = Thread.CurrentThread.ManagedThreadId - startInd - 1;
                print(ind);
                //Color[] colorTex = ;
                scripts[ind].txPixels = allColors.SubArray(textureStartIndices[ind], textureCounts[ind]);
                scripts[ind].dirty = true;
            }).Start();
        }*/
       /*Parallel.For((int)0, totalBreakable, i => {
           DateTime startTime = DateTime.Now;
           int ind = i;
           //Color[] colorTex = ;
           scripts[ind].txPixels = allColors.SubArray(textureStartIndices[ind], textureCounts[ind]);
           scripts[ind].dirty = true;
           DateTime endTime = DateTime.Now;
           TimeSpan timePassed = endTime.Subtract(startTime);
           print(timePassed);
       });*/
       AsyncGPUReadback.Request(hasBeenFilledTex, 0, 0, scripts.Length, 0, 1, 0, hasBeenFilledTex.volumeDepth,
           new Action<AsyncGPUReadbackRequest>
           (
               (AsyncGPUReadbackRequest hasBeenFilledRequest) =>
               {
                   if (!hasBeenFilledRequest.hasError)
                   {
                       Int16[] data = hasBeenFilledRequest.GetData<Int16>().ToArray();
                       //debugTexArray =
                        //   new Texture2DArray(longestWidth, longestHeight, renderTex.volumeDepth, TextureFormat.ARGB32, false);
                       AsyncGPUReadback.Request(renderTex, 0, 0, longestWidth, 0, longestHeight, 0, renderTex.volumeDepth,
                           new Action<AsyncGPUReadbackRequest>
                           (
                               (AsyncGPUReadbackRequest request) =>
                               {
                                   if (!request.hasError)
                                   {
                                       double start = Time.realtimeSinceStartup;
                                       // Copy the data
                                       float totalTime = 0f;
                                       for (var i = 0; i < totalBreakable; i++)
                                       {
                                           if (data[i] == 1)
                                           {
                                               float startTime = Time.realtimeSinceStartup;
                                               //remove in a sec
                                               //Graphics.CopyTexture(renderTex, i, 0, 0, 0, longestWidth, longestHeight, debugTexArray, i, 0, 0, 0);
                                               scripts[i].txPixels = request.GetData<Color32>(i).ToArray();
                                               scripts[i].arrayWidth = longestWidth;
                                               totalTime += (Time.realtimeSinceStartup - startTime);
                                               scripts[i].dirty = true;
                                           }
                                       }
                                       renderTex.Release();
                                   }
                               }
                           ));
                       hasBeenFilledTex.Release();
                   }
               }
           ));

       processDataTime = Time.realtimeSinceStartup - processDataTime;
        float disposal = Time.realtimeSinceStartup;
        leftEndsBuffer.Release();
        rightEndsBuffer.Release();
        transformBuffer.Release();
        ppuBuffer.Release();
        spriteBoundsBuffer.Release();
        scaleBuffer.Release();
            disposal = Time.realtimeSinceStartup - disposal;
        //print("Overlap Circle: " + overlapCircleTime + ", Voronoi Time: " + voronoiTime + ", Getting Data Time: " + processingTime + " , Getting Data Part 2 Time: " + gettingDataPart2 + ", Compute Shader Setup Time: " + computeShaderTime + ", " +
             // " compute shader apply tex time: " + computePart1 + " , computeShader do voronoiTime: " + computePart2 + ", get data time: " + getDataTime + ", process data time: " + processDataTime + ", disposal time: " + disposal + " , totalBreakable: " + totalBreakable);
    }

    private static List<Vector2f> CreateVoronoiPoints(float polygonNumber, float aroundX, float aroundY, float scale, float density) {
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
    
    private static Vector2f randomPointWeighted(float aroundX, float aroundY, float scale, float density)
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
}

public static class Extensions
{
    public static T[] SubArray<T>(this T[] array, int offset, int length)
    {
        return new List<T>(array)
            .GetRange(offset, length)
            .ToArray();
    }
}
