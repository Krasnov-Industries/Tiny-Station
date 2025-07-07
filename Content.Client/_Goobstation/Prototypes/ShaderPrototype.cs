using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization;
using System.Collections.Generic;

namespace Content.Client._Goobstation.Prototypes
{
    [Virtual]
    [Prototype("ShaderPrototype")]
    public class ShaderPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; set; } = default!;

        [DataField("vertex")]
        public string Vertex { get; set; } = default!;

        [DataField("fragment")]
        public string Fragment { get; set; } = default!;

        [DataField("sampler2D")]
        public List<Sampler2D> sampler2D { get; set; } = new();

        [DataField("float")]
        public List<FloatParam> Float { get; set; } = new();

        public class Sampler2D
        {
            [DataField("name")]
            public string Name { get; set; } = default!;

            [DataField("path")]
            public string Path { get; set; } = default!;
        }

        public class FloatParam
        {
            [DataField("name")]
            public string Name { get; set; } = default!;

            [DataField("default")]
            public float Default { get; set; }
        }
    }
}
