using System;
using System.Collections.Generic;
using T3.Core.Animation.Curves;
using T3.Core.Logging;

namespace T3.Core.Operator
{
    public class SymbolExtension
    {
        // todo: how is a symbol extension defined, what exactly does this mean
    }

    public class Animator : SymbolExtension
    {
        public void CreateInputUpdateAction<T>(IInputSlot inputSlot)
        {
            if (inputSlot is Slot<float> typedInputSlot)
            {
                var newCurve = new Curve();
                newCurve.AddOrUpdateV(EvaluationContext.GlobalTime, new VDefinition()
                                                                    {
                                                                        Value = typedInputSlot.Value,
                                                                        InType = VDefinition.Interpolation.Spline,
                                                                        OutType = VDefinition.Interpolation.Spline,
                                                                    });
                newCurve.AddOrUpdateV(EvaluationContext.GlobalTime + 1, new VDefinition()
                                                                        {
                                                                            Value = typedInputSlot.Value + 2,
                                                                            InType = VDefinition.Interpolation.Spline,
                                                                            OutType = VDefinition.Interpolation.Spline,
                                                                        });
                _animatedInputCurves.Add(inputSlot.Id, newCurve);

                typedInputSlot.UpdateAction = context => { typedInputSlot.Value = (float)newCurve.GetSampledValue(context.Time); };
            }
            else
            {
                Log.Error("Could not create update action.");
            }
        }

        public void RemoveAnimationFrom(IInputSlot inputSlot)
        {
            if (inputSlot is Slot<float> typedInputSlot)
            {
                typedInputSlot.SetUpdateActionBackToDefault();

                _animatedInputCurves.Remove(inputSlot.Id);
            }
        }

        public bool IsInputSlotAnimated(IInputSlot inputSlot)
        {
            return _animatedInputCurves.ContainsKey(inputSlot.Id);
        }

        public Curve GetCurveForInput(IInputSlot inputSlot)
        {
            return _animatedInputCurves[inputSlot.Id];
        }

        private readonly Dictionary<Guid, Curve> _animatedInputCurves = new Dictionary<Guid, Curve>();
    }
}