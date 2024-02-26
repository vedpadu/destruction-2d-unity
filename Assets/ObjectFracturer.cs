using System;
using System.Collections.Generic;
using System.Threading;
using csDelaunay;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

public class ObjectFracturer : MonoBehaviour
{
    private static ComputeShader voronoiFractureShader = Resources.Load<ComputeShader>("VoronoiFractureShader");
    private static int kiVoronoiFractureKernel = voronoiFractureShader.FindKernel("VoronoiFracture");
    private static int kiCircularFractureKernel = voronoiFractureShader.FindKernel("CircularFracture");
    private static int kiImageFractureKernel = voronoiFractureShader.FindKernel("ImageFracture");
    private static int kiAntiAliasKernel = voronoiFractureShader.FindKernel("AntiAlias");
    public static Texture2DArray debugTexArray;
    public static Texture2D debugTex;
        
    public static void DoVoronoiBreak(Vector2 origin, float blastRadius, LayerMask lM, float force, float density, float shatterCount, int lloydRelaxation)
    {
        Collider2D[] allObjects = Physics2D.OverlapCircleAll(origin, blastRadius, lM);
        if (allObjects.Length == 0)
        {
            return;
        }
        RunVoronoiBreakage(allObjects, origin, blastRadius, force, density, shatterCount, lloydRelaxation);
    }
    
    //TODO: force is a misnomer, its actually point scale
    public static void DoVoronoiBreak(Vector2 origin, float blastRadius, float force, float density, float shatterCount, int lloydRelaxation)
    {
        //runs an overlap circle to detect the objects we are going to destroy.
        Collider2D[] allObjects = Physics2D.OverlapCircleAll(origin, blastRadius);
        if (allObjects.Length == 0)
        {
            return;
        }
        RunVoronoiBreakage(allObjects, origin, blastRadius, force, density, shatterCount, lloydRelaxation);
    }

    public static void DoCircularBreak(Vector2 origin, float blastRadius, float probabilityFalloff)
    {
        //runs an overlap circle to detect the objects we are going to destroy.
        Collider2D[] allObjects = Physics2D.OverlapCircleAll(origin, blastRadius);
        if (allObjects.Length == 0)
        {
            return;
        }
        RunCircularBreakage(allObjects, origin, blastRadius, probabilityFalloff);
    }

    public static void DoImageBreakage(Vector2 origin, Texture2D[] fractureImages, float imageWidth)
    {
        float radius = 3f;
        Collider2D[] allObjects = Physics2D.OverlapCircleAll(origin, radius);
        if (allObjects.Length == 0)
        {
            return;
        }
        RunImageBreakage(allObjects, origin, imageWidth, fractureImages);
    }

