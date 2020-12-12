using Autodesk.Maya.OpenMaya;
using BabylonExport.Entities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Maya2Babylon
{
    partial class BabylonExporter
    {
        public enum M2B_FileTextureTypes
        {
            AiImage     = 0x115d17,
            MayaFile    = 0x52544654
        }

        public enum M2B_FileTextureFormats
        {
            bmp,
            gif,
            jpg,
            jpeg,
            png,
            tga
        }

        public BabylonTexture ExportTexture(MFnDependencyNode materialDependencyNode, string plugName, BabylonScene babylonScene)
        {
            var materialName = materialDependencyNode.name;
            RaiseVerbose(String.Format("Exporting texture {0}.{1}", materialName, plugName), 2);

            if (!materialDependencyNode.hasAttribute(plugName))
            {
                RaiseError(String.Format("{0} has no {1} attribute", materialName, plugName), 2);
                return null;
            }

            MFnDependencyNode textureDependencyNode = getTextureDependencyNode(materialDependencyNode, plugName);
            if (textureDependencyNode == null)
            {
                RaiseWarning(String.Format("{0} has no texture connected to attribute {1}", materialName, plugName), 2);
                return null;
            }

            string sourcePath = getSourcePathFromFileTexture(textureDependencyNode);
            if (String.IsNullOrWhiteSpace(sourcePath))
            {
                RaiseError("Texture path is missing or invalid.", 3);
                return null;
            }

            // RD TODO: convert floating point textures using oiiotool
            var imageExtension = Path.GetExtension(sourcePath).Replace(".", string.Empty);
            if (!Enum.IsDefined(typeof(M2B_FileTextureFormats), imageExtension))
            {
                RaiseError(String.Format("{0} texture format {1} is not supported and cannot be used", plugName, imageExtension));
                return null;
            }

            var id = textureDependencyNode.uuid().asString();
            var babylonTexture = new BabylonTexture(id);
            babylonTexture.name = Path.GetFileName(sourcePath).Replace(":", "_");
            babylonTexture.originalPath = sourcePath;

            // RD TODO: determine the need for the following attributes
            // babylonTexture.hasAlpha
            // babylonTexture.getAlphaFromRGB

            getTextureUVs(textureDependencyNode, babylonTexture);
            
            if (exportParameters.writeTextures)
            {
                copyTextureToOutputPath(babylonScene.OutputPath, babylonTexture);
            }

            return babylonTexture;
        }

        public BabylonTexture ExportTextureORM(MFnDependencyNode materialDependencyNode, string metalPlugName, string roughnessPlugName, BabylonScene babylonScene)
        {
                    // // Metallic & roughness
                    // MFnDependencyNode metallicTextureDependencyNode = getTextureDependencyNode(materialDependencyNode, "metalness");
                    // MFnDependencyNode roughnessTextureDependencyNode = getTextureDependencyNode(materialDependencyNode, "specularRoughness");
                    // if (metallicTextureDependencyNode != null && roughnessTextureDependencyNode != null &&
                    //     getSourcePathFromFileTexture(metallicTextureDependencyNode) == getSourcePathFromFileTexture(roughnessTextureDependencyNode))
                    // {
                    //     // If the same file is used for metallic and roughness
                    //     // Then we assume it's an ORM file (Red=Occlusion, Green=Roughness, Blue=Metallic)

                    //     // Metallic and roughness are already merged into a single file
                    //     babylonMaterial.metallicRoughnessTexture = ExportTexture(metallicTextureDependencyNode, babylonScene);

                    //     // Use same file for Ambient occlusion
                    //     babylonMaterial.occlusionTexture = babylonMaterial.metallicRoughnessTexture;
                    // }
                    // else
                    // {
                    //     // Metallic and roughness files need to be merged into a single file
                    //     // Occlusion texture is not exported since aiStandardSurface material doesn't provide input for it
                    //     babylonMaterial.metallicRoughnessTexture = ExportORMTexture(babylonScene, metallicTextureDependencyNode, roughnessTextureDependencyNode, null, babylonMaterial.metallic, babylonMaterial.roughness);
                    // }
            return new BabylonTexture("12345");
        }

        public BabylonTexture ExportTextureColorAlpha(MFnDependencyNode materialDependencyNode, string colorPlugName, string alphaPlugName, 
                                                                            float[] defaultBaseColor, float defaultOpacity, BabylonScene babylonScene)
        {
            var materialName = materialDependencyNode.name;
            RaiseVerbose(String.Format("Exporting texture color and alpha"), 2);

            if (!materialDependencyNode.hasAttribute(colorPlugName))
            {
                RaiseError(String.Format("{0} has no {1} attribute", materialName, colorPlugName), 2);
                return null;
            }
            if (!materialDependencyNode.hasAttribute(alphaPlugName))
            {
                RaiseError(String.Format("{0} has no {1} attribute", materialName, alphaPlugName), 2);
                return null;
            }

            var colorDependencyNode = getTextureDependencyNode(materialDependencyNode, colorPlugName);
            var alphaDependencyNode = getTextureDependencyNode(materialDependencyNode, alphaPlugName);
            if (colorDependencyNode == null && alphaDependencyNode == null)
            {
                RaiseWarning(String.Format("{0} has no texture connected to attribute {1} or {2} ",
                                            materialName, colorPlugName, alphaPlugName), 2);
                return null;
            }

            string colorSourcePath = null;
            if (colorDependencyNode != null)
            {
                colorSourcePath = getSourcePathFromFileTexture(colorDependencyNode);
                if (colorSourcePath != null)
                {
                    if (String.IsNullOrWhiteSpace(colorSourcePath))
                    {
                        RaiseError("Texture path is missing or invalid.", 3);
                        return null;
                    }
                    var imageExtension = Path.GetExtension(colorSourcePath).Replace(".", string.Empty);
                    if (!Enum.IsDefined(typeof(M2B_FileTextureFormats), imageExtension))
                    {
                        RaiseError(String.Format("{0} texture format {1} is not supported and cannot be used", colorPlugName, imageExtension));
                        return null;
                    }
                }
            }

            string alphaSourcePath = null;
            if (alphaDependencyNode != null)
            {
                alphaSourcePath = getSourcePathFromFileTexture(alphaDependencyNode);
                if (alphaSourcePath != null)
                {
                    if (String.IsNullOrWhiteSpace(alphaSourcePath))
                    {
                        RaiseError("Texture path is missing or invalid.", 3);
                        return null;
                    }
                    var imageExtension = Path.GetExtension(alphaSourcePath).Replace(".", String.Empty);
                    if (!Enum.IsDefined(typeof(M2B_FileTextureFormats), imageExtension))
                    {
                        RaiseError(String.Format("{0} texture format {1} is not supported and cannot be used", alphaPlugName, imageExtension));
                        return null;
                    }
                }
            }
            
            string id = colorDependencyNode != null ? colorDependencyNode.uuid().asString() : alphaDependencyNode.uuid().asString();
            string sp = colorSourcePath != null ? colorSourcePath : alphaSourcePath; 
            BabylonTexture babylonTexture = new BabylonTexture(id);
            string extension = Path.GetExtension(sp);
            babylonTexture.name = String.Format("{0}_RGBA{1}", Path.GetFileNameWithoutExtension(sp), extension).Replace(":", "_");
            babylonTexture.originalPath = sp;

            getTextureUVs(colorDependencyNode != null ? colorDependencyNode : alphaDependencyNode, babylonTexture);

            string[] mergeTextures = new string[] {colorSourcePath, alphaSourcePath};
            float[] defaultColors = new float[] {defaultBaseColor[0], defaultBaseColor[1], defaultBaseColor[2], defaultOpacity};
            int[] bitmapIndices = new int[] {0, 0, 0, 1};
            int[] channelIndices = new int[] {0, 1, 2, 0};

            babylonTexture.bitmap = mergeColorBitmaps(babylonTexture, mergeTextures, defaultColors, bitmapIndices, channelIndices);

            if (exportParameters.writeTextures)
            {
                copyTextureToOutputPath(babylonScene.OutputPath, babylonTexture);
            }

            return babylonTexture;
        }

        public BabylonTexture ExportTextureNormal(MFnDependencyNode materialDependencyNode, string normalPlugName, BabylonScene babylonScene)
        {
            return new BabylonTexture("12345");
        }

        public BabylonTexture ExportTextureClearCoat(MFnDependencyNode materialDependencyNode, string clearcoatPlugName, BabylonScene babylonScene)
        {
            return new BabylonTexture("12345");
        }

        // private BabylonTexture ExportORMTexture(BabylonScene babylonScene, MFnDependencyNode metallicTextureDependencyNode, MFnDependencyNode roughnessTextureDependencyNode, MFnDependencyNode ambientOcclusionTextureDependencyNode, float defaultMetallic, float defaultRoughness)
        // {
        //     // Prints
        //     if (metallicTextureDependencyNode != null)
        //     {
        //         Print(metallicTextureDependencyNode, logRankTexture, "Print ExportORMTexture metallicTextureDependencyNode");
        //     }
        //     if (roughnessTextureDependencyNode != null)
        //     {
        //         Print(roughnessTextureDependencyNode, logRankTexture, "Print ExportORMTexture roughnessTextureDependencyNode");
        //     }
        //     if (ambientOcclusionTextureDependencyNode != null)
        //     {
        //         Print(ambientOcclusionTextureDependencyNode, logRankTexture, "Print ExportORMTexture ambientOcclusionTextureDependencyNode");
        //     }

        //     // Use metallic or roughness texture as a reference for UVs parameters
        //     var textureDependencyNode = metallicTextureDependencyNode != null ? metallicTextureDependencyNode : roughnessTextureDependencyNode;
        //     if (textureDependencyNode == null)
        //     {
        //         return null;
        //     }

        //     var id = textureDependencyNode.uuid().asString();

        //     var babylonTexture = new BabylonTexture(id)
        //     {
        //         name = (ambientOcclusionTextureDependencyNode != null ? ambientOcclusionTextureDependencyNode.name : "") +
        //                (roughnessTextureDependencyNode != null ? roughnessTextureDependencyNode.name : ("" + (int)(defaultRoughness * 255))) +
        //                (metallicTextureDependencyNode != null ? metallicTextureDependencyNode.name : ("" + (int)(defaultMetallic * 255))) + ".jpg" // TODO - unsafe name, may conflict with another texture name
        //     };
        //     babylonTexture.name = babylonTexture.name.Replace(":", "_");

        //     // UVs
        //     getTextureUVs(textureDependencyNode, babylonTexture);

        //     // Is cube
        //     string sourcePath = getSourcePathFromFileTexture(textureDependencyNode);
        //     if (sourcePath == null)
        //     {
        //         return null;
        //     }
        //     if (sourcePath == "")
        //     {
        //         RaiseError("Texture path is missing.", logRankTexture + 1);
        //         return null;
        //     }
        //     _exportIsCube(sourcePath, babylonTexture, false);


        //     // --- Merge metallic, roughness, ambient occlusion maps ---
        //     if (exportParameters.writeTextures)
        //     {
        //         // Load bitmaps
        //         var metallicBitmap = LoadTexture(metallicTextureDependencyNode);
        //         var roughnessBitmap = LoadTexture(roughnessTextureDependencyNode);
        //         var ambientOcclusionBitmap = LoadTexture(ambientOcclusionTextureDependencyNode);

        //         // Merge bitmaps
        //         // The occlusion values are sampled from the R channel.
        //         // The roughness values are sampled from the G channel.
        //         // The metalness values are sampled from the B channel.
        //         Bitmap[] bitmaps = new Bitmap[] { ambientOcclusionBitmap, roughnessBitmap, metallicBitmap, null };
        //         int[] defaultValues = new int[] { 0, (int)(defaultRoughness * 255), (int)(defaultMetallic * 255), 0 };
        //         Bitmap ormBitmap = MergeBitmaps(bitmaps, defaultValues, ambientOcclusionBitmap != null ? "Occlusion, metallic and roughness" : "Metallic and roughness");
                
        //         // Write bitmap
        //         if (isBabylonExported)
        //         {
        //             RaiseMessage($"Texture | write image '{babylonTexture.name}'", logRankTexture + 1);
        //             SaveBitmap(ormBitmap, babylonScene.OutputPath, babylonTexture.name, System.Drawing.Imaging.ImageFormat.Jpeg);
        //         }
        //         else
        //         {
        //             // Store created bitmap for further use in gltf export
        //             babylonTexture.bitmap = ormBitmap;
        //         }
        //     }

        //     return babylonTexture;
        // }

        // private BabylonTexture ExportCoatTexture(MFnDependencyNode intensityTextureDependencyNode, MFnDependencyNode roughnessTextureDependencyNode,
        //                                                             BabylonScene babylonScene, string materialName, float intensity, float roughness)
        // {
        //     // Prints
        //     if (intensityTextureDependencyNode != null)
        //     {
        //         Print(intensityTextureDependencyNode, logRankTexture, "Print ExportCoatTexture intensityTextureDependencyNode");
        //     }
        //     if (roughnessTextureDependencyNode != null)
        //     {
        //         Print(roughnessTextureDependencyNode, logRankTexture, "Print ExportCoatTexture roughnessTextureDependencyNode");
        //     }

        //     // Use one as a reference for UVs parameters
        //     var textureDependencyNode = intensityTextureDependencyNode != null ? intensityTextureDependencyNode : roughnessTextureDependencyNode;
        //     if (textureDependencyNode == null)
        //     {
        //         return null;
        //     }

        //     var id = textureDependencyNode.uuid().asString();
        //     var babylonTexture = new BabylonTexture(id)
        //     {
        //         name = materialName + "_coat" + ".jpg" // TODO - unsafe name, may conflict with another texture name
        //     };
        //     babylonTexture.name = babylonTexture.name.Replace(":", "_");

        //     // Level
        //     babylonTexture.level = 1.0f;

        //     // Alpha
        //     babylonTexture.hasAlpha = false;
        //     babylonTexture.getAlphaFromRGB = false;

        //     // UVs
        //     getTextureUVs(textureDependencyNode, babylonTexture);

        //     // Is cube
        //     string sourcePath = getSourcePathFromFileTexture(textureDependencyNode);
        //     if (sourcePath == null)
        //     {
        //         return null;
        //     }
        //     if (sourcePath == "")
        //     {
        //         RaiseError("Texture path is missing.", logRankTexture + 1);
        //         return null;
        //     }
        //     _exportIsCube(sourcePath, babylonTexture, false);


        //     // --- Merge base color and opacity maps ---

        //     if (exportParameters.writeTextures)
        //     {
        //         // Load bitmaps
        //         var intensityBitmap = LoadTexture(intensityTextureDependencyNode);
        //         var roughnessBitmap = LoadTexture(roughnessTextureDependencyNode);

        //         // Merge bitmaps
        //         Bitmap[] bitmaps = new Bitmap[] { intensityBitmap, roughnessBitmap, null, null };
        //         int[] defaultValues = new int[] { (int)(intensity * 255), (int)(roughness * 255), 0, 1 };
        //         Bitmap coatBitmap = MergeBitmaps(bitmaps, defaultValues, "Coat intensity and roughness");
                
        //         // Write bitmap
        //         if (isBabylonExported)
        //         {
        //             RaiseMessage($"Texture | write image '{babylonTexture.name}'", logRankTexture + 1);
        //             SaveBitmap(coatBitmap, babylonScene.OutputPath, babylonTexture.name, System.Drawing.Imaging.ImageFormat.Jpeg);
        //         }
        //         else
        //         {
        //             // Store created bitmap for further use in gltf export
        //             babylonTexture.bitmap = coatBitmap;
        //         }
        //     }

        //     return babylonTexture;
        // }

        private void getTextureUVs(MFnDependencyNode textureDependencyNode, BabylonTexture babylonTexture)
        {
            MStringArray uvLinks = new MStringArray();
            MGlobal.executeCommand($@"uvLink -query -texture {textureDependencyNode.name};", uvLinks);
            if (uvLinks.Count == 0)
            {
                babylonTexture.coordinatesIndex = 0;
            }
            else
            {
                // Retreive UV set indices
                HashSet<int> uvSetIndices = new HashSet<int>();
                foreach (string uvLink in uvLinks)
                {
                    int indexOpenBracket = uvLink.LastIndexOf("[");
                    int indexCloseBracket = uvLink.LastIndexOf("]");
                    string uvSetIndexAsString = uvLink.Substring(indexOpenBracket + 1, indexCloseBracket - indexOpenBracket - 1);
                    int uvSetIndex = int.Parse(uvSetIndexAsString);
                    uvSetIndices.Add(uvSetIndex);
                }
                if (uvSetIndices.Count > 1)
                {
                    // Check that all uvSet indices are all 0 or all not 0
                    int nbZero = 0;
                    foreach (int uvSetIndex in uvSetIndices) {
                        if (uvSetIndex == 0)
                        {
                            nbZero++;
                        }
                    }
                    if (nbZero != 0 && nbZero != uvSetIndices.Count)
                    {
                        RaiseWarning("Texture is linked to UV1 and UV2. Only one UV set per texture is supported.", 3);
                    }
                }
                // The first UV set of a mesh is special because it can never be deleted
                // Thus if the UV set index is 0 then the binded mesh UV set is always UV1
                // Other UV sets of a mesh can be created / deleted at will
                // Thus the UV set index can have any value (> 0)
                // In this case, the exported UV set is always UV2 even though it would be UV3 or UV4 in Maya
                babylonTexture.coordinatesIndex = (new List<int>(uvSetIndices))[0] == 0 ? 0 : 1;
            }

            // For more information about UV
            // see http://help.autodesk.com/view/MAYAUL/2018/ENU/?guid=GUID-94070C7E-C550-42FD-AFC9-FBE82B173B1D
            babylonTexture.uOffset = textureDependencyNode.findPlug("offsetU").asFloat();
            babylonTexture.vOffset = textureDependencyNode.findPlug("offsetV").asFloat();
            babylonTexture.uScale = textureDependencyNode.findPlug("repeatU").asFloat();
            babylonTexture.vScale = textureDependencyNode.findPlug("repeatV").asFloat();
            
            // Maya only has a W rotation
            babylonTexture.uAng = 0;
            babylonTexture.vAng = 0;
            babylonTexture.wAng = textureDependencyNode.findPlug("rotateFrame").asFloat();
            if (babylonTexture.wAng != 0f && (babylonTexture.uScale != 1f || babylonTexture.vScale != 1f))
            {
                RaiseWarning("Texture rotation and tiling (scale) are supported separately or will cause unknown results", 3);
            }

            if (textureDependencyNode.findPlug("mirrorU").asBool())
            {
                babylonTexture.wrapU = BabylonTexture.AddressMode.MIRROR_ADDRESSMODE;
            }
            else if (textureDependencyNode.findPlug("wrapU").asBool())
            {
                babylonTexture.wrapU = BabylonTexture.AddressMode.WRAP_ADDRESSMODE;
            }
            else
            {
                // TODO - What is adress mode when not wrap nor mirror?
                babylonTexture.wrapU = BabylonTexture.AddressMode.CLAMP_ADDRESSMODE;
            }
            if (textureDependencyNode.findPlug("mirrorV").asBool())
            {
                babylonTexture.wrapV = BabylonTexture.AddressMode.MIRROR_ADDRESSMODE;
            }
            else if (textureDependencyNode.findPlug("wrapV").asBool())
            {
                babylonTexture.wrapV = BabylonTexture.AddressMode.WRAP_ADDRESSMODE;
            }
            else
            {
                // TODO - What is adress mode when not wrap nor mirror?
                babylonTexture.wrapV = BabylonTexture.AddressMode.CLAMP_ADDRESSMODE;
            }

            // Animation
            babylonTexture.animations = GetTextureAnimations(textureDependencyNode).ToArray();
        }

        private string getSourcePathFromFileTexture(MFnDependencyNode textureDependencyNode)
        {
            if (textureDependencyNode.typeId.id != (int)M2B_FileTextureTypes.AiImage &&
                textureDependencyNode.typeId.id != (int)M2B_FileTextureTypes.MayaFile)
            {
                RaiseError(String.Format("Texture name: {0} typeId: {1}", textureDependencyNode.name, textureDependencyNode.typeId.id));
                RaiseError("Only file or aiImage texture are supported.", 3);
                return null;
            }
            string fileTexturePath = textureDependencyNode.typeId.id == (int)M2B_FileTextureTypes.AiImage ? "filename" : "fileTextureName";
            MPlug fileTextureNamePlug = textureDependencyNode.findPlug(fileTexturePath);
            if (fileTextureNamePlug == null || fileTextureNamePlug.isNull)
            {
                RaiseError("Texture path is missing.", 3);
                return null;
            }
            string sourcePath = fileTextureNamePlug.asString();
            return sourcePath;
        }

        private MFnDependencyNode getTextureDependencyNode(MFnDependencyNode materialDependencyNode, string plugName)
        {
            MPlug texturePlug = materialDependencyNode.findPlug(plugName);
            
            if (texturePlug == null || texturePlug.isNull || !texturePlug.isConnected)
            {
                if (texturePlug.isCompound)
                {
                    // Retrieve the first non-empty plug and use the texture connected to it.
                    for (uint i = 0; i < texturePlug.numChildren; i++)
                    {
                        MPlug child = texturePlug.child(i);
                        if (child.isNull) 
                        { 
                            continue;
                        }
                        texturePlug = child;
                        break;
                    }
                }
                if (texturePlug == null || texturePlug.isNull || !texturePlug.isConnected)
                {
                    RaiseVerbose(String.Format("{0} plug has no input connections or cannot be found", plugName), 2);
                    return null;
                }
            }
            RaiseMessage(String.Format("Returning texture for input plug {0}", plugName), 2);
            return new MFnDependencyNode(texturePlug.source.node);
        }
        
        private Bitmap mergeColorBitmaps(BabylonTexture babylonTexture, string[] mergeTexturePaths, float[] defaultColors, 
                                                                                int[] bitmapIndices, int[] channelIndices)
        {
            Bitmap[] mergeBitmapArray = new Bitmap[mergeTexturePaths.Length];
            try
            {
                mergeBitmapArray = Array.ConvertAll(mergeTexturePaths, s => File.Exists(s) ? new Bitmap(s) : null);
            }
            catch
            {
                RaiseError("Failed to convert input textures to bitmap for merging");
                return null;
            }

            int outputBitmapHeight = 0, outputBitmapWidth = 0;
            bool bitmapDimensions = getBitmapDimensionsAreSquare(mergeBitmapArray, out outputBitmapHeight, out outputBitmapWidth);
            if (!bitmapDimensions)
            {
                RaiseError("Bitmap dimensions are not equal or not square and cannot be merged");
                return null;
            }

            int[] defaultColorArray = Array.ConvertAll(defaultColors, s => (int)(s * 255));

            Bitmap mergedBitmap = new Bitmap(outputBitmapWidth, outputBitmapHeight);
            for (int x=0; x < outputBitmapWidth; x++)
            {
                for (int y=0; y < outputBitmapHeight; y++)
                {
                    var r = mergeBitmapArray[bitmapIndices[0]] != null ?
                        getColorChannelFromIndex(mergeBitmapArray[bitmapIndices[0]].GetPixel(x,y), channelIndices[0]) : defaultColorArray[0];
                    var g = mergeBitmapArray[bitmapIndices[1]] != null ? 
                        getColorChannelFromIndex(mergeBitmapArray[bitmapIndices[1]].GetPixel(x,y), channelIndices[1]) : defaultColorArray[1];
                    var b = mergeBitmapArray[bitmapIndices[2]] != null ? 
                        getColorChannelFromIndex(mergeBitmapArray[bitmapIndices[2]].GetPixel(x,y), channelIndices[2]) : defaultColorArray[2];
                    var a = mergeBitmapArray[bitmapIndices[3]] != null ?
                        getColorChannelFromIndex(mergeBitmapArray[bitmapIndices[3]].GetPixel(x,y), channelIndices[3]) : defaultColorArray[3];
                    mergedBitmap.SetPixel(x, y, Color.FromArgb(a, r, g, b));
                }
            }

            var fileSavePath = Path.Combine(Path.GetDirectoryName(babylonTexture.originalPath), babylonTexture.name);
            try
            {
                using (FileStream fs = File.Open(fileSavePath, FileMode.Create))
                {
                    ImageCodecInfo encoder = getImageEncoder(ImageFormat.Png);
                    EncoderParameters encoderParameters = new EncoderParameters(1);
                    encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, exportParameters.txtQuality);
                    mergedBitmap.Save(fs, encoder, encoderParameters);                     
                }
            }
            catch
            {
                RaiseError(String.Format("Failed to encode and save combined bitmap to file {0}", fileSavePath), 2);
                return null;
            }

            RaiseError("RD DBG: mergeColorBitmaps: fileSavePath: " + fileSavePath, 2);
            babylonTexture.originalPath = fileSavePath;
            return mergedBitmap;
        }

        private bool getBitmapDimensionsAreSquare(Bitmap[] bitmaps, out int height, out int width)
        {
            height = width = 0;
            foreach(Bitmap bmp in bitmaps)
            {
                if (bmp == null) {continue;}
                if (bmp.Height != bmp.Width) {return false;}

                if (height == 0) {height = bmp.Height;}
                else
                {
                    if (height != bmp.Height) {return false;}
                }
                if (width == 0) {width = bmp.Width;}
                else
                {
                    if (width != bmp.Width) {return false;}
                }
            }
            if (height == 0 || width == 0) {return false;}
            return true;
        }

        private int getColorChannelFromIndex(Color color, int index)
        {
            switch (index)
            {
                case 0: return color.R;
                case 1: return color.G;
                case 2: return color.B;
                case 3: return color.A;
                default:
                    RaiseError("Color channel does not exist and cannot be extracted");
                    return 0;
            }
        }

        private ImageCodecInfo getImageEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        private void copyTextureToOutputPath(string sceneOutputPath, BabylonTexture babylonTexture)
        {
            try
            {
                string destinationPath = Path.Combine(sceneOutputPath, babylonTexture.name);
                RaiseMessage(String.Format("Copying texture {0} to scene output path {1}", babylonTexture.name, sceneOutputPath), 2);
                File.Copy(babylonTexture.originalPath, destinationPath, true);
            }
            catch (Exception c)
            {
                RaiseError(string.Format("Failed to copy texture {0} to scene output path {1}: {2}", babylonTexture.name, sceneOutputPath, c.ToString()), 3);
            }
        }
    }
}
