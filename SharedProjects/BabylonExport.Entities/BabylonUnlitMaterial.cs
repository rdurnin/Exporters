using System.Runtime.Serialization;

namespace BabylonExport.Entities
{
    [DataContract]
    public class BabylonUnlitMaterial : BabylonMaterial
    {
        [DataMember]
        public string customType { get; private set; }

        [DataMember]
        public float[] baseColor { get; set; }

        [DataMember]
        public BabylonTexture baseTexture { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public float? alphaCutOff { get; set; }

        [DataMember]
        public bool doubleSided { get; set; }

        [DataMember]
        public int transparencyMode { get; set; }

        public BabylonUnlitMaterial(string id) : base(id)
        {
            customType = "BABYLON.UnlitMaterial";
            
            transparencyMode = 0;
            doubleSided = false;
            isUnlit = true;
        }

        public BabylonUnlitMaterial(BabylonUnlitMaterial original) : base(original)
        {
            customType = original.customType;
            baseColor = original.baseColor;
            baseTexture = original.baseTexture;
            doubleSided = original.doubleSided;
            transparencyMode = original.transparencyMode;
            isUnlit = original.isUnlit;
        }
    }
}
