using Autodesk.Maya.OpenMaya;
using BabylonExport.Entities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

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
                var ormTexture = exportParameters.exportTextures ?
                                ExportTextureORM(materialDependencyNode, "metalness", "specularRoughness", babylonScene) : null;
                babylonMaterial.metallic = materialDependencyNode.findPlug("metalness").asFloat();// : 1.0f;
                babylonMaterial.roughness = materialDependencyNode.findPlug("specularRoughness").asFloat();// : 1.0f;
                babylonMaterial.metallicRoughnessTexture = ormTexture;
                babylonMaterial.occlusionTexture = ormTexture;

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
                                ExportTexture(materialDependencyNode, "normalCamera", babylonScene) : null;

                float coatWeight = materialDependencyNode.findPlug("coat").asFloat();
                MFnDependencyNode intensityCoatTextureDependencyNode = getTextureDependencyNode(materialDependencyNode, "coat");
                if (coatWeight > 0.0f || intensityCoatTextureDependencyNode != null)
                {
                    RaiseVerbose("Exporting AiStandardSurface clear coat", 1);
                    // babylonMaterial.clearCoat.isEnabled = true;
                    // babylonMaterial.clearCoat.indexOfRefraction = materialDependencyNode.findPlug("coatIOR").asFloat();

                    // float coatRoughness = materialDependencyNode.findPlug("coatRoughness").asFloat();
                    // MFnDependencyNode roughnessCoatTextureDependencyNode = getTextureDependencyNode(materialDependencyNode, "coatRoughness");
                    // var coatTexture = exportParameters.exportTextures ? ExportCoatTexture(intensityCoatTextureDependencyNode, 
                    //                                             roughnessCoatTextureDependencyNode, babylonScene, name, coatWeight, coatRoughness) : null;
                    // babylonMaterial.clearCoat.texture = coatTexture;
                    // babylonMaterial.clearCoat.roughness = coatTexture == null ? coatRoughness : 1.0f;
                    // babylonMaterial.clearCoat.intensity = coatTexture == null ? coatWeight : 1.0f;

                    // float[] coatColor = materialDependencyNode.findPlug("coatColor").asFloatArray();
                    // if (coatColor[0] != 1.0f || coatColor[1] != 1.0f || coatColor[2] != 1.0f || coatTexture != null)
                    // {
                    //     babylonMaterial.clearCoat.isTintEnabled = true;
                    //     babylonMaterial.clearCoat.tintColor = coatTexture == null ? coatColor : new[] { 1.0f, 1.0f, 1.0f };
                    //     babylonMaterial.clearCoat.texture = coatTexture;
                    // }

                    babylonMaterial.clearCoat.tintThickness = 0.65f;
                    babylonMaterial.clearCoat.bumpTexture = exportParameters.exportTextures ? ExportTexture(materialDependencyNode, "coatNormal", babylonScene) : null;
                }

                RaiseVerbose("Exporting AiStandardSurface export alpha mode", 1);
                if (babylonMaterial.alpha != 1.0f || (babylonMaterial.baseTexture != null && babylonMaterial.baseTexture.hasAlpha))
                {
                    babylonMaterial.transparencyMode = (int)BabylonPBRMetallicRoughnessMaterial.TransparencyMode.ALPHABLEND;
                }
                if (babylonMaterial.transparencyMode == (int)BabylonPBRMetallicRoughnessMaterial.TransparencyMode.ALPHATEST)
                {
                    babylonMaterial.alphaCutOff = 0.5f; // the statement above masks this call 
                }
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