    public static void AntiAliasTest(breakableObject[] objects, RenderTexture allObjectsRenderTexture, int[] objectIndices)
    {
        Vector2Int[] spriteBounds = new Vector2Int[objectIndices.Length];
        int longestWidth = 0;
        int longestHeight = 0;
        for (int i = 0; i < objectIndices.Length; i++)
        {
            spriteBounds[i] = new Vector2Int(objects[objectIndices[i]].texWidth, objects[objectIndices[i]].texHeight);
            if (objects[objectIndices[i]].texWidth > longestWidth)
            {
                longestWidth = objects[objectIndices[i]].texWidth;
            }
            if (objects[objectIndices[i]].texHeight > longestHeight)
            {
                longestHeight = objects[objectIndices[i]].texHeight;
            }
        }
        /*RenderTexture allObjectsRenderTexture = new RenderTexture(longestWidth, longestHeight, 0, RenderTextureFormat.ARGB32);
        allObjectsRenderTexture.enableRandomWrite = true;
        allObjectsRenderTexture.filterMode = FilterMode.Point;
        allObjectsRenderTexture.volumeDepth = objects.Length;
        allObjectsRenderTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        allObjectsRenderTexture.Create();
        for (int i = 0; i < objects.Length; i++)
        {
            //we use the Graphics.CopyTexture() method as it runs on the gpu, and we are able to write to the gpu only render texture, initializing the array with speed, instead of having to run this in the compute shader, and pass in a huge texture to initialize the array.
            Graphics.CopyTexture(objects[i].spriteTx, 0, 0, 0, 0, objects[i].texWidth, objects[i].texHeight, allObjectsRenderTexture, i, 0, 0, 0);
        }*/
        voronoiFractureShader.SetInt("totalObjects", objectIndices.Length);
        ComputeBuffer spriteBoundsBuffer = new ComputeBuffer(spriteBounds.Length, 4 * 2);
        ComputeBuffer indicesBuffer = new ComputeBuffer(objectIndices.Length, 4 * 1);
        spriteBoundsBuffer.SetData(spriteBounds);
        indicesBuffer.SetData(objectIndices);
        voronoiFractureShader.SetBuffer(kiAntiAliasKernel, "spriteBounds", spriteBoundsBuffer);
        voronoiFractureShader.SetBuffer(kiAntiAliasKernel, "indicesBuffer", indicesBuffer);
        voronoiFractureShader.SetTexture(kiAntiAliasKernel, "outputTex", allObjectsRenderTexture);
        
        voronoiFractureShader.Dispatch(kiAntiAliasKernel, Mathf.CeilToInt(objects.Length / 8f), Mathf.CeilToInt(longestWidth/ 8f), Mathf.CeilToInt(longestHeight / 8f));
        
        AsyncGPUReadback.Request(allObjectsRenderTexture, 0, 0, longestWidth, 0, longestHeight, 0, allObjectsRenderTexture.volumeDepth,
            new Action<AsyncGPUReadbackRequest>
            (
                (AsyncGPUReadbackRequest request) =>
                {
                    if (!request.hasError)
                    {
                        double start = Time.realtimeSinceStartup;
                        // Copy the data
                        float totalTime = 0f;
                        for (var i = 0; i < objects.Length; i++)
                        {
                                Graphics.CopyTexture(allObjectsRenderTexture, i, 0, 0, 0, objects[i].texWidth, objects[i].texHeight, objects[i].spriteTx, 0, 0, 0, 0);
                                float startTime = Time.realtimeSinceStartup;
                                /*objects[i].pixelData = request.GetData<Color32>(i).ToArray();
                                objects[i].pixelDataTextureWidth = longestWidth;
                                totalTime += (Time.realtimeSinceStartup - startTime);*/
                                objects[i].antiAliased = true;
                        }
                        //AntiAliasTest(scripts);
                        allObjectsRenderTexture.Release();
                    }
                }
            ));
        spriteBoundsBuffer.Release();
        indicesBuffer.Release();
        
    }
    

