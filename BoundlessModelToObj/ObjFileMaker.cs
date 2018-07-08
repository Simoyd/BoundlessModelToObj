using BoundlessMsgPackToJson;
using FastBitmapLib;
using Imaging.DDSReader;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace BoundlessModelToObj
{
    public class ObjFileMaker
    {
        public static void DoMakeObjFile(string outputDir, string inputFile, string gradient, string emissive)
        {
            byte[] gradientColors = gradient.Split(new char[] { '[', ']', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(cur => byte.Parse(cur.Trim())).ToArray();

            SColor[] gradientObj = new SColor[] {
                new SColor { R = gradientColors[0], B = gradientColors[1], G = gradientColors[2] },
                new SColor { R = gradientColors[3], B = gradientColors[4], G = gradientColors[5] },
                new SColor { R = gradientColors[6], B = gradientColors[7], G = gradientColors[8] },
            };

            byte[] emissiveColors = emissive.Split(new char[] { '[', ']', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(cur => byte.Parse(cur.Trim())).ToArray();

            SColor emissiveObj = new SColor { R = emissiveColors[0], B = emissiveColors[1], G = emissiveColors[2] };

            string inputOrg = inputFile;

            bool isJson = inputFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
            bool isMsgpack = inputFile.EndsWith(".msgpack", StringComparison.OrdinalIgnoreCase);

            if (!File.Exists(inputFile) || (!isMsgpack && !isJson))
            {
                throw new Exception("Input file must be .msgpack or .json");
            }

            if (!Directory.Exists(outputDir))
            {
                throw new Exception("Output directory does not exist!");
            }

            if (isMsgpack)
            {
                Parser.DoParse(outputDir, inputFile);
                inputFile = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputFile)}.json");
            }

            string materialName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(inputFile));
            string materialFile = $"{materialName}.mtl";
            string materialFilePath = Path.Combine(outputDir, materialFile);

            JObject root = JObject.Parse(File.ReadAllText(inputFile));

            Dictionary<string, Dictionary<string, string>> materialLookup = new Dictionary<string, Dictionary<string, string>>();

            foreach (var curNode in root["nodes"].Value<JObject>())
            {
                if (!curNode.Value.Value<JObject>().ContainsKey("geometryinstances"))
                {
                    continue;
                }

                foreach (var curGeom in curNode.Value["geometryinstances"].Value<JObject>())
                {
                    string geometry = curGeom.Value["geometry"].Value<string>();
                    string surface = curGeom.Value["surface"].Value<string>();
                    string material = curGeom.Value["material"].Value<string>();

                    if (!materialLookup.TryGetValue(geometry, out Dictionary<string, string> curGeomLookup))
                    {
                        curGeomLookup = new Dictionary<string, string>();
                        materialLookup.Add(geometry, curGeomLookup);
                    }

                    curGeomLookup[surface] = material;
                }
            }

            bool failMat = false;

            string outputFile = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputFile)}.obj");
            using (StreamWriter sw = new StreamWriter(outputFile))
            {
                sw.WriteLine($"mtllib {materialFile}");

                int vOffset = 0;
                int vtOffset = 0;
                int vnOffset = 0;

                foreach (var curGeom in root["geometries"].Value<JObject>())
                {
                    // TODO: handle TANGENT and BINORMAL. Anymore?

                    //if (curGeom.Value["inputs"].Count() != 3)
                    //{
                    //    throw new Exception("Invalid source input count...");
                    //}

                    double[][] vertLocs = ReadShit<double>(curGeom.Value, "POSITION", out int vertOffset);
                    foreach (double[] curVert in vertLocs)
                    {
                        sw.WriteLine($"v {curVert[0]} {curVert[1]} {curVert[2]}");
                    }

                    double[][] texCoords = ReadShit<double>(curGeom.Value, "TEXCOORD0", out int texOffset);
                    foreach (double[] curTex in texCoords)
                    {
                        sw.WriteLine($"vt {curTex[0]} {1-curTex[1]}");
                    }

                    double[][] normals = ReadShit<double>(curGeom.Value, "NORMAL", out int normOffset);
                    foreach (double[] curNorm in normals)
                    {
                        sw.WriteLine($"vn {curNorm[0]} {curNorm[1]} {curNorm[2]}");
                    }

                    foreach (var curSurf in curGeom.Value["surfaces"].Value<JObject>())
                    {
                        sw.WriteLine($"g {curSurf.Key}");

                        try
                        {
                            sw.WriteLine($"usemtl {materialLookup[curGeom.Key][curSurf.Key]}");
                        }
                        catch
                        {
                            failMat = true;
                            sw.WriteLine($"usemtl failMat");
                            Console.WriteLine("material not found...");
                        }

                        var blarg = curSurf.Value["triangles"].Select(cur => cur.Value<int>());
                        int numPrimitives = curSurf.Value["numPrimitives"].Value<int>();

                        if ((blarg.Count() % numPrimitives) != 0)
                        {
                            throw new Exception("Invalid stride");
                        }

                        int stride = blarg.Count() / numPrimitives;

                        if ((stride % 3) != 0)
                        {
                            throw new Exception("Invalid stride");
                        }

                        int pointLength = stride / 3;

                        int[][] triangles = FuckinReadIt(blarg, stride);

                        foreach (int[] curTri in triangles)
                        {
                            if (pointLength >= 1)
                            {
                                if ((curTri[pointLength * 0 + 0] >= vertLocs.Length) ||
                                    (curTri[pointLength * 1 + 0] >= vertLocs.Length) ||
                                    (curTri[pointLength * 2 + 0] >= vertLocs.Length))
                                {
                                    Console.WriteLine("vert index out of range");
                                    continue;
                                }
                            }

                            if (pointLength >= 2)
                            {
                                if ((curTri[pointLength * 0 + 1] >= texCoords.Length) ||
                                    (curTri[pointLength * 1 + 1] >= texCoords.Length) ||
                                    (curTri[pointLength * 2 + 1] >= texCoords.Length))
                                {
                                    Console.WriteLine("tex index out of range");
                                    continue;
                                }
                            }

                            if (pointLength >= 3)
                            {
                                if ((curTri[pointLength * 0 + 2] >= normals.Length) ||
                                    (curTri[pointLength * 1 + 2] >= normals.Length) ||
                                    (curTri[pointLength * 2 + 2] >= normals.Length))
                                {
                                    Console.WriteLine("norm index out of range");
                                    continue;
                                }
                            }

                            sw.Write("f");

                            for (int i = 0; i < 3; ++i)
                            {
                                sw.Write(" ");

                                if (pointLength >= 1)
                                {
                                    sw.Write($"{curTri[pointLength * i + 0] + 1 + vOffset}");
                                }

                                if (pointLength >= 2)
                                {
                                    sw.Write($"/{curTri[pointLength * i + 1] + 1 + vtOffset}");
                                }

                                if (pointLength >= 3)
                                {
                                    sw.Write($"/{curTri[pointLength * i + 2] + 1 + vnOffset}");
                                }
                            }

                            sw.WriteLine();
                        }
                    }

                    vOffset += vertLocs.Length;
                    vtOffset += texCoords.Length;
                    vnOffset += normals.Length;
                }

                sw.Flush();
                sw.Close();
                sw.Dispose();
            }

            string rootDir = inputOrg;

            while (Path.GetFileName(rootDir) != "assets")
            {
                rootDir = Path.GetDirectoryName(rootDir);
            }

            using (StreamWriter sw = new StreamWriter(materialFilePath))
            {
                foreach (var curMat in root["materials"].Value<JObject>())
                {
                    sw.WriteLine($"newmtl {curMat.Key}");

                    var material = curMat.Value.Value<JObject>();

                    int usedTextures = 0;

                    if (material["parameters"].Value<JObject>().ContainsKey("diffuse") ||
                        material["parameters"].Value<JObject>().ContainsKey("gradient_mask"))
                    {
                        string diffuse;
                        FastBitmap diffuseTexture;

                        if (material["parameters"].Value<JObject>().ContainsKey("diffuse"))
                        {
                            ++usedTextures;
                            diffuse = material["parameters"]["diffuse"].Value<string>().Replace('/', '\\');

                            try
                            {
                                diffuseTexture = DDS.LoadImage(Path.Combine(rootDir, diffuse)).FastLock();
                            }
                            catch
                            {
                                diffuseTexture = null;
                                Console.WriteLine("Error loading texture");
                            }
                        }
                        else
                        {
                            diffuse = material["parameters"]["gradient_mask"].Value<string>().Replace('/', '\\');
                            diffuseTexture = null;
                        }

                        if (material["parameters"].Value<JObject>().ContainsKey("gradient_mask"))
                        {
                            ++usedTextures;
                            string gradient_mask = material["parameters"]["gradient_mask"].Value<string>().Replace('/', '\\');

                            FastBitmap gradient_maskTexture = null;
                            bool fail = false;

                            try
                            {
                                gradient_maskTexture = DDS.LoadImage(Path.Combine(rootDir, gradient_mask)).FastLock();
                            }
                            catch
                            {
                                fail = true;
                                Console.WriteLine("Error loading texture");
                            }

                            if (!fail)
                            {
                                if (diffuseTexture == null)
                                {
                                    diffuseTexture = new Bitmap(gradient_maskTexture.Width, gradient_maskTexture.Height, PixelFormat.Format32bppArgb).FastLock();
                                }

                                ApplyGradientToDiffuse(diffuseTexture, gradient_maskTexture, gradientObj);

                                gradient_maskTexture.Unlock();
                                gradient_maskTexture.Bitmap.Dispose();
                                gradient_maskTexture.Dispose();
                            }
                        }

                        if (diffuseTexture == null)
                        {
                            diffuseTexture = new Bitmap(512, 512, PixelFormat.Format32bppArgb).FastLock();
                            SetFlatColor(diffuseTexture, new SColor { R = 255, G = 0, B = 255 });
                        }

                        diffuseTexture.Unlock();

                        string saveFilePath = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(diffuse)}.png");
                        sw.WriteLine($"map_Kd {Path.GetFileName(saveFilePath)}");

                        try
                        {
                            diffuseTexture.Bitmap.Save(saveFilePath, ImageFormat.Png);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to save image {saveFilePath}\r\n{ex.Message}");
                        }

                        diffuseTexture.Bitmap.Dispose();
                        diffuseTexture.Dispose();
                    }

                    if (material["parameters"].Value<JObject>().ContainsKey("normal"))
                    {
                        ++usedTextures;
                        string normal = material["parameters"]["normal"].Value<string>().Replace('/', '\\');

                        FastBitmap normalTexture = null;
                        bool fail = false;

                        try
                        {
                            normalTexture = DDS.LoadImage(Path.Combine(rootDir, normal)).FastLock();
                        }
                        catch
                        {
                            fail = true;
                            Console.WriteLine("Error loading texture");
                        }

                        if (!fail)
                        {
                            normalTexture.Unlock();

                            string saveFilePath = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(normal)}.png");
                            sw.WriteLine($"map_Bump {Path.GetFileName(saveFilePath)}");

                            try
                            {
                                normalTexture.Bitmap.Save(saveFilePath, ImageFormat.Png);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to save image {saveFilePath}\r\n{ex.Message}");
                            }

                            normalTexture.Bitmap.Dispose();
                            normalTexture.Dispose();
                        }
                    }

                    if (material["parameters"].Value<JObject>().ContainsKey("specular_emissive"))
                    {
                        ++usedTextures;
                        string specular_emissive = material["parameters"]["specular_emissive"].Value<string>().Replace('/', '\\');

                        FastBitmap specular_emissiveTexture = null;

                        bool fail = false;
                        try
                        {
                            specular_emissiveTexture = DDS.LoadImage(Path.Combine(rootDir, specular_emissive)).FastLock();
                        }
                        catch
                        {
                            fail = true;
                            Console.WriteLine("Error loading texture");
                        }

                        if (!fail)
                        {
                            ApplyEmissiveColor(specular_emissiveTexture, emissiveObj);

                            specular_emissiveTexture.Unlock();

                            string saveFilePath = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(specular_emissive)}.png");
                            sw.WriteLine($"map_Ke {Path.GetFileName(saveFilePath)}");

                            try
                            {
                                specular_emissiveTexture.Bitmap.Save(saveFilePath, ImageFormat.Png);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to save image {saveFilePath}\r\n{ex.Message}");
                            }

                            specular_emissiveTexture.Bitmap.Dispose();
                            specular_emissiveTexture.Dispose();
                        }
                    }

                    if (material["parameters"].Value<JObject>().Count != usedTextures)
                    {
                        Console.WriteLine($"Warning, unused texture(s) on {curMat.Key}...");
                    }
                }

                if (failMat)
                {
                    FastBitmap failTexture = new Bitmap(512, 512, PixelFormat.Format32bppArgb).FastLock();
                    SetFlatColor(failTexture, new SColor { R = 255, G = 0, B = 255 });

                    sw.WriteLine($"newmtl failMat");

                    failTexture.Unlock();

                    string saveFilePath = Path.Combine(outputDir, $"failMat.png");
                    sw.WriteLine($"map_Kd {Path.GetFileName(saveFilePath)}");

                    try
                    {
                        failTexture.Bitmap.Save(saveFilePath, ImageFormat.Png);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to save image {saveFilePath}\r\n{ex.Message}");
                    }

                    failTexture.Bitmap.Dispose();
                    failTexture.Dispose();
                }

                sw.Flush();
                sw.Close();
                sw.Dispose();
            }

            Console.WriteLine("Done Done!!!");
        }

        private static T[][] ReadShit<T>(JToken curGeom, string key, out int offset)
        {
            var bork = curGeom["inputs"][key];

            offset = bork["offset"].Value<int>();
            key = bork["source"].Value<string>();
            JToken thinger = curGeom["sources"][key];

            int stride = thinger["stride"].Value<int>();

            var blarg = thinger["data"].Select(cur => cur.Value<T>());

            return FuckinReadIt<T>(blarg, stride);
        }

        private static T[][] FuckinReadIt<T>(IEnumerable<T> blarg, int stride)
        {
            IEnumerator<T> enumer = blarg.GetEnumerator();

            List<T[]> result = new List<T[]>();

            bool done = false;
            while (!done)
            {
                T[] curShit = new T[stride];

                for (int i = 0; i < stride; ++i)
                {
                    if (!enumer.MoveNext())
                    {
                        done = true;
                        break;
                    }

                    curShit[i] = enumer.Current;
                }

                if (!done)
                {
                    result.Add(curShit);
                }
            }

            return result.ToArray();
        }

        public unsafe static void ApplyGradientToDiffuse(FastBitmap diffuseMap, FastBitmap gradientMap, SColor[] gradient)
        {
            if (!diffuseMap.Locked)
            {
                diffuseMap.Lock();
            }

            if (!gradientMap.Locked)
            {
                gradientMap.Lock();
            }

            if ((diffuseMap.Width != gradientMap.Width) || (diffuseMap.Height != gradientMap.Height))
            {
                throw new Exception("Map dimensions don't match!");
            }

            byte[] gR = new byte[256];
            byte[] gG = new byte[256];
            byte[] gB = new byte[256];

            for (int gradientIndex = 0; gradientIndex < 256; ++gradientIndex)
            {
                byte r, g, b;

                if (gradientIndex < 128) // 0-127.5 is the lower gradient
                {
                    r = (byte)Math.Round(((double)gradientIndex / (double)127.5 * ((double)gradient[1].R - (double)gradient[0].R)) + (double)gradient[0].R);
                    g = (byte)Math.Round(((double)gradientIndex / (double)127.5 * ((double)gradient[1].G - (double)gradient[0].G)) + (double)gradient[0].G);
                    b = (byte)Math.Round(((double)gradientIndex / (double)127.5 * ((double)gradient[1].B - (double)gradient[0].B)) + (double)gradient[0].B);

                }
                else // 127.5-255 is the higher gradient
                {
                    r = (byte)Math.Round((((double)gradientIndex - (double)127.5) / (double)127.5 * ((double)gradient[2].R - (double)gradient[1].R)) + (double)gradient[1].R);
                    g = (byte)Math.Round((((double)gradientIndex - (double)127.5) / (double)127.5 * ((double)gradient[2].G - (double)gradient[1].G)) + (double)gradient[1].G);
                    b = (byte)Math.Round((((double)gradientIndex - (double)127.5) / (double)127.5 * ((double)gradient[2].B - (double)gradient[1].B)) + (double)gradient[1].B);
                }

                gR[gradientIndex] = r;
                gG[gradientIndex] = g;
                gB[gradientIndex] = b;
            }

            byte* destScan = (byte*)diffuseMap.Scan0;
            byte* sourceScan = (byte*)gradientMap.Scan0;

            for (int yOffset = 0; yOffset < diffuseMap.Height; ++yOffset)
            {
                int destPixelOffsetPart = yOffset * diffuseMap.Stride;

                for (int xOffset = 0; xOffset < diffuseMap.Width; ++xOffset)
                {
                    int sourceYStart = yOffset;
                    int sourceXStart = xOffset;

                    int pixelOffsetPart = sourceYStart * gradientMap.Stride + sourceXStart;

                    int pixelOffset = (pixelOffsetPart) * FastBitmap.BytesPerPixel;

                    int gradientIndex = sourceScan[pixelOffset + 1];

                    byte curB = gB[gradientIndex];
                    byte curG = gG[gradientIndex];
                    byte curR = gR[gradientIndex];
                    byte curA = sourceScan[pixelOffset + 3];

                    int destPixelOffset = (destPixelOffsetPart + xOffset) * FastBitmap.BytesPerPixel;

                    if (curA != 0)
                    {
                        double alpha = curA / 255.0;

                        destScan[destPixelOffset + 0] = ApplyAlpha(destScan[destPixelOffset + 0], curB, alpha);
                        destScan[destPixelOffset + 1] = ApplyAlpha(destScan[destPixelOffset + 1], curG, alpha);
                        destScan[destPixelOffset + 2] = ApplyAlpha(destScan[destPixelOffset + 2], curR, alpha);
                        destScan[destPixelOffset + 3] = Math.Max((byte)255, (byte)(destScan[destPixelOffset + 3] + (255 - destScan[destPixelOffset + 3]) * alpha));
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ApplyAlpha(byte oldValue, byte newValue, double alpha)
        {
            return (byte)(newValue * alpha + oldValue * (1 - alpha));
        }

        public unsafe static void ApplyEmissiveColor(FastBitmap emissiveMap, SColor color)
        {
            if (!emissiveMap.Locked)
            {
                emissiveMap.Lock();
            }

            byte* sourceScan = (byte*)emissiveMap.Scan0;

            for (int yOffset = 0; yOffset < emissiveMap.Height; ++yOffset)
            {
                int destPixelOffsetPart = yOffset * emissiveMap.Stride;

                for (int xOffset = 0; xOffset < emissiveMap.Width; ++xOffset)
                {
                    int sourceYStart = yOffset;
                    int sourceXStart = xOffset;

                    int pixelOffsetPart = sourceYStart * emissiveMap.Stride + sourceXStart;

                    int pixelOffset = (pixelOffsetPart) * FastBitmap.BytesPerPixel;

                    int destPixelOffset = (destPixelOffsetPart + xOffset) * FastBitmap.BytesPerPixel;

                    if (sourceScan[destPixelOffset + 3] != 0)
                    {
                        sourceScan[destPixelOffset + 0] = color.B;
                        sourceScan[destPixelOffset + 1] = color.G;
                        sourceScan[destPixelOffset + 2] = color.R;
                    }
                }
            }
        }

        public unsafe static void SetFlatColor(FastBitmap diffuseMap, SColor color)
        {
            if (!diffuseMap.Locked)
            {
                diffuseMap.Lock();
            }

            byte* sourceScan = (byte*)diffuseMap.Scan0;

            for (int yOffset = 0; yOffset < diffuseMap.Height; ++yOffset)
            {
                int destPixelOffsetPart = yOffset * diffuseMap.Stride;

                for (int xOffset = 0; xOffset < diffuseMap.Width; ++xOffset)
                {
                    int sourceYStart = yOffset;
                    int sourceXStart = xOffset;

                    int pixelOffsetPart = sourceYStart * diffuseMap.Stride + sourceXStart;

                    int pixelOffset = (pixelOffsetPart) * FastBitmap.BytesPerPixel;

                    int destPixelOffset = (destPixelOffsetPart + xOffset) * FastBitmap.BytesPerPixel;

                    sourceScan[destPixelOffset + 0] = color.B;
                    sourceScan[destPixelOffset + 1] = color.G;
                    sourceScan[destPixelOffset + 2] = color.R;
                    sourceScan[destPixelOffset + 3] = color.A;
                }
            }
        }
    }
}
