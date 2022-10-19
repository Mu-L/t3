using SharpDX.Direct3D11;
using T3.Core.Operator;
using T3.Core.Operator.Attributes;
using T3.Core.Operator.Slots;
using T3.Operators.Types.Id_b238b288_6e9b_4b91_bac9_3d7566416028;

namespace T3.Operators.Types.Id_8e6ed99c_a3e0_42c0_9f81_a89b1e340757
{
    public class Sketch : Instance<Sketch>
    {
        [Output(Guid = "8cedd2ef-75a2-46d9-8a07-02491389a89f")]
        public readonly Slot<Texture2D> ColorBuffer = new Slot<Texture2D>();

        [Output(Guid = "6d0b50be-70d4-4539-8d9d-ebb7434075c2")]
        public readonly Slot<T3.Core.DataTypes.BufferWithViews> Points = new Slot<T3.Core.DataTypes.BufferWithViews>();

        [Input(Guid = "1280a038-ea38-472d-aeb9-37a27b76ce8f", MappedType = typeof(_SketchImpl.Modes))]
        public readonly InputSlot<int> Mode = new InputSlot<int>();

        [Input(Guid = "4e80860e-f553-43cb-8b69-fc3b3b3eda73")]
        public readonly InputSlot<float> PageFloatIndex = new InputSlot<float>();

        [Input(Guid = "1c5d0d86-c000-449e-903a-3212d19d8e1d")]
        public readonly InputSlot<float> StrokeSize = new InputSlot<float>();

        [Input(Guid = "31f3942e-bac5-407f-ad44-6d09920754d9")]
        public readonly InputSlot<System.Numerics.Vector4> StrokeColor = new InputSlot<System.Numerics.Vector4>();

        [Input(Guid = "44b88a09-6374-4180-9bc9-713ccfbb36f0")]
        public readonly InputSlot<System.Numerics.Vector4> Background = new InputSlot<System.Numerics.Vector4>();

        [Input(Guid = "45be101b-0086-4f40-a230-0b860209460b", MappedType = typeof(ShowModes))]
        public readonly InputSlot<int> ShowAs = new InputSlot<int>();

        [Input(Guid = "0f089041-06b0-43f2-a5b6-8214abf9f959")]
        public readonly InputSlot<float> WidthFactor = new InputSlot<float>();

        [Input(Guid = "2ded8235-157d-486b-a997-87d09d18f998")]
        public readonly InputSlot<string> Filename = new InputSlot<string>();

        private enum ShowModes
        {
            OnlyAtFrame,
            ShowUntilNextFrame,
            WithOnionSkin,
        }
    }
}
