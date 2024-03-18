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


namespace SimpleBendingDetail
{
    internal class BendindDetail
    {

        //private UIDocument uidoc;
        //private Document doc;
        //private View vew;

       
        public XYZ ProjectedCenter;
        private IList<DetailSegment> segments { get; set; }
        private DetailSegment hook1Segment { get; } = null;
        private DetailSegment hook2Segment { get; } = null;
        private double barDiameter;

        public BendindDetail(Document doc, View view, Rebar rebar)
        {

            this.segments = new List<DetailSegment>();

            //******************
            //Get bar diameter
            //******************

            RebarBarType barType = doc.GetElement(rebar.GetTypeId()) as RebarBarType;
            this.barDiameter = barType.BarDiameter;

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
            for (int i = 0; i < rebar.NumberOfBarPositions; i++)
            {
                if (!rebar.IsBarHidden(view, i) && visibleBarPosition > i)
                {
                    visibleBarPosition = i;
                }
            }

            //Get CenterLineCurves projected to current view and Projection of rebar curves BoundingBox onto plane
            planCurves = GetCenterlineCurvesProjectedToPlane(rebar, view, visibleBarPosition);

            //draw detail curves for check
            foreach (Curve curve in planCurves)
            {
                DetailCurve detailCurve = doc.Create.NewDetailCurve(view, curve);
            }

            //***********************
            //Get segments parameters for family
            //***********************

            //int numberOfCurves = planCurves.Count;

            int currentSegment = 0;
            ElementId shapeId = rebar.GetShapeId();
            RebarShape rebarShape = doc.GetElement(shapeId) as RebarShape;
            IList<Curve> curvesForBrowser = rebarShape.GetCurvesForBrowser();

            for (int i = 0; i < planCurves.Count; i++)
            {

                Curve curve = planCurves[i];
                DetailSegment segment = new DetailSegment();

                //TODO CHECK IF it works with projectedCenter instead of centerOfBox (real center, before projection to plane)

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
                    int itttt = 0;
                }
                else //lines
                {
                    XYZ curveDirection = (curve as Line).Direction;
                    segment.Rotation = 2*Math.PI - curveDirection.AngleOnPlaneTo(viewX, viewNormal);
                    int itt = 0;
                }

                int startSegmentPosition = 0;
                int endSegmentPosition = planCurves.Count - 1;

                //Position of segments excluding hook
                if (rebar.GetHookTypeId(0) != ElementId.InvalidElementId) //there is start hook
                {
                    startSegmentPosition = 2;
                }
                if (rebar.GetHookTypeId(1) != ElementId.InvalidElementId) //there is end hook
                {
                    endSegmentPosition -= 2;
                }

                //CONTINUE doesnt work for single arc!!

                //**********************
                //Get All Segments Length
                //**********************

                List<ParameterValue> segmentLengths = BarSegmentsLength(rebar, doc, visibleBarPosition);

                //Get Length parameter for each segment except for hooks. Set Length=0 for arc between segments 

                if (i >= startSegmentPosition && i <= endSegmentPosition)
                {
                    if (curve.GetType() == curvesForBrowser[currentSegment].GetType())
                    {
                        segment.MinLabel = (segmentLengths[currentSegment] as DoubleParameterValue).Value;
                        currentSegment++;
                    }
                }
                else
                {
                    segment.MinLabel = 0;
                }

                this.segments.Add(segment);
            }

        }


