using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Autodesk.Maya.OpenMaya;
using BabylonExport.Entities;

namespace Maya2Babylon
{
    partial class BabylonExporter
    {
        public enum M2B_SurfaceShaderTypes
        {
            AiCarPaint          = 0x115d8b,
            AiFlat              = 0x115d85,
            AiLambert           = 0x115db0,
            AiStandardSurface   = 0x115d51,
            AiWireframe         = 0x115d08,
            MayaBlinn           = 0x52424c4e,
            MayaLambert         = 0x524c414d,
            MayaPhong           = 0x5250484f,
            MayaSurface         = 0x52535348
        }

        /// <summary>
        /// List of simple materials
        /// </summary>
        List<MFnDependencyNode> referencedMaterials = new List<MFnDependencyNode>();

        /// <summary>
        /// List of sub materials binded to each multimaterial
        /// </summary>
        readonly Dictionary<string, List<MFnDependencyNode>> multiMaterials = new Dictionary<string, List<MFnDependencyNode>>();

        private void ExportMultiMaterial(string uuidMultiMaterial, List<MFnDependencyNode> materials, BabylonScene babylonScene, bool fullPBR)
        {
            var babylonMultimaterial = new BabylonMultiMaterial { id = uuidMultiMaterial };

            // Name
            string nameConcatenated = "";
            bool isFirstTime = true;
            List<MFnDependencyNode> materialsSorted = new List<MFnDependencyNode>(materials);
            materialsSorted.Sort(new MFnDependencyNodeComparer());
            foreach (MFnDependencyNode material in materialsSorted)
            {
                if (!isFirstTime)
                {
                    nameConcatenated += "_";
                }
                isFirstTime = false;

                nameConcatenated += material.name;
            }
            babylonMultimaterial.name = nameConcatenated;

            // Materials
            var uuids = new List<string>();
            foreach (MFnDependencyNode subMat in materials)
            {
                string uuidSubMat = subMat.uuid().asString();
                uuids.Add(uuidSubMat);

                if (!referencedMaterials.Contains(subMat, new MFnDependencyNodeEqualityComparer()))
                {
                    // Export sub material
                    referencedMaterials.Add(subMat);
                    ExportMaterial(subMat, babylonScene, fullPBR);
                }
            }
            babylonMultimaterial.materials = uuids.ToArray();

            babylonScene.MultiMaterialsList.Add(babylonMultimaterial);
        }

