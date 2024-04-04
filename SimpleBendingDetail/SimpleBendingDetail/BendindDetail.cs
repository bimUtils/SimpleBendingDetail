using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI.Selection;
using System.Security.Cryptography;


namespace SimpleBendingDetail
{
    internal class BendindDetail
    {

       
        public XYZ ProjectedCenter;
        private IList<DetailSegment> segments { get; set; }
        private DetailSegment hook0Segment { get; set; } 
        private DetailSegment hook1Segment { get; set; } 
        private double barDiameter;

        public BendindDetail(Document doc, View view, Rebar rebar)
        {

            this.segments = new List<DetailSegment>();

            //******************
            //Get bar diameter
            //******************

            double rebarDiameter = rebar.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER).AsDouble();
            this.barDiameter = rebarDiameter;


            //******************
            //Get projected centerline curves
            //******************

            int visibleBarPosition = int.MaxValue;
            IList<Curve> planCurves;

            //direction vectors
            XYZ rebarNormal = rebar.GetShapeDrivenAccessor().Normal;
            XYZ viewNormal = view.ViewDirection.Normalize();
            XYZ viewX = view.RightDirection.Normalize();
            XYZ viewY = view.UpDirection.Normalize();

            //Get first visible bar from rebar

            int startPos = 0;
            int endPos = rebar.NumberOfBarPositions;


            if (rebar.LayoutRule != RebarLayoutRule.Single)
            {
                if (!rebar.IncludeFirstBar)
                {
                    startPos++;
                }

                if (!rebar.IncludeLastBar)
                {
                    endPos--;
                }
            }

            for (int i = startPos; i < endPos; i++)
            {
                if (!rebar.IsBarHidden(view, i) && visibleBarPosition > i)
                {
                    visibleBarPosition = i;
                }
            }

            //Get CenterLineCurves projected to current view and Projection of rebar curves BoundingBox onto plane
            planCurves = GetCenterlineCurvesProjectedToPlane(rebar, view, visibleBarPosition);


            //******************************
            //draw detail curves for check
            //******************************

            //foreach (Curve curve in planCurves)
            //{
            //    DetailCurve detailCurve = doc.Create.NewDetailCurve(view, curve);
            //}


            //***********************
            //Set segments geometry
            //***********************

            foreach (Curve curve in planCurves)
            {

                //Curve curve = planCurves[i];
                DetailSegment segment = new DetailSegment();

                //Get all parameters except of Length
                XYZ curveOffset = curve.Evaluate(0.5, true) - this.ProjectedCenter;
                segment.XOffset = curveOffset.DotProduct(viewX); //3D distance projected on view X axis
                segment.YOffset = curveOffset.DotProduct(viewY); //3D distance projected on view Y axis
                segment.Length = curve.Length;

                if (curve == planCurves.First())
                    segment.IsStartSegment = true;
                else
                    segment.IsStartSegment = false;

                if (curve == planCurves.Last())
                    segment.IsEndSegment = true;
                else
                    segment.IsEndSegment = false;


                if (curve.IsCyclic)  //arc
                {

                    segment.ArcRadius = (curve as Arc).Radius;

                    Transform derivates = (curve as Arc).ComputeDerivatives(0.5, true);
                    XYZ curveTangent = derivates.BasisX.Normalize();
                    XYZ curveNormal = derivates.BasisY.Normalize();
                    XYZ curveBiNormal = derivates.BasisZ.Normalize();
                    segment.Rotation = Math.PI - curveTangent.AngleOnPlaneTo(viewX, viewNormal);

                    if(!curveBiNormal.IsAlmostEqualTo(viewNormal))
                    {
                        segment.Rotation += Math.PI;
                    }
                }
                else //lines
                {
                    XYZ curveDirection = (curve as Line).Direction;
                    segment.Rotation = 2*Math.PI - curveDirection.AngleOnPlaneTo(viewX, viewNormal);
                }

                //int startSegmentPosition = 0;
                //int endSegmentPosition = planCurves.Count - 1;

                if (rebar.GetHookTypeId(0) != ElementId.InvalidElementId && curve == planCurves.First()) //this is the start hook
                {
                    this.hook0Segment = segment;
                }
                else if (rebar.GetHookTypeId(1) != ElementId.InvalidElementId && curve == planCurves.Last()) //this is the last hook
                {
                        this.hook1Segment = segment;
                }
                else
                    this.segments.Add(segment);

            }


            //***********************
            //Write length and visibility to each segment
            //***********************

            SetBarLengths(rebar, doc, visibleBarPosition);

            //***********************
            //Rotate labels in right direction
            //***********************
            
            OrientLabels();

        }