        public void PlaceOnView (Document doc, View view, FamilySymbol familySymbol)
        {
            //place family

            const int MaxSegments = 18;

            if (!familySymbol.IsActive)
            {
                familySymbol.Activate();
            }

            FamilyInstance familyInstance = doc.Create.NewFamilyInstance(this.ProjectedCenter, familySymbol, view);


            int seg = 0;
            foreach(DetailSegment segment in this.segments)
            {
                seg++;
                if (seg > MaxSegments) continue;
                familyInstance.GetParameters("s" + seg.ToString() + "_Length").First().Set(segment.Length);
                familyInstance.GetParameters("s" + seg.ToString() + "_X_Offset").First().Set(segment.XOffset);
                familyInstance.GetParameters("s" + seg.ToString() + "_Y_Offset").First().Set(segment.YOffset);
                familyInstance.GetParameters("s" + seg.ToString() + "_Arc_Radius").First().Set(segment.ArcRadius);
                familyInstance.GetParameters("s" + seg.ToString() + "_Rotation").First().Set(segment.Rotation);
                familyInstance.GetParameters("s" + seg.ToString() + "_Min_Label").First().Set(segment.MinLabel);
                familyInstance.GetParameters("s" + seg.ToString() + "_Max_Label").First().Set(segment.MaxLabel);

                //BOOL!!!!
                //familyInstance.GetParameters(seg.ToString() + "_IsStartSegment").First().Set((int)segment.IsStartSegment);
                //familyInstance.GetParameters(seg.ToString() + "_IsEndSegment").First().Set((int)segment.IsEndSegment);
            }

            //Parameter barDiameter = familyInstance.LookupParameter("Bar_Diameter");
            //barDiameter.Set(0.1);







            //int d = 1;

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

            //vector parallel to intersection of rebar plane and view plane
            XYZ intersectingVector = viewNormal.CrossProduct(rebarNormal);

            //angles between rebar and view, and view X direction
            double angleRebarPlanToView = viewNormal.AngleTo(rebarNormal);
            double angleRebarToXDirection = intersectingVector.AngleTo(viewX);



            //angle up to pi/2
            if (angleRebarPlanToView > Math.PI / 2)
            {
                angleRebarPlanToView = Math.PI - angleRebarPlanToView;
            }

            //read from bottom and right
            //todo implement almost equal 0.0
            if (angleRebarPlanToView > 0.00000001 && angleRebarToXDirection > -0.0000001 && (angleRebarToXDirection < Math.PI / 2 + 0.0000001 || angleRebarToXDirection > Math.PI / 2 * 3 - 0.0000001))
            {
                angleRebarPlanToView += Math.PI;
            }


            //project center to view plane
            this.ProjectedCenter = ProjectPointToPlane(plane, centerOfBox);


            foreach (Curve curve in rebarCurves)
            {
                Curve c = null;

                //todo implement almostNotEqual

                if (angleRebarPlanToView > 0.00000000001)
                {
                    Transform rotated = Transform.CreateRotationAtPoint(intersectingVector, angleRebarPlanToView, centerOfBox) * visibleRebarTransform;
                    c = curve.CreateTransformed(rotated);
                }
                else
                {
                    c = curve.CreateTransformed(visibleRebarTransform);
                }

                //TODO MOVE CURVE c TO PLANE!!!!

                curves.Add(c);  //transposed curves to visibleRebarPosition in the same plane as centerOfBox

            }

            return curves;


        }

        private List<ParameterValue> BarSegmentsLength(Rebar rebar, Document doc, int barPosition)
        {

            List<ParameterValue> segmentLengths = new List<ParameterValue>();

            ElementId shapeId = rebar.GetShapeId();
            RebarShape rebarShape = doc.GetElement(shapeId) as RebarShape;
            RebarShapeDefinition shapeDefinition = rebarShape.GetRebarShapeDefinition();


            //todo full circle error

            if (rebarShape.SimpleArc)
            {
                //code for simple arc
                RebarShapeDefinitionByArc definitionByArc = shapeDefinition as RebarShapeDefinitionByArc;
                IList<RebarShapeConstraint> constraints = definitionByArc.GetConstraints();

                foreach (RebarShapeConstraint constraint in constraints)
                {
                    if (constraint.GetType() == typeof(RebarShapeConstraintArcLength))
                    {
                        ElementId paramId = constraint.GetParamId();
                        //to do: getparameter value at index test
                        ParameterValue parameterValue = rebar.GetParameterValueAtIndex(paramId, barPosition);
                        segmentLengths.Add(parameterValue);
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

                    foreach (RebarShapeConstraint constraint in constraints)
                    {
                        if (constraint.GetType() == typeof(RebarShapeConstraintArcLength)
                            || constraint.GetType() == typeof(RebarShapeConstraintSegmentLength))
                        {
                            ElementId paramId = constraint.GetParamId();
                            //to do: getparameter value at index test
                            ParameterValue parameterValue = rebar.GetParameterValueAtIndex(paramId, barPosition);
                            segmentLengths.Add(parameterValue);
                        }
                    }

                }

            }


            return segmentLengths;


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