        private void ExportMaterial(MFnDependencyNode materialDependencyNode, BabylonScene babylonScene, bool fullPBR)
        {
            var name = materialDependencyNode.name;
            var id = materialDependencyNode.uuid().asString();

            RaiseMessage(string.Format("Exporting material dependency node {0}", name), 1);

            // Maya non-physical materials
            if (materialDependencyNode.typeId.id == (int)M2B_SurfaceShaderTypes.MayaLambert ||
                materialDependencyNode.typeId.id == (int)M2B_SurfaceShaderTypes.MayaBlinn ||
                materialDependencyNode.typeId.id == (int)M2B_SurfaceShaderTypes.MayaPhong)
            {
                // RD TODO: Rebuild and reinstate the following routine
                RaiseMessage("Exporting Maya non-physical material", 2);
            }

            // AiFlat
            else if (materialDependencyNode.typeId.id == (int)M2B_SurfaceShaderTypes.AiFlat)
            {
                RaiseMessage("Exporting AiFlat shader", 2);
                var babylonMaterial = new BabylonUnlitMaterial(id) {name=name};

                RaiseMessage("Gathering AiFlat custom attributes", 2);
                babylonMaterial.metadata = ExportCustomAttributeFromMaterial(babylonMaterial);

                RaiseVerbose("Exporting AiFlat base", 1);
                float[] baseColor = materialDependencyNode.findPlug("color").asFloatArray();

                RaiseVerbose("Exporting AiFlat base texture", 1);
                var baseTexture = exportParameters.exportTextures ?
                                ExportTexture(materialDependencyNode, "color", babylonScene) : null;
                babylonMaterial.baseColor = baseTexture == null ? baseColor : new[] {1.0f, 1.0f, 1.0f, 1.0f};
                babylonMaterial.baseTexture = baseTexture;
            
                babylonScene.MaterialsList.Add(babylonMaterial);
            }

            // AiStandardSurface
            else if (materialDependencyNode.typeId.id == (int)M2B_SurfaceShaderTypes.AiStandardSurface)
            {
                RaiseMessage("Exporting AiStandardSurface shader", 2);
                var babylonMaterial = new BabylonPBRMetallicRoughnessMaterial(id) {name=name};

                RaiseMessage("Gathering AiStandardSurface custom attributes", 2);
                babylonMaterial.metadata = ExportCustomAttributeFromMaterial(babylonMaterial);

                RaiseMessage("Gathering AiStandardSurface standard attributes", 2);

                RaiseVerbose("Exporting AiStandardSurface base", 1);
                float baseWeight = materialDependencyNode.findPlug("base").asFloat();
                float[] baseColor = materialDependencyNode.findPlug("baseColor").asFloatArray();

                RaiseVerbose("Exporting AiStandardSurface opacity", 1);
                float[] opacityColor = materialDependencyNode.findPlug("opacity").asFloatArray();
                float opacityAvg = (opacityColor[0] + opacityColor[1] + opacityColor[2]) / 3.0f;

                RaiseVerbose("Combining AiStandardSurface baseColor and opacity", 1);
                var baseTexture = exportParameters.exportTextures ?
                                ExportTextureColorAlpha(materialDependencyNode, "baseColor", "opacity", 
                                                        baseColor, opacityAvg, babylonScene) : null;
                babylonMaterial.baseColor = baseTexture == null ?
                                baseColor.Multiply(baseWeight) : new[] {baseWeight, baseWeight, baseWeight};
                babylonMaterial.baseTexture = baseTexture;
                babylonMaterial.alpha = (baseTexture != null && baseTexture.hasAlpha) ? 1.0f : opacityAvg; 

                RaiseVerbose("Exporting AiStandardSurface metalness", 1);
                float defaultOcclusion = 1.0f; // TODO: pull this value from custom attributes
                float defaultMetallic = materialDependencyNode.findPlug("metalness").asFloat();
                float defaultRoughness = materialDependencyNode.findPlug("specularRoughness").asFloat();
                var ormTexture = exportParameters.exportTextures ?
                                ExportTextureORM(materialDependencyNode, "metalness", "specularRoughness",
                                                defaultOcclusion, defaultMetallic, defaultRoughness, babylonScene) : null;
                babylonMaterial.metallic = ormTexture == null ? defaultMetallic : 1.0f;
                babylonMaterial.roughness = ormTexture == null ? defaultRoughness : 1.0f;
                babylonMaterial.metallicRoughnessTexture = ormTexture;
                babylonMaterial.occlusionTexture = ormTexture;
                // TODO: move the following to a custom gltf extension
                babylonMaterial.indexOfRefraction = materialDependencyNode.findPlug("specularIOR").asFloat();
                babylonMaterial.anisotropicWeight = materialDependencyNode.findPlug("specularAnisotropy").asFloat();
                babylonMaterial.anisotropicRotation = materialDependencyNode.findPlug("specularRotation").asFloat();

                RaiseVerbose("Exporting AiStandardSurface emission", 1);
                float emissiveWeight = materialDependencyNode.findPlug("emission").asFloat();
                float[] emissiveColor = materialDependencyNode.findPlug("emissionColor").asFloatArray();
                var emissiveTexture = exportParameters.exportTextures ?
                                ExportTexture(materialDependencyNode, "emissionColor", babylonScene) : null;
                babylonMaterial.emissive = emissiveTexture == null ? 
                                emissiveColor.Multiply(emissiveWeight) : new[] {emissiveWeight, emissiveWeight, emissiveWeight};
                babylonMaterial.emissiveTexture = emissiveTexture;

                RaiseVerbose("Exporting AiStandardSurface normal", 1);
                babylonMaterial.normalTexture = exportParameters.exportTextures ? 
                                ExportTextureNormal(materialDependencyNode, "normalCamera", babylonScene) : null;

                float coatWeight = materialDependencyNode.findPlug("coat").asFloat();
                if (coatWeight > 0.0f || materialDependencyNode.findPlug("coat").isConnected)
                {
                    RaiseVerbose("Exporting AiStandardSurface clear coat", 1);
                    babylonMaterial.clearCoat.isEnabled = true;

                    RaiseVerbose("Exporting AiStandardSurface clear coat color", 1);
                    float[] coatColor = materialDependencyNode.findPlug("coatColor").asFloatArray();
                    // the following are not used in the gltf extension
                    var coatColorTexture = exportParameters.exportTextures ?
                                    ExportTexture(materialDependencyNode, "coatColor", babylonScene) : null;
                    babylonMaterial.clearCoat.isTintEnabled = (coatColorTexture != null || coatColor.Take(3).Sum() == 3.0f); 
                    babylonMaterial.clearCoat.tintColor = coatColorTexture == null ? coatColor : new float[] {1.0f, 1.0f, 1.0f};
                    babylonMaterial.clearCoat.tintTexture = coatColorTexture;

                    RaiseVerbose("Combining AiStandardSurface clear coat weight and roughness", 1);
                    float coatRoughness = materialDependencyNode.findPlug("coatRoughness").asFloat();
                    var coatTexture = exportParameters.exportTextures ?
                                    ExportTextureCoatRoughness(materialDependencyNode, "coat", "coatRoughness",
                                                                coatWeight, coatRoughness, babylonScene) : null;
                    babylonMaterial.clearCoat.intensity = coatTexture == null ? coatWeight : 1.0f;
                    babylonMaterial.clearCoat.roughness = coatTexture == null ? coatRoughness : 1.0f;
                    babylonMaterial.clearCoat.texture = coatTexture;
                    // the following is not used in the gltf extension
                    babylonMaterial.clearCoat.indexOfRefraction = materialDependencyNode.findPlug("coatIOR").asFloat();

                    RaiseVerbose("Exporting AiStandardSurface clear coat bump", 1);
                    babylonMaterial.clearCoat.bumpTexture = exportParameters.exportTextures ?
                                    ExportTexture(materialDependencyNode, "coatNormal", babylonScene) : null;

                    // the following is not used in the gltf extension
                    RaiseVerbose("Exporting AiStandardSurface clear coat thickness approximation", 1);
                    babylonMaterial.clearCoat.tintThickness = 0.65f;
                }

                RaiseVerbose("Exporting AiStandardSurface export alpha mode", 1);
                // TODO: cover case where baseTexture.alpha is 1.0f
                if (babylonMaterial.alpha != 1.0f || (babylonMaterial.baseTexture != null && babylonMaterial.baseTexture.hasAlpha))
                {
                    babylonMaterial.transparencyMode = (int)BabylonPBRMetallicRoughnessMaterial.TransparencyMode.ALPHABLEND;
                }
                 // TODO: the statement above masks this call and should be rethought
                if (babylonMaterial.transparencyMode == (int)BabylonPBRMetallicRoughnessMaterial.TransparencyMode.ALPHATEST)
                {
                    babylonMaterial.alphaCutOff = 0.5f;
                }
                // TODO: define more cases where full pbr is required (sss, transparent, refractive)
                if (fullPBR) // upgrayedd
                {
                    RaiseVerbose("Converting AiStandardSurface babylon material to full PBR", 1);
                    babylonScene.MaterialsList.Add(new BabylonPBRMaterial(babylonMaterial));
                }
                else
                {
                    babylonScene.MaterialsList.Add(babylonMaterial);
                }
            }
            else
            {
                RaiseWarning(string.Format("{0} is an unsupported material type and will not be exported", name), 2);
            }
        }
        