        public ElementId PlaceOnView (Document doc, View view, FamilySymbol familySymbol)
        {
            //place family

            const int MaxSegments = 18;

            if (!familySymbol.IsActive)
            {
                familySymbol.Activate();
            }

            FamilyInstance familyInstance = doc.Create.NewFamilyInstance(this.ProjectedCenter, familySymbol, view);

            ElementId famId = familyInstance.Id;

            //edit bar diameter

            familyInstance.GetParameters("Bar_Diameter").First().Set(this.barDiameter);


            //edit segments length
            int seg = 0;
            foreach (DetailSegment segment in this.segments)
            {
                seg++;
                if (seg > MaxSegments && segment.Length == 0)
                {
                    break;
                }
                familyInstance.GetParameters("s" + seg.ToString() + "_Length").First().Set(segment.Length);
                familyInstance.GetParameters("s" + seg.ToString() + "_X_Offset").First().Set(segment.XOffset);
                familyInstance.GetParameters("s" + seg.ToString() + "_Y_Offset").First().Set(segment.YOffset);
                familyInstance.GetParameters("s" + seg.ToString() + "_Arc_Radius").First().Set(segment.ArcRadius);
                familyInstance.GetParameters("s" + seg.ToString() + "_Rotation").First().Set(segment.Rotation);
                familyInstance.GetParameters("s" + seg.ToString() + "_Min_Label").First().Set(segment.MinLabel);
                familyInstance.GetParameters("s" + seg.ToString() + "_Max_Label").First().Set(segment.MaxLabel);
                familyInstance.GetParameters("s" + seg.ToString() + "_Visibility").First().Set(1);
                if(segment == segments.Last())
                {
                    if(hook1Segment == null)
                    {
                        familyInstance.GetParameters("s" + seg.ToString() + "_End_Segment").First().Set(1);
                    }
                }
                else if (hook1Segment == null && segments[seg - 1].Length == 0)
                {
                    familyInstance.GetParameters("s" + seg.ToString() + "_End_Segment").First().Set(1);
                }
            }

            //add hook lengths
            if (this.hook0Segment != null) //there is start hook
            {
                familyInstance.GetParameters("h1_Length").First().Set(this.hook0Segment.Length);
                familyInstance.GetParameters("h1_X_Offset").First().Set(this.hook0Segment.XOffset);
                familyInstance.GetParameters("h1_Y_Offset").First().Set(this.hook0Segment.YOffset);
                familyInstance.GetParameters("h1_Rotation").First().Set(this.hook0Segment.Rotation);
                familyInstance.GetParameters("h1_Label").First().Set(this.hook0Segment.MinLabel);
                familyInstance.GetParameters("h1_Visibility").First().Set(1);
                
                familyInstance.GetParameters("s1_Start_Segment").First().Set(0);
            }
            if (this.hook1Segment != null) //there is start hook
            {
                familyInstance.GetParameters("h2_Length").First().Set(this.hook1Segment.Length);
                familyInstance.GetParameters("h2_X_Offset").First().Set(this.hook1Segment.XOffset);
                familyInstance.GetParameters("h2_Y_Offset").First().Set(this.hook1Segment.YOffset);
                familyInstance.GetParameters("h2_Rotation").First().Set(this.hook1Segment.Rotation);
                familyInstance.GetParameters("h2_Label").First().Set(this.hook1Segment.MinLabel);
                familyInstance.GetParameters("h2_Visibility").First().Set(1);
            }

            return famId;

        }

        private IList<Curve> GetCenterlineCurvesProjectedToPlane(Rebar rebar, View view, int barPosition)
        {

            IList<Curve> curves = new List<Curve>();
            Plane plane = Plane.CreateByOriginAndBasis(view.Origin, view.RightDirection, view.UpDirection);


            IList<Curve> rebarCurves = rebar.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeAllMultiplanarCurves, barPosition).ToList();
            Transform visibleRebarTransform = rebar.GetShapeDrivenAccessor().GetBarPositionTransform(barPosition);

            //Get the bounding box center of rebar curve
            CurveBoundingBoxXYZ box = new CurveBoundingBoxXYZ();
            foreach (Curve curve in rebarCurves)
                box.AddCurve(curve.CreateTransformed(visibleRebarTransform));
            XYZ centerOfBox = (box.Max + box.Min) / 2;

            //direction vectors
            XYZ rebarNormal = rebar.GetShapeDrivenAccessor().Normal;
            XYZ viewNormal = view.ViewDirection;
            XYZ viewX = view.RightDirection;
            XYZ viewY = view.UpDirection;
            XYZ intersectingVector = new XYZ();


