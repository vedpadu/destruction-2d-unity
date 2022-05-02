using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class ColliderGenerator : MonoBehaviour
{
    public static List<breakableObject> allObjects = new List<breakableObject>();
    public Texture2DArray tempArray;
    private static ComputeShader colliderGenShader;
    private static int kiFindHoles;
    private static int kiGetPixelCountsAndTextures;
    private bool hasDoneDetection = false;
    private bool hasFinishedHole;
    private bool hasFinishedCounts;
    private bool hasFinishedTextures;

    private void Awake()
    {
        colliderGenShader = Resources.Load<ComputeShader>("ColliderGenerationCollective");
        kiFindHoles = colliderGenShader.FindKernel("FindHolesViaVerticesCollective");
        kiGetPixelCountsAndTextures = colliderGenShader.FindKernel("GetPixelCountsAndTextures");
    }

    private void Update()
    {
        if (!hasDoneDetection && allObjects.Count > 0)
        {
            DoHoleFindingAndPixelCounting();
        }

        if (hasDoneDetection && hasFinishedCounts && hasFinishedHole && hasFinishedTextures)
        {
            hasDoneDetection = false;
            hasFinishedCounts = false;
            hasFinishedHole = false;
            hasFinishedTextures = false;
            allObjects.Clear();
        }
    }

    public void DoHoleFindingAndPixelCounting()
    {
        for (int i = 0; i < allObjects.Count; i++)
        {
            if (!allObjects[i].readyForHoleFind)
            {
                return;
            }
        }
        hasDoneDetection = true;
        List<Vector2> verts = new List<Vector2>();
        int[] objectStartIndicesVerts = new int[allObjects.Count + 1];
        int[] objectStartIndicesEntranceIds = new int[allObjects.Count];
        Vector2Int[] spriteBounds = new Vector2Int[allObjects.Count];
        float[] resolutionFactors = new float[allObjects.Count];
        int currentInd = 0;
        int totalPolyEntrances = 0;
        int longestWidth = 0;
        int longestHeight = 0;
        int longestWidthUnadjusted = 0;
        int longestHeightUnadjusted = 0;
        int maxPolygonCount = 0;
        for (int k = 0; k < allObjects.Count; k++)
        {
            resolutionFactors[k] = allObjects[k].resolutionFactor;
            int widthUnadjust = allObjects[k].texWidth;
            int heightUnadjust = allObjects[k].texHeight;
            if (widthUnadjust > longestWidthUnadjusted)
            {
                longestWidthUnadjusted = widthUnadjust;
            }
            if (heightUnadjust > longestHeightUnadjusted)
            {
                longestHeightUnadjusted = heightUnadjust;
            }
            spriteBounds[k] = new Vector2Int((int)(widthUnadjust * allObjects[k].resolutionFactor), (int)(heightUnadjust * allObjects[k].resolutionFactor));
            if (spriteBounds[k].x > longestWidth)
            {
                longestWidth = spriteBounds[k].x;
            }
            if (spriteBounds[k].y > longestHeight)
            {
                longestHeight = spriteBounds[k].y;
            }
            objectStartIndicesVerts[k] = currentInd;
            objectStartIndicesEntranceIds[k] = totalPolyEntrances;
            if (allObjects[k].polygonCounts.Count > maxPolygonCount)
            {
                maxPolygonCount = allObjects[k].polygonCounts.Count;
            }
            for (int j = 0; j < allObjects[k].polygonCounts.Count; j++)
            {
                totalPolyEntrances += allObjects[k].polygonCounts.Count * allObjects[k].polygonCounts.Count;
                verts.Add(allObjects[k].hullVerticesUnSimp[allObjects[k].polygonStartIndices[j]] * allObjects[k].resolutionFactor);
                currentInd += 1;
            }
        }
        objectStartIndicesVerts[objectStartIndicesVerts.Length - 1] = verts.Count;
        RenderTexture idTextures = new RenderTexture(longestWidth, longestHeight, 0, RenderTextureFormat.ARGB32);
        idTextures.enableRandomWrite = true;
        idTextures.filterMode = FilterMode.Point;
        idTextures.volumeDepth = allObjects.Count;
        idTextures.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        idTextures.Create();
        RenderTexture polygonTextures = new RenderTexture(longestWidthUnadjusted, longestHeightUnadjusted, 0, RenderTextureFormat.ARGB32);
        polygonTextures.enableRandomWrite = true;
        polygonTextures.filterMode = FilterMode.Point;
        polygonTextures.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        polygonTextures.volumeDepth = verts.Count;
        polygonTextures.Create();
        RenderTexture pixelCountsOutput = new RenderTexture(maxPolygonCount, 1, 0, RenderTextureFormat.RG32);
        pixelCountsOutput.enableRandomWrite = true;
        pixelCountsOutput.filterMode = FilterMode.Point;
        pixelCountsOutput.dimension = TextureDimension.Tex2DArray;
        pixelCountsOutput.volumeDepth = allObjects.Count;
        pixelCountsOutput.Create();
        RenderTexture debugTextures = new RenderTexture(longestWidth, longestHeight, 0, RenderTextureFormat.ARGB32);
        debugTextures.enableRandomWrite = true;
        debugTextures.filterMode = FilterMode.Point;
        debugTextures.volumeDepth = allObjects.Count;
        debugTextures.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        debugTextures.Create();
        RenderTexture holeOutput = new RenderTexture(maxPolygonCount, 1, 0, RenderTextureFormat.R16);
        holeOutput.enableRandomWrite = true;
        holeOutput.filterMode = FilterMode.Point;
        holeOutput.dimension = TextureDimension.Tex2DArray;
        holeOutput.volumeDepth = allObjects.Count;
        holeOutput.Create();
        Texture2DArray origTextures = new Texture2DArray(longestWidthUnadjusted, longestHeightUnadjusted,
            allObjects.Count, TextureFormat.ARGB32, false);
        for (int i = 0; i < allObjects.Count; i++)
        {
            //Graphics.CopyTexture(allObjects[i].spriteTx, 0, 0, 0, 0, allObjects[i].texWidth, allObjects[i].texHeight, tempArray, i, 0, 0, 0);
            Graphics.CopyTexture(allObjects[i].spriteTx, 0, 0, 0, 0, allObjects[i].texWidth, allObjects[i].texHeight, origTextures, i, 0, 0, 0);
            Graphics.CopyTexture(allObjects[i].idArrayTex, 0, 0, 0, 0, spriteBounds[i].x, spriteBounds[i].y, idTextures, i, 0, 0, 0);
           // Graphics.CopyTexture(allObjects[i].idArrayTex, 0, 0, 0, 0, spriteBounds[i].x, spriteBounds[i].y, tempArray, i, 0, 0, 0);
        }
        ComputeBuffer nonRepeatingEntranceIds = new ComputeBuffer(totalPolyEntrances * 2, 4 * 1);
        ComputeBuffer startIndicesEntrances = new ComputeBuffer(allObjects.Count, 4 * 1);
        ComputeBuffer startIndicesVerts = new ComputeBuffer(allObjects.Count + 1, 4 * 1);
        ComputeBuffer allVerts = new ComputeBuffer(verts.Count, 4 * 2);
        ComputeBuffer boundsBuffer = new ComputeBuffer(spriteBounds.Length, 4 * 2);

        ComputeBuffer pixelCountsBuffer = new ComputeBuffer(verts.Count, 4 * 1);
        ComputeBuffer holeResultsBuffer = new ComputeBuffer(verts.Count, 4 * 1);
        ComputeBuffer resolutionFactorBuffer = new ComputeBuffer(allObjects.Count, 4 * 1);
        
        pixelCountsBuffer.SetData(new int[verts.Count]);
        holeResultsBuffer.SetData(new int[verts.Count]);
        resolutionFactorBuffer.SetData(resolutionFactors);
        startIndicesEntrances.SetData(objectStartIndicesEntranceIds);
        startIndicesVerts.SetData(objectStartIndicesVerts);
        allVerts.SetData(verts);
        boundsBuffer.SetData(spriteBounds);
        nonRepeatingEntranceIds.SetData(new int[totalPolyEntrances]);
        colliderGenShader.SetInt("maxPolys", maxPolygonCount);
        colliderGenShader.SetInt("totalObjects", allObjects.Count);
        colliderGenShader.SetInt("totalVerts", verts.Count);
        colliderGenShader.SetInt("totalPolyEntrances", totalPolyEntrances);
        colliderGenShader.SetFloat("resolutionFactor", 1f); //todo make procedural
        colliderGenShader.SetBuffer(kiFindHoles, "nonRepeatingEntranceIdsAndCounts", nonRepeatingEntranceIds);
        //colliderGenShader.SetBuffer(kiFindHoles, "nonRepeatingEntranceIdsCounts", nonRepeatingEntranceIdsCounts);
        colliderGenShader.SetBuffer(kiFindHoles, "allVerts", allVerts);
        colliderGenShader.SetBuffer(kiFindHoles, "startIndicesEntrances", startIndicesEntrances);
        colliderGenShader.SetBuffer(kiFindHoles, "startIndicesVerts", startIndicesVerts);
        colliderGenShader.SetBuffer(kiFindHoles, "spriteBounds", boundsBuffer);
        colliderGenShader.SetTexture(kiFindHoles, "idTextures", idTextures);
        colliderGenShader.SetTexture(kiFindHoles, "outputDebugTex", debugTextures);
        colliderGenShader.SetTexture(kiFindHoles, "holeOutputTex", holeOutput);
        colliderGenShader.SetBuffer(kiFindHoles, "polyHoleResults", holeResultsBuffer);
        
        colliderGenShader.SetBuffer(kiGetPixelCountsAndTextures, "pixelCounts", pixelCountsBuffer);
        colliderGenShader.SetBuffer(kiGetPixelCountsAndTextures, "spriteBounds", boundsBuffer);
        colliderGenShader.SetBuffer(kiGetPixelCountsAndTextures, "resolutionFactors", resolutionFactorBuffer);
        colliderGenShader.SetBuffer(kiGetPixelCountsAndTextures, "polyHoleResults", holeResultsBuffer);
        colliderGenShader.SetTexture(kiGetPixelCountsAndTextures, "idTextures", idTextures);
        colliderGenShader.SetTexture(kiGetPixelCountsAndTextures, "allTextures", polygonTextures);
        colliderGenShader.SetTexture(kiGetPixelCountsAndTextures, "origTextures", origTextures);
        colliderGenShader.SetTexture(kiGetPixelCountsAndTextures, "pixelCountsOutput", pixelCountsOutput);
        colliderGenShader.SetBuffer(kiGetPixelCountsAndTextures, "startIndicesVerts", startIndicesVerts);
        colliderGenShader.Dispatch(kiFindHoles, Mathf.CeilToInt(allObjects.Count/8f), Mathf.CeilToInt(maxPolygonCount / 8f), 1);
        colliderGenShader.Dispatch(kiGetPixelCountsAndTextures, Mathf.CeilToInt(longestHeight/32f), Mathf.CeilToInt(allObjects.Count/8f), 1);
        /* AsyncGPUReadback.Request(debugTextures, 0, 0, longestWidth, 0, longestHeight, 0, debugTextures.volumeDepth,
             new Action<AsyncGPUReadbackRequest>
             (
                 (AsyncGPUReadbackRequest request) =>
                 {
                     if (!request.hasError)
                     {
                         for (var i = 0; i < request.layerCount; i++)
                         {
                             tempArray.SetPixels32(request.GetData<Color32>(i).ToArray(), i);
                         }
                         debugTextures.Release();
                     }
 
                     tempArray.filterMode = FilterMode.Point;
                     tempArray.Apply();
                 }
             ));*/
        AsyncGPUReadback.Request(holeOutput, 0, 0, maxPolygonCount, 0, 1, 0, holeOutput.volumeDepth,
            new Action<AsyncGPUReadbackRequest>
            (
                (AsyncGPUReadbackRequest request) =>
                {
                    if (!request.hasError)
                    {
                        for (int j = 0; j < request.layerCount; j++)
                        {
                            Int16[] data = request.GetData<Int16>(j).ToArray();
                            //print(objectStartIndicesVerts[j + 1] + " " + objectStartIndicesVerts[j]);
                            for (int i = 0; i < data.Length; i++) //(objectStartIndicesVerts[j + 1] - objectStartIndicesVerts[j])
                            {
                                int x = (int) data[i];
                                if (x < 0)
                                {
                                    x += 65536;
                                }
                                //print(j + " " + i + " " + (x - 1));
                                if (i < allObjects[j].holes.Length)
                                {
                                    allObjects[j].holes[i] = x - 1;
                                }
                            }
                            allObjects[j].holesFinalized = true;
                        }
                        hasFinishedHole = true;
                        holeOutput.Release();
                    }
                }
            ));
        AsyncGPUReadback.Request(pixelCountsOutput, 0, 0, maxPolygonCount, 0, 1, 0, pixelCountsOutput.volumeDepth,
            new Action<AsyncGPUReadbackRequest>
            (
                (AsyncGPUReadbackRequest requestPixelCounts) =>
                {
                    if (!requestPixelCounts.hasError)
                    {
                        for (int i = 0; i < requestPixelCounts.layerCount; i++)
                        {
                            Int32[] dataPixelCounts = requestPixelCounts.GetData<Int32>(i).ToArray();
                            allObjects[i].totalPixels = 0;
                            for (int j = 0; j < allObjects[i].pixelCountsResults.Length; j++)
                            {
                                allObjects[i].pixelCountsResults[j] = (int) dataPixelCounts[j];
                                allObjects[i].totalPixels += dataPixelCounts[j];
                            }
                            allObjects[i].countsFinalized = true;
                        }
                        hasFinishedCounts = true;
                        pixelCountsOutput.Release();
                    }
                }
            ));
        tempArray = new Texture2DArray(longestWidthUnadjusted, longestHeightUnadjusted, verts.Count,
            TextureFormat.ARGB32, false);
        tempArray.filterMode = FilterMode.Point;
        AsyncGPUReadback.Request(polygonTextures, 0, 0, longestWidthUnadjusted, 0, longestHeightUnadjusted, 0, polygonTextures.volumeDepth,
            new Action<AsyncGPUReadbackRequest>
            (
                (AsyncGPUReadbackRequest request) =>
                {
                    if (!request.hasError)
                    {
                        for (int i = 0; i < allObjects.Count; i++)
                        { 
                            allObjects[i].allPixelData = new List<Texture2D>();
                            for (int j = objectStartIndicesVerts[i]; j < objectStartIndicesVerts[i + 1]; j++)
                            {
                                print(j);
                                tempArray.SetPixels32(request.GetData<Color32>(j).ToArray(), j);
                                Texture2D tex = new Texture2D(allObjects[i].texWidth, allObjects[i].texHeight, TextureFormat.RGBA32, false);
                                tex.filterMode = FilterMode.Point;
                                Graphics.CopyTexture(polygonTextures, j, 0, 0, 0, allObjects[i].texWidth, allObjects[i].texHeight, tex, 0, 0, 0, 0);
                                allObjects[i].allPixelData.Add(tex);
                                // tex.SetPixels32(allColors[i]);
                                //tex.Apply();
                                //tex.filterMode = FilterMode.Point;
                                // tempArray.SetPixels32(request.GetData<Color32>(j).ToArray(), j);
                                // allObjects[i].allPixelData.Add(request.GetData<Color32>(j).ToArray());
                            }
                            allObjects[i].colorsFinalized = true;
                        }
                        hasFinishedTextures = true;
                        polygonTextures.Release();
                        tempArray.Apply();
                    }
                }
            ));
        pixelCountsBuffer.Release();
        holeResultsBuffer.Release();
        resolutionFactorBuffer.Release();
        allVerts.Release();
        nonRepeatingEntranceIds.Release();
        startIndicesEntrances.Release();
        startIndicesVerts.Release();
        idTextures.Release();
        boundsBuffer.Release();
        //allObjects.Clear();
    }
}