        public string GetMultimaterialUUID(List<MFnDependencyNode> materials)
        {
            List<MFnDependencyNode> materialsSorted = new List<MFnDependencyNode>(materials);
            materialsSorted.Sort(new MFnDependencyNodeComparer());

            string uuidConcatenated = "";
            bool isFirstTime = true;
            foreach (MFnDependencyNode material in materialsSorted)
            {
                if (!isFirstTime)
                {
                    uuidConcatenated += "_";
                }
                isFirstTime = false;

                uuidConcatenated += material.uuid().asString();
            }

            return uuidConcatenated;
        }

        public class MFnDependencyNodeComparer : IComparer<MFnDependencyNode>
        {
            public int Compare(MFnDependencyNode x, MFnDependencyNode y)
            {
                // RD TODO: see below
                return x.uuid().asString().CompareTo(y.uuid().asString());
            }
        }

        public class MFnDependencyNodeEqualityComparer : IEqualityComparer<MFnDependencyNode>
        {
            public bool Equals(MFnDependencyNode x, MFnDependencyNode y)
            {
                // RD TODO: I don't believe this needs to be cast as string
                // https://docs.microsoft.com/en-us/dotnet/api/system.guid.op_equality?view=net-5.0
                return x.uuid().asString() == y.uuid().asString();
            }

            public int GetHashCode(MFnDependencyNode obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}