            //angles between rebar and view, and view X direction
            double angleRebarPlanToView = viewNormal.AngleTo(rebarNormal);

            //angle up to pi/2
            if (angleRebarPlanToView > Math.PI / 2 )
            {
                angleRebarPlanToView = Math.PI - angleRebarPlanToView;
            }


            if(!MathComparisonUtils.IsAlmostEqual (angleRebarPlanToView, 0))
            {
                //vector parallel to intersection of rebar plane and view plane
                intersectingVector = viewNormal.CrossProduct(rebarNormal);

                //original double angleRebarToXDirection = intersectingVector.AngleTo(viewX);
                double angleRebarToXDirection = intersectingVector.AngleOnPlaneTo(viewX, viewNormal);

                //read from bottom and right
                if (MathComparisonUtils.IsGreaterThanOrAlmostEqual(angleRebarToXDirection, 0) && MathComparisonUtils.IsLessThan(angleRebarToXDirection, Math.PI / 2) || MathComparisonUtils.IsGreaterThanOrAlmostEqual(angleRebarToXDirection, Math.PI / 2 * 3))
                {
                    angleRebarPlanToView += Math.PI;
                }

            }



            //project center to view plane
            this.ProjectedCenter = ProjectPointToPlane(plane, centerOfBox);


            foreach (Curve curve in rebarCurves)
            {
                Curve c = null;


                if (!MathComparisonUtils.IsAlmostEqual (angleRebarPlanToView, 0) )
                {
                    Transform rotated = Transform.CreateRotationAtPoint(intersectingVector, angleRebarPlanToView, centerOfBox) * visibleRebarTransform;
                    c = curve.CreateTransformed(rotated);
                }
                else
                {
                    c = curve.CreateTransformed(visibleRebarTransform);
                }

                curves.Add(c);  //transposed curves to visibleRebarPosition in the same plane as centerOfBox

            }

