﻿using ImGuiNET;
using SharpDX;

namespace T3.Gui.InputUi
{
    public class Int3InputUi : SingleControlInputUi<Int3>
    {
        public override bool DrawSingleEditControl(string name, ref Int3 value)
        {
            return ImGui.DragInt3("##int3Edit", ref value.X);
        }

        protected override void DrawValueDisplay(string name, ref Int3 value)
        {
            DrawEditControl(name, ref value);
        }
    }
}