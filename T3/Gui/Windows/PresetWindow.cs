﻿using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.Mathematics.Interop;
using T3.Core;
using T3.Core.Logging;
using T3.Core.Operator;
using T3.Gui.Graph;
using T3.Gui.Graph.Rendering;
using T3.Gui.OutputUi;
using T3.Gui.Selection;
using T3.Gui.Styling;
using T3.Gui.Windows.Output;
using UiHelpers;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace T3.Gui.Windows
{
    /// <summary>
    /// Renders the <see cref="PresetWindow"/>
    /// </summary>
    public class PresetWindow : Window
    {
        public PresetWindow()
        {
            _previewCanvas = new PreviewCanvas(this);
            Config.Title = "Presets";
            Config.Visible = true;
        }

        private readonly List<VariationParameter> _variationParameters = new List<VariationParameter>();

        private class VariationParameter
        {
            public SymbolChildUi SymbolChildUi;
            public IInputSlot InputSlot { get; set; }
            public InputValue OriginalValue { get; set; }
            public SymbolChild.Input Input;
            public Type Type;
            public Instance Instance;
            public float Strength = 1;
        }

        protected override void DrawContent()
        {
            ImGui.BeginChild("#params", new Vector2(200, -1));
            {
                foreach (var symbolChildUi in SelectionManager.GetSelectedSymbolChildUis())
                {
                    ImGui.PushFont(Fonts.FontBold);
                    ImGui.Selectable(symbolChildUi.SymbolChild.ReadableName);
                    ImGui.PopFont();
                    ImGui.PushStyleColor(ImGuiCol.Text, Color.Gray.Rgba);
                    ImGui.PushID(symbolChildUi.Id.GetHashCode());
                        
                    foreach (var input in symbolChildUi.SymbolChild.InputValues.Values)
                    {
                        var p = input.DefaultValue;

                        // TODO: check if input is connected
                        if (p.ValueType == typeof(float)
                            || p.ValueType == typeof(Vector2)
                            || p.ValueType == typeof(Vector3)
                            || p.ValueType == typeof(Vector4)
                            )
                        {
                            var matchingParam = _variationParameters.FirstOrDefault(variationParam => input == variationParam.Input && symbolChildUi.Id == variationParam.SymbolChildUi.Id);
                            var selected = matchingParam != null;
                            
                            if (ImGui.Selectable(input.Name, selected))
                            {
                                if (selected)
                                {
                                    _variationParameters.Remove(matchingParam);
                                }
                                else
                                {
                                    var instance = SelectionManager.GetInstanceForSymbolChildUi(symbolChildUi);
                                    var inputSlot = instance.Inputs.Single(input2 => input2.Id == input.InputDefinition.Id);
                                    
                                    _variationParameters.Add(new VariationParameter()
                                                             {
                                                                 SymbolChildUi =  symbolChildUi,
                                                                 Input = input,
                                                                 Instance = SelectionManager.GetInstanceForSymbolChildUi(symbolChildUi),
                                                                 Type = p.ValueType,
                                                                 InputSlot = inputSlot,
                                                                 OriginalValue = inputSlot.Input.Value.Clone(),
                                                             });
                                }

                                _previewCanvas.ClearVariations();
                            }
                        }
                    }
                    ImGui.PopID();

                    ImGui.Dummy(Spacing);
                    ImGui.PopStyleColor();
                }
            }
            ImGui.EndChild();
            ImGui.SameLine();

            ImGui.BeginChild("canvas", new Vector2(-1, -1));
            {
                _previewCanvas.Draw();
            }
            ImGui.EndChild();
        }

        public override List<Window> GetInstances()
        {
            return new List<Window>();
        }

        private IOutputUi _outputUi;

        //=====================================================================
        private class PreviewCanvas : ScalableCanvas
        {
            public PreviewCanvas(PresetWindow presetWindow)
            {
                var extend = new Vector2(3, 3);
                var center = new Vector2(VariationGridSize / 2f, VariationGridSize / 2f);
                var left = (center - extend) * ThumbnailSize;
                var right = (center + extend) * ThumbnailSize;
                ZoomSpeed = 20000;

                FitAreaOnCanvas(new ImRect(left, right));
                _presetWindow = presetWindow;
            }

            private ISlot _firstOutputSlot;

            public void Draw()
            {
                InitializeCanvasTexture();

                var outputWindow = OutputWindow.OutputWindowInstances.FirstOrDefault(window => window.Config.Visible) as OutputWindow;
                if (outputWindow == null)
                {
                    ImGui.Text("No output window found");
                    return;
                }

                var instance = outputWindow.ShownInstance;
                if (instance == null)
                {
                    ImGui.Text("Nothing selected");
                    return;
                }

                _firstOutputSlot = instance.Outputs[0];
                if (!(_firstOutputSlot is Slot<Texture2D> textureSlot))
                {
                    ImGui.Text("Output should be texture");
                    _firstOutputSlot = null;
                    return;
                }

                // Set correct output ui
                {
                    var symbolUi = SymbolUiRegistry.Entries[instance.Symbol.Id];
                    if (!symbolUi.OutputUis.ContainsKey(_firstOutputSlot.Id))
                        return;

                    _presetWindow._outputUi = symbolUi.OutputUis[_firstOutputSlot.Id];
                }

                _previewTexture = textureSlot.Value;
                if (_previewTexture == null)
                    return;

                _previewTextureSrv = SrvManager.GetSrvForTexture(_previewTexture);

                FillInNextVariation();
                UpdateCanvas();
                Invalidate();

                // Draw Canvas Texture
                var rectOnScreen = ImRect.RectWithSize(WindowPos, new Vector2(_canvasTexture.Description.Width, _canvasTexture.Description.Height));
                
                var drawlist = ImGui.GetWindowDrawList();
                drawlist.AddImage((IntPtr)_canvasTextureSrv,
                                  rectOnScreen.Min,
                                  rectOnScreen.Max);

                foreach (var variation in _variationByGridIndex.Values)
                {
                    if(!IsGridPosVisible(variation.GridPos))
                        continue;
                    
                    var screenRect = GetGridPosScreenRect(variation.GridPos);
                    if (variation.ThumbnailNeedsUpdate)
                    {
                        drawlist.AddRectFilled(screenRect.Min, screenRect.Max, _needsUpdateColor);
                    }
                    else
                    {
                        //drawlist.AddRect(screenRect.Min, screenRect.Max, Color.Black);
                        //drawlist.AddRectFilled(screenRect.Min, screenRect.Max, Color.Green);
                    }
                }
            }

            private Color _needsUpdateColor = new Color(1f,1f,1f,0.1f);
            
            private void InitializeCanvasTexture()
            {
                if (_canvasTexture != null)
                    return;

                var description = new Texture2DDescription()
                                  {
                                      Height = 2048,
                                      Width = 2048,
                                      ArraySize = 1,
                                      BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                                      Usage = ResourceUsage.Default,
                                      CpuAccessFlags = CpuAccessFlags.None,
                                      Format = SharpDX.DXGI.Format.R8G8B8A8_UNorm,
                                      //MipLevels = mipLevels,
                                      OptionFlags = ResourceOptionFlags.GenerateMipMaps,
                                      SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                                  };

                _canvasTexture = new Texture2D(Program.Device, description);
                _canvasTextureSrv = SrvManager.GetSrvForTexture(_canvasTexture);
                _canvasTextureRtv = new RenderTargetView(Program.Device, _canvasTexture);
            }

            
            private ImRect GetGridPosScreenRect(GridPos gridPos)
            {
                var thumbnailInCanvas = ImRect.RectWithSize(new Vector2(gridPos.X, gridPos.Y) * ThumbnailSize, ThumbnailSize);
                var r = TransformRect(thumbnailInCanvas);
                return new ImRect((int)r.Min.X, (int)r.Min.Y, (int)r.Max.X-1, (int)r.Max.Y-1);
            }

            private bool IsGridPosVisible(GridPos gridPos)
            {
                var contentRegion = new ImRect(ImGui.GetWindowContentRegionMin() + ImGui.GetWindowPos(),
                                               ImGui.GetWindowContentRegionMax() + ImGui.GetWindowPos());

                contentRegion.Expand(ThumbnailSize * Scale);

                var rectOnScreen = GetGridPosScreenRect(gridPos);

                var visible = contentRegion.Contains(rectOnScreen);
                return visible;
            }

            
            private void Invalidate()
            {
                var scaleChanged = Math.Abs(Scale.X - _lastScale) > 0.01f;
                var scrollChanged = Scroll != _lastScroll;

                // TODO: optimize performance by only invalidating thumbnails and moving part of the canvas  
                if (scaleChanged || scrollChanged)
                {
                    _lastScale = Scale.X;
                    _lastScroll = Scroll;
                    _currentOffsetIndexForFocus = 0;
                    _updateCompleted = false;

                    foreach (var variation in _variationByGridIndex.Values)
                    {
                        variation.ThumbnailNeedsUpdate = true;
                    }
                    
                    SetGridIndexAtWindowCenter();

                    Program.Device.ImmediateContext.ClearRenderTargetView(_canvasTextureRtv, new RawColor4(0, 0, 0, 0));
                }
            }

            private void SetGridIndexAtWindowCenter()
            {
                var contentRegion = new ImRect(ImGui.GetWindowContentRegionMin() + ImGui.GetWindowPos(),
                                               ImGui.GetWindowContentRegionMax() + ImGui.GetWindowPos());
                var centerInCanvas = InverseTransformPosition(contentRegion.GetCenter());
                _currentFocusIndex.X = (int)(centerInCanvas.X / ThumbnailSize.X);
                _currentFocusIndex.Y = (int)(centerInCanvas.Y / ThumbnailSize.Y);
            }
            
            
            private void FillInNextVariation()
            {
                if (_updateCompleted)
                    return;

                while (_currentOffsetIndexForFocus < SortedOffset.Length)
                {
                    var offset = SortedOffset[_currentOffsetIndexForFocus++];
                    var gridPos = _currentFocusIndex + offset;
                    if (!gridPos.IsWithinGrid(VariationGridSize))
                        continue;

                    if (!IsGridPosVisible(gridPos))
                        continue;

                    var hasVariation = _variationByGridIndex.TryGetValue(gridPos.GridIndex, out var variation);
                    if (hasVariation)
                    {
                        if (!variation.ThumbnailNeedsUpdate)
                            continue;

                        RenderThumbnail(variation);
                        return;
                    }
                    else
                    {
                        variation = CreateVariationAtGridPos(gridPos);
                        if (variation.ValuesForParameters.Count > 0)
                        {
                            RenderThumbnail(variation);
                            _variationByGridIndex[gridPos.GridIndex] = variation;
                        }

                        return;
                    }
                }

                _updateCompleted = true;
            }

            public void ClearVariations()
            {
                _currentOffsetIndexForFocus = 0;
                _updateCompleted = false;
                _variationByGridIndex.Clear();
            }

            private readonly GridPos[] _neighbourOffsets =
            {
                new GridPos(-1, -1),
                new GridPos(0, -1),
                new GridPos(1, -1),
                new GridPos(1, 0),
                new GridPos(1, 1),
                new GridPos(0, 1),
                new GridPos(-1, 1),
                new GridPos(-1, 0),
            };

            private readonly Random _random = new Random();


            private Variation CreateVariationAtGridPos(GridPos pos)
            {
                // Collect Neighbours
                var newVariation = new Variation(pos);
                Variation neighbour2;

                // Collect neighbours
                var neighbours = new List<Variation>();
                foreach (var nOffset in _neighbourOffsets)
                {
                    var neighbourPos = pos + nOffset;

                    if (_variationByGridIndex.TryGetValue(neighbourPos.GridIndex, out neighbour2))
                        neighbours.Add(neighbour2);
                }

                // Initialize parameters with defaults of neighbour averages 
                var scattering = 0.5f;
                var useDefault = (neighbours.Count == 0);

                foreach (var param in _presetWindow._variationParameters)
                {
                    if (useDefault)
                    {
                        if (param.OriginalValue is InputValue<float> value)
                        {
                            newVariation.ValuesForParameters.Add(param, value.Value);
                        }
                        else if (param.OriginalValue is InputValue<Vector2> vec2Value)
                        {
                            newVariation.ValuesForParameters.Add(param, vec2Value.Value);
                        }
                        else if (param.OriginalValue is InputValue<Vector3> vec3Value)
                        {
                            newVariation.ValuesForParameters.Add(param, vec3Value.Value);
                        }
                        else if (param.OriginalValue is InputValue<Vector4> vec4Value)
                        {
                            newVariation.ValuesForParameters.Add(param, vec4Value.Value);
                        }
                        continue;
                    }

                    if (param.Type == typeof(float))
                    {
                        var value = 0f;
                        foreach (var neighbour in neighbours)
                        {
                            value += (float)neighbour.ValuesForParameters[param];
                        }

                        value *= 1f / neighbours.Count + ((float)_random.NextDouble() - 0.5f) * scattering;
                        value += _random.NextFloat(-scattering, scattering);
                        newVariation.ValuesForParameters.Add(param, value);
                    }

                    if (param.Type == typeof(Vector2))
                    {
                        var value = Vector2.Zero;
                        foreach (var neighbour in neighbours)
                        {
                            value += (Vector2)neighbour.ValuesForParameters[param];
                        }
                    
                        value *= 1f / neighbours.Count;
                        value += new Vector2(
                                             _random.NextFloat(-scattering, scattering),
                                             _random.NextFloat(-scattering, scattering)
                                            );
                        
                        newVariation.ValuesForParameters.Add(param, value);
                    }
                    
                    if (param.Type == typeof(Vector3))
                    {
                        var value = Vector3.Zero;
                        foreach (var neighbour in neighbours)
                        {
                            value += (Vector3)neighbour.ValuesForParameters[param];
                        }
                    
                        value *= 1f / neighbours.Count;
                        value += new Vector3(
                                             _random.NextFloat(-scattering, scattering),
                                             _random.NextFloat(-scattering, scattering),
                                             _random.NextFloat(-scattering, scattering)
                                            );
                        
                        newVariation.ValuesForParameters.Add(param, value);
                    }
                    
                    if (param.Type == typeof(Vector4))
                    {
                        var value = Vector4.Zero;
                        foreach (var neighbour in neighbours)
                        {
                            value += (Vector4)neighbour.ValuesForParameters[param];
                        }
                    
                        value *= 1f / neighbours.Count;
                        value += new Vector4(
                                             _random.NextFloat(-scattering, scattering),
                                             _random.NextFloat(-scattering, scattering),
                                             _random.NextFloat(-scattering, scattering),
                                             _random.NextFloat(-scattering, scattering)
                                            );
                        
                        newVariation.ValuesForParameters.Add(param, value);
                    }
                }
                return newVariation;
            }

            private static GridPos[] BuildSortedOffsets()
            {
                var offsets = new List<GridPos>();
                for (var x = -VariationGridSize; x < VariationGridSize; x++)
                {
                    for (var y = -VariationGridSize; y < VariationGridSize; y++)
                    {
                        offsets.Add(new GridPos(x, y));
                    }
                }

                offsets.Sort((a, b) => (a.X * a.X + a.Y * a.Y)
                                       - (b.X * b.X + b.Y * b.Y));

                return offsets.ToArray();
            }

            private void RenderThumbnail(Variation variation)
            {
                variation.ThumbnailNeedsUpdate = false;

                var screenRect = GetGridPosScreenRect(variation.GridPos);
                var posInCanvasTexture = screenRect.Min - WindowPos;

                // Set variation values
                foreach (var (param, value) in variation.ValuesForParameters)
                {
                    switch (param.InputSlot)
                    {
                        case InputSlot<float> floatInput:
                            floatInput.SetTypedInputValue((float)value);
                            break;
                        case InputSlot<Vector2> vec2Input:
                            vec2Input.SetTypedInputValue((Vector2)value);
                            break;
                        case InputSlot<Vector3> vec3Input:
                            vec3Input.SetTypedInputValue((Vector3)value);
                            break;
                        case InputSlot<Vector4> vec4Input:
                            vec4Input.SetTypedInputValue((Vector4)value);
                            break;
                    }
                }

                // Render variation
                EvaluationContext.Reset();
                _presetWindow._outputUi.DrawValue(_firstOutputSlot, EvaluationContext);

                // Setup graphics pipeline for rendering into the canvas texture
                var resourceManager = ResourceManager.Instance();
                var deviceContext = resourceManager.Device.ImmediateContext;
                deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
                deviceContext.Rasterizer.SetViewport(new ViewportF(posInCanvasTexture.X, posInCanvasTexture.Y,
                                                                   screenRect.GetWidth(), screenRect.GetHeight(),
                                                                   0.0f, 1.0f));
                deviceContext.OutputMerger.SetTargets(_canvasTextureRtv);

                var vertexShader = resourceManager.GetVertexShader(Program.FullScreenVertexShaderId);
                deviceContext.VertexShader.Set(vertexShader);
                var pixelShader = resourceManager.GetPixelShader(Program.FullScreenPixelShaderId);
                deviceContext.PixelShader.Set(pixelShader);
                deviceContext.PixelShader.SetShaderResource(0, _previewTextureSrv);

                // Render the preview in the canvas texture
                deviceContext.Draw(3, 0);
                deviceContext.PixelShader.SetShaderResource(0, null);

                // Restore values
                foreach (var param in variation.ValuesForParameters.Keys)
                {
                    param.Input.Value.Assign(param.OriginalValue);
                    param.InputSlot.DirtyFlag.Invalidate();
                }
            }

            private static readonly GridPos[] SortedOffset = BuildSortedOffsets();
            private static readonly GridPos GridCenter = new GridPos(VariationGridSize / 2, VariationGridSize / 2);
            private float _lastScale;
            private Vector2 _lastScroll = Vector2.One;

            public override IEnumerable<ISelectableNode> SelectableChildren { get; } = new List<ISelectableNode>();
            private static readonly Vector2 ThumbnailSize = new Vector2(160, 160 / 16f * 9);
            private readonly Dictionary<int, Variation> _variationByGridIndex = new Dictionary<int, Variation>();
            private  GridPos _currentFocusIndex = GridCenter;
            private int _currentOffsetIndexForFocus;
            private bool _updateCompleted;

            private Texture2D _previewTexture;
            private ShaderResourceView _previewTextureSrv;

            private Texture2D _canvasTexture;
            private ShaderResourceView _canvasTextureSrv;
            private RenderTargetView _canvasTextureRtv;
            private readonly PresetWindow _presetWindow;

            private static readonly EvaluationContext EvaluationContext = new EvaluationContext()
                                                                          {
                                                                              RequestedResolution = new Size2((int)ThumbnailSize.X, (int)ThumbnailSize.Y)
                                                                          };
        }

        private class Variation
        {
            public readonly GridPos GridPos;
            public bool ThumbnailNeedsUpdate;

            public Variation(GridPos pos)
            {
                GridPos = pos;
                ThumbnailNeedsUpdate = true;
            }

            public readonly Dictionary<VariationParameter, object> ValuesForParameters = new Dictionary<VariationParameter, object>();

            // public void UpdateThumbnail()
            // {
            //     _canv RenderThumbnail(this);
            //     ThumbnailNeedsUpdate = false;
            // }
        }

        public struct GridPos
        {
            public  int X;
            public  int Y;

            public GridPos(int x, int y)
            {
                X = x;
                Y = y;
            }

            public static GridPos operator +(GridPos a, GridPos b)
            {
                return new GridPos(a.X + b.X, a.Y + b.Y);
            }

            public static GridPos operator +(GridPos a, Size2 b)
            {
                return new GridPos(a.X + b.Width, a.Y + b.Height);
            }

            public bool IsWithinGrid(int gridSize)
            {
                return X > 0 && X < gridSize && Y > 0 && Y < gridSize;
            }

            public int GridIndex => Y * VariationGridSize + X;
        }

        private readonly PreviewCanvas _previewCanvas;
        private const int VariationGridSize = 100;
        private static readonly Vector2 Spacing = new Vector2(1, 5);
    }
}