    private static void RunImageBreakage(Collider2D[] allObjects, Vector2 origin, float scale, Texture2D[] fractureImages)
    {
        int[] isBreakable = new int[allObjects.Length];
        breakableObject[] allScripts = new breakableObject[allObjects.Length];
        //This will make sure that we do not draw lines outside the square centered on the origin with side length of blastRadius * 2
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
        if (totalBreakable == 0)
        {
            return;
        }
        print("totalBreakable: " + totalBreakable);
        //this sector gives us all the data we require to run this, and only gets data from the objects which are destructible.
        breakableObject[] scripts = new breakableObject[totalBreakable];
        int currentIndex = 0;
        Vector2[] originPixelPositions = new Vector2[totalBreakable];
        Vector2Int[] spriteBounds = new Vector2Int[totalBreakable];
        Vector2[] ppusAndResFactors = new Vector2[totalBreakable];
        Vector2[] objectScales = new Vector2[totalBreakable];
        int longestWidth = 0;
        int longestHeight = 0;
        Vector3[] objectTransforms = new Vector3[totalBreakable];
        for (int i = 0; i < allScripts.Length; i++)
        {
            if (isBreakable[i] == 1)
            {
                Transform t = allObjects[i].gameObject.transform;
                objectScales[currentIndex] = new Vector2(t.localScale.x, t.localScale.y);
                objectTransforms[currentIndex] = new Vector3(t.position.x, t.position.y, t.rotation.eulerAngles.z * Mathf.Deg2Rad);
                ppusAndResFactors[currentIndex] = new Vector2(allScripts[i].ppu, allScripts[i].resolutionFactorDecimal);
                Vector2 diffInOrigin = origin - new Vector2(t.position.x, t.position.y);
                float angleOrigin = Mathf.Atan2(diffInOrigin.y, diffInOrigin.x) - (t.rotation.eulerAngles.z * Mathf.Deg2Rad);
                float magOrigin = Mathf.Sqrt(Mathf.Pow(diffInOrigin.x, 2) + Mathf.Pow(diffInOrigin.y, 2));
                diffInOrigin = new Vector2(magOrigin * Mathf.Cos(angleOrigin) * (1/t.localScale.x), magOrigin * Mathf.Sin(angleOrigin) * (1/t.localScale.y));
                Vector2 pixelPosOrigin = diffInOrigin * allScripts[i].ppu;
                pixelPosOrigin = new Vector2(pixelPosOrigin.x + allScripts[i].texWidth/2f, pixelPosOrigin.y + allScripts[i].texHeight/2f);
                scripts[currentIndex] = allScripts[i];
                print("pixelPosOrigin: " + pixelPosOrigin);
                print(fractureImages[0].width + " width " + fractureImages[0].height + " height" );
                originPixelPositions[currentIndex] = pixelPosOrigin;
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
        RenderTexture allObjectsRenderTexture = new RenderTexture(longestWidth, longestHeight, 0, RenderTextureFormat.ARGB32);
        allObjectsRenderTexture.enableRandomWrite = true;
        allObjectsRenderTexture.filterMode = FilterMode.Point;
        allObjectsRenderTexture.volumeDepth = totalBreakable;
        allObjectsRenderTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        allObjectsRenderTexture.Create();
        RenderTexture hasBeenFilledTex = new RenderTexture(scripts.Length, 1, 0, RenderTextureFormat.R16); //we use a render texture to save 16 bit integer data to avoid calling ComputeBuffer.GetData(), as the get data call can take upwards of 5 ms.
        hasBeenFilledTex.enableRandomWrite = true;
        hasBeenFilledTex.filterMode = FilterMode.Point;
        hasBeenFilledTex.dimension = TextureDimension.Tex2D;
        hasBeenFilledTex.Create();
        RenderTexture fractureImage = new RenderTexture(fractureImages[0].width, fractureImages[0].height, 0,
            RenderTextureFormat.ARGB32);
        fractureImage.enableRandomWrite = true;
        fractureImage.filterMode = fractureImages[0].filterMode;
        fractureImage.dimension = TextureDimension.Tex2D;
        fractureImage.Create();
        Graphics.CopyTexture(fractureImages[0], 0, 0, 0, 0, fractureImages[0].width, fractureImages[0].height, fractureImage, 0, 0, 0, 0);
        for (int i = 0; i < totalBreakable; i++)
        {
            //we use the Graphics.CopyTexture() method as it runs on the gpu, and we are able to write to the gpu only render texture, initializing the array with speed, instead of having to run this in the compute shader, and pass in a huge texture to initialize the array.
            Graphics.CopyTexture(scripts[i].spriteTx, 0, 0, 0, 0, scripts[i].texWidth, scripts[i].texHeight, allObjectsRenderTexture, i, 0, 0, 0);
        }

        ComputeBuffer originPixelPositionsBuffer = new ComputeBuffer(originPixelPositions.Length, 4 * 2);
        ComputeBuffer spriteBoundsBuffer = new ComputeBuffer(spriteBounds.Length, 4 * 2);
        ComputeBuffer transformBuffer = new ComputeBuffer(objectTransforms.Length, 4 * 3);
        ComputeBuffer ppuAndResFactorBuffer = new ComputeBuffer(ppusAndResFactors.Length, 4 * 2);
        ComputeBuffer scaleBuffer = new ComputeBuffer(objectScales.Length, 4 * 2);
        transformBuffer.SetData(objectTransforms);
        ppuAndResFactorBuffer.SetData(ppusAndResFactors);
        originPixelPositionsBuffer.SetData(originPixelPositions);
        spriteBoundsBuffer.SetData(spriteBounds);
        scaleBuffer.SetData(objectScales);
        
        voronoiFractureShader.SetInt("totalObjects", totalBreakable);
        voronoiFractureShader.SetInt("fractureImageHeight", fractureImages[0].height);
        voronoiFractureShader.SetInt("fractureImageWidth", fractureImages[0].width);
        voronoiFractureShader.SetInt("numberDrawn", 0);
        voronoiFractureShader.SetFloat("imageWidth", scale);
        
        voronoiFractureShader.SetBuffer(kiImageFractureKernel, "transforms", transformBuffer);
        voronoiFractureShader.SetBuffer(kiImageFractureKernel, "spriteBounds", spriteBoundsBuffer);
        voronoiFractureShader.SetBuffer(kiImageFractureKernel, "originPixelPositions", originPixelPositionsBuffer);
        voronoiFractureShader.SetBuffer(kiImageFractureKernel, "ppusAndResolutionFactors", ppuAndResFactorBuffer);
        voronoiFractureShader.SetBuffer(kiImageFractureKernel, "scales", scaleBuffer);
        
        voronoiFractureShader.SetTexture(kiImageFractureKernel, "outputTex", allObjectsRenderTexture);
        voronoiFractureShader.SetTexture(kiImageFractureKernel, "hasBeenDrawnTex", hasBeenFilledTex);
        voronoiFractureShader.SetTexture(kiImageFractureKernel, "fractureImage", fractureImage);
        
        voronoiFractureShader.Dispatch(kiImageFractureKernel, Mathf.CeilToInt(totalBreakable / 8f), Mathf.CeilToInt((fractureImages[0].width * 2f)/ 8f), Mathf.CeilToInt((fractureImages[0].height * 2f) / 8f));
        //data readback, done async
        AsyncGPUReadback.Request(hasBeenFilledTex, 0, 0, scripts.Length, 0, 1, 0, hasBeenFilledTex.volumeDepth,
           new Action<AsyncGPUReadbackRequest>
           (
               (AsyncGPUReadbackRequest hasBeenFilledRequest) =>
               {
                   if (!hasBeenFilledRequest.hasError)
                   {
                       Int16[] data = hasBeenFilledRequest.GetData<Int16>().ToArray();
                       AsyncGPUReadback.Request(allObjectsRenderTexture, 0, 0, longestWidth, 0, longestHeight, 0, allObjectsRenderTexture.volumeDepth,
                           new Action<AsyncGPUReadbackRequest>
                           (
                               (AsyncGPUReadbackRequest request) =>
                               {
                                   if (!request.hasError)
                                   {
                                       ProcessImages(data, totalBreakable, scripts, allObjectsRenderTexture, request,
                                           longestWidth);
                                   }
                               }
                           ));
                       hasBeenFilledTex.Release();
                   }
               }
           ));
        
        originPixelPositionsBuffer.Release();
        spriteBoundsBuffer.Release();
        fractureImage.Release();
        transformBuffer.Release();
        ppuAndResFactorBuffer.Release();
        scaleBuffer.Release();
    }

    private static void RunCircularBreakage(Collider2D[] allObjects, Vector2 origin, float blastRadius, float probabilityFalloff)
    {
        int[] isBreakable = new int[allObjects.Length];
        breakableObject[] allScripts = new breakableObject[allObjects.Length];
        //This will make sure that we do not draw lines outside the square centered on the origin with side length of blastRadius * 2
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
        if (totalBreakable == 0)
        {
            return;
        }
        //this sector gives us all the data we require to run this, and only gets data from the objects which are destructible.
        breakableObject[] scripts = new breakableObject[totalBreakable];
        int currentIndex = 0;
        Vector3[] objectTransforms = new Vector3[totalBreakable];
        Vector2[] objectScales = new Vector2[totalBreakable];
        Vector2[] ppusAndResFactors = new Vector2[totalBreakable];
        Vector2Int[] spriteBounds = new Vector2Int[totalBreakable];
        int longestWidth = 0;
        int longestHeight = 0;
        float largestPPU = 0;
        for (int i = 0; i < allScripts.Length; i++)
        {
            if (isBreakable[i] == 1)
            {
                Transform t = allObjects[i].gameObject.transform;
                objectTransforms[currentIndex] = new Vector3(t.position.x, t.position.y, t.rotation.eulerAngles.z * Mathf.Deg2Rad);
                objectScales[currentIndex] = new Vector2(t.localScale.x, t.localScale.y);
                scripts[currentIndex] = allScripts[i];
                ppusAndResFactors[currentIndex] = new Vector2(allScripts[i].ppu, allScripts[i].resolutionFactorDecimal);
                spriteBounds[currentIndex] = new Vector2Int(allScripts[i].texWidth, allScripts[i].texHeight);
                if (allScripts[i].texWidth > longestWidth)
                {
                    longestWidth = allScripts[i].texWidth;
                }
                if (allScripts[i].texHeight > longestHeight)
                {
                    longestHeight = allScripts[i].texHeight;
                }
                if (allScripts[i].ppu > largestPPU)
                {
                    largestPPU = allScripts[i].ppu;
                }
                currentIndex++;
            }
        }
        //3D Render texture saves all sprites as one texture -- soooo free.
        //render texture initiation - this one saves the textures and then we apply the changes on the gpu - each texture has whitespace, if it's smaller than the largest texture we are applying this to. This may create some overhead, but it is necessary to store every texture efficiently and pass it to the gpu.
        RenderTexture allObjectsRenderTexture = new RenderTexture(longestWidth, longestHeight, 0, RenderTextureFormat.ARGB32);
        allObjectsRenderTexture.enableRandomWrite = true;
        allObjectsRenderTexture.filterMode = FilterMode.Point;
        allObjectsRenderTexture.volumeDepth = totalBreakable;
        allObjectsRenderTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        allObjectsRenderTexture.Create();
        RenderTexture hasBeenFilledTex = new RenderTexture(scripts.Length, 1, 0, RenderTextureFormat.R16); //we use a render texture to save 16 bit integer data to avoid calling ComputeBuffer.GetData(), as the get data call can take upwards of 5 ms.
        hasBeenFilledTex.enableRandomWrite = true;
        hasBeenFilledTex.filterMode = FilterMode.Point;
        hasBeenFilledTex.dimension = TextureDimension.Tex2D;
        hasBeenFilledTex.Create();
        for (int i = 0; i < totalBreakable; i++)
        {
            //we use the Graphics.CopyTexture() method as it runs on the gpu, and we are able to write to the gpu only render texture, initializing the array with speed, instead of having to run this in the compute shader, and pass in a huge texture to initialize the array.
            Graphics.CopyTexture(scripts[i].spriteTx, 0, 0, 0, 0, scripts[i].texWidth, scripts[i].texHeight, allObjectsRenderTexture, i, 0, 0, 0);
        }
        
        ComputeBuffer transformBuffer = new ComputeBuffer(objectTransforms.Length, 4 * 3);
        ComputeBuffer ppuAndResFactorBuffer = new ComputeBuffer(ppusAndResFactors.Length, 4 * 2);
        ComputeBuffer spriteBoundsBuffer = new ComputeBuffer(spriteBounds.Length, 4 * 2);
        ComputeBuffer scaleBuffer = new ComputeBuffer(objectScales.Length, 4 * 2);
        transformBuffer.SetData(objectTransforms);
        spriteBoundsBuffer.SetData(spriteBounds);
        ppuAndResFactorBuffer.SetData(ppusAndResFactors);
        scaleBuffer.SetData(objectScales);
        
        voronoiFractureShader.SetInt("totalObjects", totalBreakable);
        voronoiFractureShader.SetFloat("originX", origin.x);
        voronoiFractureShader.SetFloat("originY", origin.y);
        voronoiFractureShader.SetFloat("blastRadius", blastRadius);
        voronoiFractureShader.SetFloat("probabilityFalloff", probabilityFalloff);
        
        voronoiFractureShader.SetBuffer(kiCircularFractureKernel, "transforms", transformBuffer);
        voronoiFractureShader.SetBuffer(kiCircularFractureKernel, "scales", scaleBuffer);
        voronoiFractureShader.SetBuffer(kiCircularFractureKernel, "ppusAndResolutionFactors", ppuAndResFactorBuffer);
        voronoiFractureShader.SetBuffer(kiCircularFractureKernel, "spriteBounds", spriteBoundsBuffer);
        
        voronoiFractureShader.SetTexture(kiCircularFractureKernel, "outputTex", allObjectsRenderTexture);
        voronoiFractureShader.SetTexture(kiCircularFractureKernel, "hasBeenDrawnTex", hasBeenFilledTex);
        voronoiFractureShader.Dispatch(kiCircularFractureKernel, Mathf.CeilToInt(totalBreakable / 8f), Mathf.CeilToInt((blastRadius * largestPPU) / 8f), 1);
        
        //data readback, done async
        AsyncGPUReadback.Request(hasBeenFilledTex, 0, 0, scripts.Length, 0, 1, 0, hasBeenFilledTex.volumeDepth,
           new Action<AsyncGPUReadbackRequest>
           (
               (AsyncGPUReadbackRequest hasBeenFilledRequest) =>
               {
                   if (!hasBeenFilledRequest.hasError)
                   {
                       Int16[] data = hasBeenFilledRequest.GetData<Int16>().ToArray();
                       AsyncGPUReadback.Request(allObjectsRenderTexture, 0, 0, longestWidth, 0, longestHeight, 0, allObjectsRenderTexture.volumeDepth,
                           new Action<AsyncGPUReadbackRequest>
                           (
                               (AsyncGPUReadbackRequest request) =>
                               {
                                   if (!request.hasError)
                                   {
                                       ProcessImages(data, totalBreakable, scripts, allObjectsRenderTexture, request,
                                           longestWidth);
                                   }
                               }
                           ));
                       hasBeenFilledTex.Release();
                   }
               }
           ));
        
        transformBuffer.Release();
        ppuAndResFactorBuffer.Release();
        spriteBoundsBuffer.Release();
        scaleBuffer.Release();
    }

    private static void RunVoronoiBreakage(Collider2D[] allObjects, Vector2 origin, float blastRadius, float force,
        float density, float shatterCount, int lloydRelaxation)
    {
        //this runs a delauney triangulation based voronoi algorithm based on the input parameters
        int[] isBreakable = new int[allObjects.Length];
        breakableObject[] allScripts = new breakableObject[allObjects.Length];
        List<Vector2f> points = CreateVoronoiPoints(shatterCount, origin.x, origin.y, force, density);
        //This will make sure that we do not draw lines outside the square centered on the origin with side length of blastRadius * 2
        Rectf bounds = new Rectf(origin.x - blastRadius,origin.y-blastRadius,2 * blastRadius, 2 * blastRadius); //TODO: not actually a circular blast radius :skull:
        //voronoi is generated with points and bounds. Lloyd Relaxation may be applied by the user to make the points more spread out and create more even sized polygons.
        Voronoi voronoi = new Voronoi(points,bounds, lloydRelaxation);
        List<Edge> edges = voronoi.Edges;
        List<Vector2> allLeftEdgeEnds = new List<Vector2>();
        List<Vector2> allRightEdgeEnds = new List<Vector2>();
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
        if (totalBreakable == 0)
        {
            return;
        }
        
        //this sector gives us all the data we require to run this, and only gets data from the objects which are destructible.
        breakableObject[] scripts = new breakableObject[totalBreakable];
        int currentIndex = 0;
        Vector3[] objectTransforms = new Vector3[totalBreakable];
        Vector2[] objectScales = new Vector2[totalBreakable];
        Vector2[] ppusAndResFactors = new Vector2[totalBreakable];
        Vector2Int[] spriteBounds = new Vector2Int[totalBreakable];
        int longestWidth = 0;
        int longestHeight = 0;
        for (int i = 0; i < allScripts.Length; i++)
        {
            if (isBreakable[i] == 1)
            {
                Transform t = allObjects[i].gameObject.transform;
                objectTransforms[currentIndex] = new Vector3(t.position.x, t.position.y, t.rotation.eulerAngles.z * Mathf.Deg2Rad);
                objectScales[currentIndex] = new Vector2(t.localScale.x, t.localScale.y);
                scripts[currentIndex] = allScripts[i];
                ppusAndResFactors[currentIndex] = new Vector2(allScripts[i].ppu, allScripts[i].resolutionFactorDecimal);
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

        //render texture initiation - this one saves the textures and then we apply the changes on the gpu - each texture has whitespace, if it's smaller than the largest texture we are applying this to. This may create some overhead, but it is necessary to store every texture efficiently and pass it to the gpu.
        RenderTexture allObjectsRenderTexture = new RenderTexture(longestWidth, longestHeight, 0, RenderTextureFormat.ARGB32);
        allObjectsRenderTexture.enableRandomWrite = true;
        allObjectsRenderTexture.filterMode = FilterMode.Point;
        allObjectsRenderTexture.volumeDepth = totalBreakable;
        allObjectsRenderTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
        allObjectsRenderTexture.Create();
        RenderTexture hasBeenFilledTex = new RenderTexture(scripts.Length, 1, 0, RenderTextureFormat.R16); //we use a render texture to save 16 bit integer data to avoid calling ComputeBuffer.GetData(), as the get data call can take upwards of 5 ms.
        hasBeenFilledTex.enableRandomWrite = true;
        hasBeenFilledTex.filterMode = FilterMode.Point;
        hasBeenFilledTex.dimension = TextureDimension.Tex2D;
        hasBeenFilledTex.Create();
        for (int i = 0; i < totalBreakable; i++)
        {
            //we use the Graphics.CopyTexture() method as it runs on the gpu, and we are able to write to the gpu only render texture, initializing the array with speed, instead of having to run this in the compute shader, and pass in a huge texture to initialize the array.
            Graphics.CopyTexture(scripts[i].preAliasedTx, 0, 0, 0, 0, scripts[i].texWidth, scripts[i].texHeight, allObjectsRenderTexture, i, 0, 0, 0);
        }
        
        //compute buffer initiation
        ComputeBuffer leftEndsBuffer = new ComputeBuffer(allLeftEdgeEnds.Count, 4 * 2);
        ComputeBuffer rightEndsBuffer = new ComputeBuffer(allRightEdgeEnds.Count, 4 * 2);
        ComputeBuffer transformBuffer = new ComputeBuffer(objectTransforms.Length, 4 * 3);
        ComputeBuffer ppuAndResFactorBuffer = new ComputeBuffer(ppusAndResFactors.Length, 4 * 2);
        ComputeBuffer spriteBoundsBuffer = new ComputeBuffer(spriteBounds.Length, 4 * 2);
        ComputeBuffer scaleBuffer = new ComputeBuffer(objectScales.Length, 4 * 2);
        leftEndsBuffer.SetData(allLeftEdgeEnds.ToArray());
        rightEndsBuffer.SetData(allRightEdgeEnds.ToArray());
        transformBuffer.SetData(objectTransforms);
        spriteBoundsBuffer.SetData(spriteBounds);
        ppuAndResFactorBuffer.SetData(ppusAndResFactors);
        scaleBuffer.SetData(objectScales);
        
        //compute shader setup
        voronoiFractureShader.SetInt("totalObjects", totalBreakable);
        voronoiFractureShader.SetInt("totalEdges", allLeftEdgeEnds.Count);
        voronoiFractureShader.SetInt("longestHeight", longestHeight);
        voronoiFractureShader.SetInt("longestWidth", longestWidth);
        
        voronoiFractureShader.SetBuffer(kiVoronoiFractureKernel, "leftEdgeEnds", leftEndsBuffer);
        voronoiFractureShader.SetBuffer(kiVoronoiFractureKernel, "rightEdgeEnds", rightEndsBuffer);
        voronoiFractureShader.SetBuffer(kiVoronoiFractureKernel, "transforms", transformBuffer);
        voronoiFractureShader.SetBuffer(kiVoronoiFractureKernel, "scales", scaleBuffer);
        voronoiFractureShader.SetBuffer(kiVoronoiFractureKernel, "ppusAndResolutionFactors", ppuAndResFactorBuffer);
        voronoiFractureShader.SetBuffer(kiVoronoiFractureKernel, "spriteBounds", spriteBoundsBuffer);
        
        voronoiFractureShader.SetTexture(kiVoronoiFractureKernel, "outputTex", allObjectsRenderTexture);
        voronoiFractureShader.SetTexture(kiVoronoiFractureKernel, "hasBeenDrawnTex", hasBeenFilledTex);
        
        voronoiFractureShader.Dispatch(kiVoronoiFractureKernel, Mathf.CeilToInt(totalBreakable / 8f), Mathf.CeilToInt(allLeftEdgeEnds.Count / 8f), 1);
        
        //data readback, done async
        AsyncGPUReadback.Request(hasBeenFilledTex, 0, 0, scripts.Length, 0, 1, 0, hasBeenFilledTex.volumeDepth,
           new Action<AsyncGPUReadbackRequest>
           (
               (AsyncGPUReadbackRequest hasBeenFilledRequest) =>
               {
                   if (!hasBeenFilledRequest.hasError)
                   {
                       Int16[] data = hasBeenFilledRequest.GetData<Int16>().ToArray();
                       AsyncGPUReadback.Request(allObjectsRenderTexture, 0, 0, longestWidth, 0, longestHeight, 0, allObjectsRenderTexture.volumeDepth,
                           new Action<AsyncGPUReadbackRequest>
                           (
                               (AsyncGPUReadbackRequest request) =>
                               {
                                   if (!request.hasError)
                                   {
                                       ProcessImages(data, totalBreakable, scripts, allObjectsRenderTexture, request,
                                           longestWidth);
                                   }
                               }
                           ));
                       hasBeenFilledTex.Release();
                   }
               }
           ));
        
        leftEndsBuffer.Release();
        rightEndsBuffer.Release();
        transformBuffer.Release();
        ppuAndResFactorBuffer.Release();
        spriteBoundsBuffer.Release();
        scaleBuffer.Release();
    }

    private static void ProcessImages(Int16[] data, int totalBreakable, breakableObject[] scripts, RenderTexture allObjectsRenderTexture, AsyncGPUReadbackRequest request, int longestWidth)
    {
        double start = Time.realtimeSinceStartup;
       // Copy the data
       float totalTime = 0f;
       bool anyAntiAliased = false;
       List<int> objectIndices = new List<int>();
       for (var i = 0; i < totalBreakable; i++)
       {
           if (data[i] == 1)
           {
               if (!scripts[i].doAntiAliasing)
               {
                   Graphics.CopyTexture(allObjectsRenderTexture, i, 0, 0, 0, scripts[i].texWidth, scripts[i].texHeight, scripts[i].preAliasedTx, 0, 0, 0, 0);
                   scripts[i].spriteTx = scripts[i].preAliasedTx;
               }
               else
               {
                   Graphics.CopyTexture(allObjectsRenderTexture, i, 0, 0, 0, scripts[i].texWidth, scripts[i].texHeight, scripts[i].preAliasedTx, 0, 0, 0, 0);
                   objectIndices.Add(i);
                   anyAntiAliased = true;
               } 
               float startTime = Time.realtimeSinceStartup;
               scripts[i].pixelData = request.GetData<Color32>(i).ToArray();
               scripts[i].pixelDataTextureWidth = longestWidth;
               //scripts[i].spriteTx.SetPixels32(scripts[i].pixelData);
               //scripts[i].spriteTx.Apply();
               totalTime += (Time.realtimeSinceStartup - startTime);
               scripts[i].dirty = true;
           }
       }
       //AntiAliasTest(scripts);
       if (anyAntiAliased)
       {
           AntiAliasTest(scripts, allObjectsRenderTexture, objectIndices.ToArray());
           //allObjectsRenderTexture.Release();
       }
       else
       {
           allObjectsRenderTexture.Release();
       }
    }

    //creates a set of points that are weighted around a center at a certain scale and weight to create variation in the voronoi points.
    private static List<Vector2f> CreateVoronoiPoints(float polygonNumber, float aroundX, float aroundY, float scale, float density) {
        // Use Vector2f, instead of Vector2
        // Vector2f is pretty much the same than Vector2, but you can run Voronoi in another thread, though this isn't used in this implementation
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
    
    //creates a random point with a weight around the origin. Higher number density = more points generated nearer to the center point in the long run
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