            return curves;

        }

        private void SetBarLengths(Rebar rebar, Document doc, int barPosition)
        {


            ElementId shapeId = rebar.GetShapeId();
            RebarShape rebarShape = doc.GetElement(shapeId) as RebarShape;
            RebarShapeDefinition shapeDefinition = rebarShape.GetRebarShapeDefinition();

            bool isVarying = (rebar.DistributionType == DistributionType.VaryingLength);
            int startPos = 0;
            int endPos = rebar.NumberOfBarPositions;


            if(rebar.LayoutRule != RebarLayoutRule.Single)
            {
                if (!rebar.IncludeFirstBar)
                {
                    startPos++;
                }

                if (!rebar.IncludeLastBar)
                {
                    endPos--;
                }
            }





            //add hook lengths

            if (rebar.GetHookTypeId(0) != ElementId.InvalidElementId) //there is start hook
            {
                double startHookTangentLength = rebar.get_Parameter(BuiltInParameter.REBAR_SHAPE_PARAM_START_HOOK_TAN_LEN).AsDouble();
                this.hook0Segment.MinLabel = startHookTangentLength;
            }
 
            if (rebar.GetHookTypeId(1) != ElementId.InvalidElementId) //there is end hook
            {
                double startHookTangentLength = rebar.get_Parameter(BuiltInParameter.REBAR_SHAPE_PARAM_END_HOOK_TAN_LEN).AsDouble();
                this.hook1Segment.MinLabel = startHookTangentLength;
            }

            int familySegment = 0;
            if (rebar.GetHookTypeId(0) != ElementId.InvalidElementId) //there is start hook
            {
                familySegment++;
            }

            if (shapeDefinition.GetType() == typeof(RebarShapeDefinitionByArc))
            {
                //code for simple arc
                RebarShapeDefinitionByArc definitionByArc = shapeDefinition as RebarShapeDefinitionByArc;
                IList<RebarShapeConstraint> constraints = definitionByArc.GetConstraints();

                foreach (RebarShapeConstraint constraint in constraints)
                {
                    if (constraint.GetType() == typeof(RebarShapeConstraintArcLength))
                    {
                        ElementId paramId = constraint.GetParamId();

                        ParameterValue minParameterValue = null;
                        ParameterValue maxParameterValue = null;


                        if (isVarying)
                        {

                            double min = double.MaxValue;
                            double max = double.MinValue;

                            for (int i = startPos; i < endPos; i++)
                            {
                                DoubleParameterValue value = rebar.GetParameterValueAtIndex(paramId, i) as DoubleParameterValue;
                                if (min > value.Value)
                                {
                                    min = value.Value;
                                    minParameterValue = rebar.GetParameterValueAtIndex(paramId, i);
                                }

                                if(max < value.Value)
                                {
                                    max = value.Value;
                                    maxParameterValue = rebar.GetParameterValueAtIndex(paramId,i);
                                }
                            }

                            if(MathComparisonUtils.IsAlmostEqual(min, max))
                            {
                                maxParameterValue = null;
                            }

                        } //if isVarying
                        else
                        {
                            minParameterValue = rebar.GetParameterValueAtIndex(paramId, barPosition);
                        }
                        if (definitionByArc.Type == RebarShapeDefinitionByArcType.LappedCircle)  //todo implement lap splice label
                        {
                            this.segments[familySegment].MinLabel = 0;
                            if(maxParameterValue != null)
                            {
                                this.segments[familySegment].MaxLabel = 0;
                            }
                        }
                        else
                        {
                            this.segments[familySegment].MinLabel = (minParameterValue as DoubleParameterValue).Value;
                            if(maxParameterValue  != null)
                            {
                                this.segments[familySegment].MaxLabel = (maxParameterValue as DoubleParameterValue).Value;
                            }
                        }

                    }
                }
            }
            else
            {
                //code for RebarShapeDefinitionBySegments
                RebarShapeDefinitionBySegments definitionBySegments = shapeDefinition as RebarShapeDefinitionBySegments;


                for (int i = 0; i < definitionBySegments.NumberOfSegments; i++)
                {
                    RebarShapeSegment seg = definitionBySegments.GetSegment(i);
                    IList<RebarShapeConstraint> constraints = seg.GetConstraints();

                    ParameterValue minParameterValue = null;
                    ParameterValue maxParameterValue = null;

                    //get type and values of current segment constraints
                    double minConstr180DegreeBendArcLength = 0;
                    double maxConstr180DegreeBendArcLength = 0;

                    double minConstrSegmentLength = 0;
                    double maxConstrSegmentLength = 0;

                    double minConstr180DegreeBendRadius = 0;
                    double maxConstr180DegreeBendRadius = 0;

                    foreach (RebarShapeConstraint constraint in constraints)
                    {
                        //********************
                        //code for RebarShapeConstraintSegmentLength
                        //********************

                        if (constraint.GetType() == typeof(RebarShapeConstraintSegmentLength))
                        {
                            ElementId paramId = constraint.GetParamId();

                            if (isVarying)
                            {
                                double min = double.MaxValue;
                                double max = double.MinValue;

                                for (int ii = startPos; ii < endPos; ii++)
                                {
                                    DoubleParameterValue value = rebar.GetParameterValueAtIndex(paramId, ii) as DoubleParameterValue;
                                    if (min > value.Value)
                                    {
                                        min = value.Value;
                                        minParameterValue = rebar.GetParameterValueAtIndex(paramId, ii);
                                    }

                                    if (max < value.Value)
                                    {
                                        max = value.Value;
                                        maxParameterValue = rebar.GetParameterValueAtIndex(paramId, ii);
                                    }
                                }
                                if (MathComparisonUtils.IsAlmostEqual(min, max))
                                {
                                    maxParameterValue = null;
                                }

                            }
                            else
                            {
                                minParameterValue = rebar.GetParameterValueAtIndex(paramId, barPosition);
                            }

                            minConstrSegmentLength = (minParameterValue as DoubleParameterValue).Value;
                            if(maxParameterValue != null)
                            {
                                maxConstrSegmentLength = (maxParameterValue as DoubleParameterValue).Value;
                            }

                        }

                        //********************
                        //code for RebarShapeConstraint180DegreeBendRadius
                        //********************

                        if (constraint.GetType() == typeof(RebarShapeConstraint180DegreeBendRadius))
                        {
                            ElementId paramId = constraint.GetParamId();

                            if (isVarying)
                            {
                                double min = double.MaxValue;
                                double max = double.MinValue;

                                for (int ii = startPos; ii < endPos; ii++)
                                {
                                    DoubleParameterValue value = rebar.GetParameterValueAtIndex(paramId, ii) as DoubleParameterValue;
                                    if (min > value.Value)
                                    {
                                        min = value.Value;
                                        minParameterValue = rebar.GetParameterValueAtIndex(paramId, ii);
                                    }

                                    if (max < value.Value)
                                    {
                                        max = value.Value;
                                        maxParameterValue = rebar.GetParameterValueAtIndex(paramId, ii);
                                    }
                                }
                                if (MathComparisonUtils.IsAlmostEqual(min, max))
                                {
                                    maxParameterValue = null;
                                }

                            }
                            else
                            {
                                minParameterValue = rebar.GetParameterValueAtIndex(paramId, barPosition);
                            }

                            minConstr180DegreeBendRadius = (minParameterValue as DoubleParameterValue).Value;
                            if (maxParameterValue != null)
                            {
                                maxConstr180DegreeBendRadius = (maxParameterValue as DoubleParameterValue).Value;
                            }

                        }

                        //********************
                        //code for RebarShapeConstraint180DegreeBendArcLength
                        //********************

                        if (constraint.GetType() == typeof(RebarShapeConstraint180DegreeBendArcLength))
                        {
                            ElementId paramId = constraint.GetParamId();

                            if (isVarying)
                            {
                                double min = double.MaxValue;
                                double max = double.MinValue;

                                int barNum = rebar.NumberOfBarPositions;
                                for (int ii = startPos; ii < endPos; ii++)
                                {
                                    DoubleParameterValue value = rebar.GetParameterValueAtIndex(paramId, ii) as DoubleParameterValue;
                                    if (min > value.Value)
                                    {
                                        min = value.Value;
                                        minParameterValue = rebar.GetParameterValueAtIndex(paramId, ii);
                                    }

                                    if (max < value.Value)
                                    {
                                        max = value.Value;
                                        maxParameterValue = rebar.GetParameterValueAtIndex(paramId, ii);
                                    }
                                }
                                if (MathComparisonUtils.IsAlmostEqual(min, max))
                                {
                                    maxParameterValue = null;
                                }

                            }
                            else
                            {
                                minParameterValue = rebar.GetParameterValueAtIndex(paramId, barPosition);
                            }

                            minConstr180DegreeBendArcLength = (minParameterValue as DoubleParameterValue).Value;
                            if (maxParameterValue != null)
                            {
                                maxConstr180DegreeBendArcLength = (maxParameterValue as DoubleParameterValue).Value;
                            }

                        }

                    }

                    if ( minConstr180DegreeBendArcLength > 0)
                    {
                        familySegment--;
                        this.segments[familySegment].MinLabel = minConstr180DegreeBendArcLength;
                        if (maxConstr180DegreeBendArcLength > 0)
                        {
                            this.segments[familySegment].MaxLabel = maxConstr180DegreeBendArcLength;
                        }
                        familySegment++; //next segment
                    }
                    else if (minConstr180DegreeBendRadius > 0 && minConstrSegmentLength > 0)
                    {
                        this.segments[familySegment].MinLabel = 0;
                    }
                    else if (minConstrSegmentLength > 0) 
                    {
                        this.segments[familySegment].MinLabel = minConstrSegmentLength;
                        if(maxConstrSegmentLength > 0)
                        {
                            this.segments[familySegment].MaxLabel = maxConstrSegmentLength;
                        }
                        familySegment++; //next segment
                        familySegment++; //jump over vertex
                    }
                }

            }

        }

        private bool RotateLabelSingleSegment (DetailSegment segment)
        {

            double xCoord = segment.XOffset;
            double yCoord = segment.YOffset;

            //center of segment
            XYZ segmentCenter = new XYZ(xCoord, yCoord, 0);

            //vector from center to 0,0,0
            XYZ vectorFromCenterToSegment = segmentCenter;

            //vector from 0,0,0 direction by segment.Rotation
            Transform rotation = Transform.CreateRotation(XYZ.BasisZ, segment.Rotation);
            XYZ normalToSegment = rotation.OfVector(XYZ.BasisY.Negate());

            double angle = 0;
            if (!vectorFromCenterToSegment.IsZeroLength())
            {
                angle = vectorFromCenterToSegment.AngleTo(normalToSegment);
            }

            //rotate if vectors are opposite
            if (angle > (-Math.PI / 2) && angle < Math.PI / 2)
            {
                return true;
            }
            else
            {
                return false;
            }

        }

        private void OrientLabels()
        {

            int seg = 0;
            foreach (DetailSegment segment in this.segments)
            {
                if (RotateLabelSingleSegment(segment))
                {
                    this.segments[seg].MinLabel *= -1;

                }
                seg++;
            }

            if (hook0Segment != null) //has start hook
            {
                if (RotateLabelSingleSegment(hook0Segment))
                {
                    this.hook0Segment.MinLabel *= -1;
                }
            }
 
            if (hook1Segment != null) //has start hook
            {
                if (RotateLabelSingleSegment(hook1Segment))
                {
                    this.hook1Segment.MinLabel *= -1;
                }
            }
        }


            //utils
        private static XYZ ProjectPointToPlane(Plane plane, XYZ p)
        {
            XYZ v = p - plane.Origin;
            double signedDisatnce = plane.Normal.DotProduct(v);

            XYZ q = p - signedDisatnce * plane.Normal;

            return q;
        }





    }




}
